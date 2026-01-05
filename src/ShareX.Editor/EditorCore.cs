#region License Information (GPL v3)

/*
    ShareX.Editor - The UI-agnostic Editor library for ShareX
    Copyright (c) 2007-2025 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.Editor.Annotations;
using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// Platform-agnostic image editor core. Handles all editing logic including:
/// - Annotation management (create, select, delete)
/// - Mouse/pointer input processing
/// - Undo/redo operations
/// - Rendering to SKCanvas
/// 
/// Platform hosts (Avalonia, WinForms) provide:
/// - SKCanvas surface for rendering
/// - Forward input events to this core
/// - Display rendered results
/// </summary>
public class EditorCore
{
    #region Events

    /// <summary>
    /// Raised when the editor state changes and a redraw is needed
    /// </summary>
    public event Action? InvalidateRequested;

    /// <summary>
    /// Raised when the status text should be updated
    /// </summary>
    public event Action<string>? StatusTextChanged;

    #endregion

    #region State

    /// <summary>
    /// The source image being edited
    /// </summary>
    public SKBitmap? SourceImage { get; private set; }

    /// <summary>
    /// Current active tool
    /// </summary>
    public EditorTool ActiveTool { get; set; } = EditorTool.Select;

    /// <summary>
    /// Current stroke color (hex string)
    /// </summary>
    public string StrokeColor { get; set; } = "#ef4444";

    /// <summary>
    /// Current stroke width
    /// </summary>
    public float StrokeWidth { get; set; } = 4;

    /// <summary>
    /// Current zoom level (1.0 = 100%)
    /// </summary>
    public float Zoom { get; set; } = 1.0f;

    /// <summary>
    /// Canvas size for rendering
    /// </summary>
    public SKSize CanvasSize { get; set; }

    /// <summary>
    /// Auto-incrementing number counter for Number tool
    /// </summary>
    public int NumberCounter { get; set; } = 1;

    #endregion

    #region Annotations

    private readonly List<Annotation> _annotations = new();
    private readonly Stack<Annotation> _undoStack = new();
    private readonly Stack<Annotation> _redoStack = new();

    private Annotation? _currentAnnotation;
    private Annotation? _selectedAnnotation;
    private bool _isDrawing;
    private SKPoint _startPoint;
    private SKPoint _lastDragPoint;
    private bool _isDragging;
    private bool _isResizing;
    private HandleType _activeHandle;
    private SKRect _initialBounds;

    private const float HandleSize = 10f;
    private enum HandleType { None, TopLeft, TopMiddle, TopRight, MiddleRight, BottomRight, BottomMiddle, BottomLeft, MiddleLeft, Start, End }

    /// <summary>
    /// All annotations in the editor
    /// </summary>
    public IReadOnlyList<Annotation> Annotations => _annotations;

    /// <summary>
    /// Currently selected annotation
    /// </summary>
    public Annotation? SelectedAnnotation => _selectedAnnotation;

    #endregion

    #region Initialization

    /// <summary>
    /// Load an image into the editor
    /// </summary>
    public void LoadImage(SKBitmap bitmap)
    {
        SourceImage?.Dispose();
        SourceImage = bitmap;
        CanvasSize = new SKSize(bitmap.Width, bitmap.Height);
        ClearAll();
        InvalidateRequested?.Invoke();
    }

    /// <summary>
    /// Load image from file path
    /// </summary>
    public void LoadImage(string filePath)
    {
        var bitmap = SKBitmap.Decode(filePath);
        if (bitmap != null)
        {
            LoadImage(bitmap);
        }
    }

    /// <summary>
    /// Clear all annotations
    /// </summary>
    public void ClearAll()
    {
        _annotations.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        _currentAnnotation = null;
        _selectedAnnotation = null;
        _isDrawing = false;
        NumberCounter = 1;
        InvalidateRequested?.Invoke();
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// Handle pointer/mouse press
    /// </summary>
    /// <param name="point">Position in canvas coordinates</param>
    /// <param name="isRightButton">True if right mouse button</param>
    public void OnPointerPressed(SKPoint point, bool isRightButton = false)
    {
        // Right-click deletes annotation under cursor
        if (isRightButton)
        {
            var hitAnnotation = HitTest(point);
            if (hitAnnotation != null)
            {
                _annotations.Remove(hitAnnotation);
                if (_selectedAnnotation == hitAnnotation)
                    _selectedAnnotation = null;
                StatusTextChanged?.Invoke("Annotation deleted");
                InvalidateRequested?.Invoke();
            }
            return;
        }

        _startPoint = point;
        _redoStack.Clear();

        string? sampledSmartEraserColor = null;

        // Sample rendered color for Smart Eraser to mirror Avalonia behavior
        if (ActiveTool == EditorTool.SmartEraser)
        {
            sampledSmartEraserColor = SampleCanvasColor(point);
        }

        // Interact with currently selected annotation first so users can resize/move immediately after drawing
        if (_selectedAnnotation != null)
        {
            var handle = GetHitHandle(point);
            if (handle != HandleType.None)
            {
                BeginResize(handle, point);
                return;
            }

            // Allow dragging a selected annotation even if the current tool is not Select
            if (_selectedAnnotation.HitTest(point))
            {
                _isDragging = true;
                _lastDragPoint = point;
                return;
            }
        }

        // Select mode - hit test existing annotations
        if (ActiveTool == EditorTool.Select)
        {
            var hit = HitTest(point);
            if (hit != null)
            {
                _selectedAnnotation = hit;
                _lastDragPoint = point;
                _isDragging = true;
            }
            else
            {
                _selectedAnnotation = null;
            }
            InvalidateRequested?.Invoke();
            return;
        }

        // Create new annotation based on active tool
        _currentAnnotation = CreateAnnotation(ActiveTool);
        if (_currentAnnotation != null)
        {
            _currentAnnotation.StartPoint = point;
            _currentAnnotation.EndPoint = point;
            _currentAnnotation.StrokeColor = StrokeColor;
            _currentAnnotation.StrokeWidth = StrokeWidth;
            
            // Handle special tools
            if (_currentAnnotation is FreehandAnnotation freehand)
            {
                freehand.Points.Add(point);
            }
            else if (_currentAnnotation is SmartEraserAnnotation smartEraser)
            {
                // Match Avalonia: always use thicker stroke and sampled color if available
                smartEraser.StrokeWidth = 10;
                if (!string.IsNullOrEmpty(sampledSmartEraserColor))
                {
                    smartEraser.StrokeColor = sampledSmartEraserColor;
                }
                else
                {
                    smartEraser.StrokeColor = "#80FF0000";
                }

                // Also seed the freehand point list
                smartEraser.Points.Add(point);
            }
            else if (_currentAnnotation is TextAnnotation textAnn)
            {
                textAnn.FontSize = Math.Max(12, StrokeWidth * 4);
            }
            else if (_currentAnnotation is NumberAnnotation num)
            {
                num.Number = NumberCounter++;
            }
            else if (_currentAnnotation is SpotlightAnnotation spotlight)
            {
                spotlight.CanvasSize = CanvasSize;
            }

            _annotations.Add(_currentAnnotation);
            _isDrawing = true;
            InvalidateRequested?.Invoke();
        }
    }

    /// <summary>
    /// Handle pointer/mouse move
    /// </summary>
    public void OnPointerMoved(SKPoint point)
    {
        if (_isResizing && _selectedAnnotation != null)
        {
            if (_activeHandle == HandleType.Start)
            {
                _selectedAnnotation.StartPoint = point;
                UpdateAnnotationState(_selectedAnnotation);
                InvalidateRequested?.Invoke();
                return;
            }
            if (_activeHandle == HandleType.End)
            {
                _selectedAnnotation.EndPoint = point;
                UpdateAnnotationState(_selectedAnnotation);
                InvalidateRequested?.Invoke();
                return;
            }

            ApplyResize(point);

            _lastDragPoint = point;
            InvalidateRequested?.Invoke();
            return;
        }

        if (_isDragging && _selectedAnnotation != null)
        {
            var delta = new SKPoint(point.X - _lastDragPoint.X, point.Y - _lastDragPoint.Y);
            
            if (_selectedAnnotation is FreehandAnnotation freehand)
            {
                 for(int i=0; i<freehand.Points.Count; i++)
                {
                     freehand.Points[i] = new SKPoint(freehand.Points[i].X + delta.X, freehand.Points[i].Y + delta.Y);
                 }
            }
            
            _selectedAnnotation.StartPoint = new SKPoint(_selectedAnnotation.StartPoint.X + delta.X, _selectedAnnotation.StartPoint.Y + delta.Y);
            _selectedAnnotation.EndPoint = new SKPoint(_selectedAnnotation.EndPoint.X + delta.X, _selectedAnnotation.EndPoint.Y + delta.Y);

            UpdateAnnotationState(_selectedAnnotation);
            
            _lastDragPoint = point;
            InvalidateRequested?.Invoke();
            return;
        }

        if (!_isDrawing || _currentAnnotation == null) return;

        if (_currentAnnotation is FreehandAnnotation freehandDraw)
        {
            freehandDraw.Points.Add(point);
        }
        else
        {
            _currentAnnotation.EndPoint = point;
        }

        if (_currentAnnotation is SpotlightAnnotation spotlight)
        {
            spotlight.CanvasSize = CanvasSize;
        }

        UpdateAnnotationState(_currentAnnotation);

        InvalidateRequested?.Invoke();
    }

    /// <summary>
    /// Handle pointer/mouse release
    /// </summary>
    public void OnPointerReleased(SKPoint point)
    {
        if (_isResizing)
        {
            _isResizing = false;
            _activeHandle = HandleType.None;
            return;
        }

        if (_isDragging)
        {
            _isDragging = false;
            return;
        }

        if (!_isDrawing || _currentAnnotation == null) return;

        _isDrawing = false;

        // Finalize annotation
        _currentAnnotation.EndPoint = point;

        // Crop executes immediately like the Avalonia master behavior
        if (_currentAnnotation is CropAnnotation)
        {
            PerformCrop();
            _currentAnnotation = null;
            return;
        }

        // Add to undo stack
        _undoStack.Push(_currentAnnotation);

        // Auto-select the created annotation (skip freehand/eraser which are not resizable)
        if (_currentAnnotation is not FreehandAnnotation && _currentAnnotation is not SmartEraserAnnotation)
        {
            _selectedAnnotation = _currentAnnotation;
        }
        _currentAnnotation = null;

        // Update effect with final bounds
        if (_selectedAnnotation is BaseEffectAnnotation effect && SourceImage != null)
        {
            effect.UpdateEffect(SourceImage);
        }

        StatusTextChanged?.Invoke($"{ActiveTool} created");
        InvalidateRequested?.Invoke();
    }

    private HandleType GetHitHandle(SKPoint point)
    {
        if (_selectedAnnotation == null) return HandleType.None;

        var handles = GetAnnotationHandles(_selectedAnnotation);
        float halfSize = HandleSize / 2;

        foreach (var handle in handles)
        {
            var r = new SKRect(
                handle.Position.X - halfSize,
                handle.Position.Y - halfSize,
                handle.Position.X + halfSize,
                handle.Position.Y + halfSize);
                
            if (r.Contains(point)) return handle.Type;
        }

        return HandleType.None;
    }

    private void BeginResize(HandleType handle, SKPoint point)
    {
        _isResizing = true;
        _activeHandle = handle;
        _initialBounds = _selectedAnnotation!.GetBounds();
        _lastDragPoint = point;

        if (_selectedAnnotation is FreehandAnnotation freehand)
        {
        }
        else
        {
        }

        InvalidateRequested?.Invoke();
    }

    private void ApplyResize(SKPoint point)
    {
        if (_selectedAnnotation == null) return;

        // Lines/arrows resize via start/end handles only
        if (_selectedAnnotation is LineAnnotation || _selectedAnnotation is ArrowAnnotation)
        {
            return;
        }

        // Freehand/SmartEraser are not resizable in the Avalonia editor
        if (_selectedAnnotation is FreehandAnnotation || _selectedAnnotation is SmartEraserAnnotation)
        {
            return;
        }

        var newBounds = CalculateResizeBounds(point);

        _selectedAnnotation.StartPoint = new SKPoint(newBounds.Left, newBounds.Top);
        _selectedAnnotation.EndPoint = new SKPoint(newBounds.Right, newBounds.Bottom);

        UpdateAnnotationState(_selectedAnnotation);
    }

    private SKRect CalculateResizeBounds(SKPoint point)
    {
        float left = _initialBounds.Left;
        float right = _initialBounds.Right;
        float top = _initialBounds.Top;
        float bottom = _initialBounds.Bottom;

        switch (_activeHandle)
        {
            case HandleType.TopLeft:
                left = point.X;
                top = point.Y;
                break;
            case HandleType.TopMiddle:
                top = point.Y;
                break;
            case HandleType.TopRight:
                right = point.X;
                top = point.Y;
                break;
            case HandleType.MiddleRight:
                right = point.X;
                break;
            case HandleType.BottomRight:
                right = point.X;
                bottom = point.Y;
                break;
            case HandleType.BottomMiddle:
                bottom = point.Y;
                break;
            case HandleType.BottomLeft:
                left = point.X;
                bottom = point.Y;
                break;
            case HandleType.MiddleLeft:
                left = point.X;
                break;
        }

        return new SKRect(
            Math.Min(left, right),
            Math.Min(top, bottom),
            Math.Max(left, right),
            Math.Max(top, bottom));
    }

    private void UpdateAnnotationState(Annotation annotation)
    {
        if (annotation is SpotlightAnnotation spotlight)
        {
            spotlight.CanvasSize = CanvasSize;
        }

        if (annotation is BaseEffectAnnotation effect && SourceImage != null)
        {
            effect.UpdateEffect(SourceImage);
        }
    }

    private string? SampleCanvasColor(SKPoint point)
    {
        using var snapshot = GetSnapshot();
        if (snapshot == null) return null;

        int x = (int)Math.Round(point.X);
        int y = (int)Math.Round(point.Y);

        if (x < 0 || y < 0 || x >= snapshot.Width || y >= snapshot.Height)
        {
            return null;
        }

        var color = snapshot.GetPixel(x, y);
        return $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    }

    #endregion

    #region Selection & HitTest

    private Annotation? HitTest(SKPoint point)
    {
        // Test in reverse order (top-most first)
        for (int i = _annotations.Count - 1; i >= 0; i--)
        {
            if (_annotations[i].HitTest(point))
            {
                return _annotations[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Select an annotation
    /// </summary>
    public void Select(Annotation? annotation)
    {
        _selectedAnnotation = annotation;
        InvalidateRequested?.Invoke();
    }

    /// <summary>
    /// Deselect current annotation
    /// </summary>
    public void Deselect()
    {
        _selectedAnnotation = null;
        InvalidateRequested?.Invoke();
    }

    /// <summary>
    /// Delete the selected annotation
    /// </summary>
    public void DeleteSelected()
    {
        if (_selectedAnnotation != null)
        {
            _annotations.Remove(_selectedAnnotation);
            _selectedAnnotation = null;
            InvalidateRequested?.Invoke();
        }
    }

    #endregion

    #region Undo/Redo

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Undo()
    {
        if (_undoStack.Count > 0)
        {
            var annotation = _undoStack.Pop();
            _annotations.Remove(annotation);
            _redoStack.Push(annotation);
            if (_selectedAnnotation == annotation)
                _selectedAnnotation = null;
            InvalidateRequested?.Invoke();
        }
    }

    public void Redo()
    {
        if (_redoStack.Count > 0)
        {
            var annotation = _redoStack.Pop();
            _annotations.Add(annotation);
            _undoStack.Push(annotation);
            InvalidateRequested?.Invoke();
        }
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Render the entire editor canvas to an SKCanvas
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        canvas.Clear(SKColors.Transparent);

        // Draw source image
        if (SourceImage != null)
        {
            canvas.DrawBitmap(SourceImage, 0, 0);
        }

        // Draw all annotations
        foreach (var annotation in _annotations)
        {
            annotation.Render(canvas);
        }

        // Draw selection handles for selected annotation
        if (_selectedAnnotation != null)
        {
            DrawSelectionHandles(canvas, _selectedAnnotation);
        }
    }

    /// <summary>
    /// Get a snapshot of the current canvas as an SKBitmap
    /// </summary>
    public SKBitmap? GetSnapshot()
    {
        if (SourceImage == null) return null;

        var bitmap = new SKBitmap(SourceImage.Width, SourceImage.Height);
        using var canvas = new SKCanvas(bitmap);
        
        // Draw without selection handles
        canvas.DrawBitmap(SourceImage, 0, 0);
        foreach (var annotation in _annotations)
        {
            annotation.Render(canvas);
        }

        return bitmap;
    }

    private void DrawSelectionHandles(SKCanvas canvas, Annotation annotation)
    {
        if (annotation is FreehandAnnotation || annotation is SmartEraserAnnotation)
        {
            return;
        }

        var bounds = annotation.GetBounds();

        using var strokePaint = new SKPaint
        {
            Color = SKColors.DodgerBlue,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        using var fillPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Draw selection rectangle only for box-based annotations
        if (!(annotation is LineAnnotation || annotation is ArrowAnnotation))
        {
            canvas.DrawRect(bounds, strokePaint);
        }

        // Draw handles
        var handles = GetAnnotationHandles(annotation);
        float handleSize = HandleSize;

        foreach (var handle in handles)
        {
            float radius = handleSize / 2;
            canvas.DrawCircle(handle.Position, radius, fillPaint);
            canvas.DrawCircle(handle.Position, radius, strokePaint);
        }
    }

    private IEnumerable<(HandleType Type, SKPoint Position)> GetAnnotationHandles(Annotation annotation)
    {
        if (annotation is FreehandAnnotation || annotation is SmartEraserAnnotation)
        {
            yield break;
        }
        else if (annotation is LineAnnotation || annotation is ArrowAnnotation)
        {
            yield return (HandleType.Start, annotation.StartPoint);
            yield return (HandleType.End, annotation.EndPoint);
        }
        else
        {
            var bounds = annotation.GetBounds();
            yield return (HandleType.TopLeft, new SKPoint(bounds.Left, bounds.Top));
            yield return (HandleType.TopMiddle, new SKPoint(bounds.MidX, bounds.Top));
            yield return (HandleType.TopRight, new SKPoint(bounds.Right, bounds.Top));
            yield return (HandleType.MiddleRight, new SKPoint(bounds.Right, bounds.MidY));
            yield return (HandleType.BottomRight, new SKPoint(bounds.Right, bounds.Bottom));
            yield return (HandleType.BottomMiddle, new SKPoint(bounds.MidX, bounds.Bottom));
            yield return (HandleType.BottomLeft, new SKPoint(bounds.Left, bounds.Bottom));
            yield return (HandleType.MiddleLeft, new SKPoint(bounds.Left, bounds.MidY));
        }
    }

    #endregion

    #region Annotation Factory

    private Annotation? CreateAnnotation(EditorTool tool)
    {
        return tool switch
        {
            EditorTool.Rectangle => new RectangleAnnotation(),
            EditorTool.Ellipse => new EllipseAnnotation(),
            EditorTool.Line => new LineAnnotation(),
            EditorTool.Arrow => new ArrowAnnotation(),
            EditorTool.Text => new TextAnnotation(),
            EditorTool.Pen => new FreehandAnnotation(),
            EditorTool.SmartEraser => new SmartEraserAnnotation(),
            EditorTool.Number => new NumberAnnotation(),
            EditorTool.Blur => new BlurAnnotation(),
            EditorTool.Pixelate => new PixelateAnnotation(),
            EditorTool.Highlighter => new HighlightAnnotation(),
            EditorTool.Spotlight => new SpotlightAnnotation(),
            EditorTool.Magnify => new MagnifyAnnotation(),
            EditorTool.SpeechBalloon => new SpeechBalloonAnnotation(),
            EditorTool.Crop => new CropAnnotation(),
            _ => null
        };
    }

    #endregion

    #region Crop

    /// <summary>
    /// Perform crop operation using the current crop annotation
    /// </summary>
    public void PerformCrop()
    {
        var cropAnnotation = _annotations.OfType<CropAnnotation>().FirstOrDefault();
        if (cropAnnotation == null || SourceImage == null) return;

        var bounds = cropAnnotation.GetBounds();
        int x = (int)Math.Max(0, bounds.Left);
        int y = (int)Math.Max(0, bounds.Top);
        int width = (int)Math.Min(SourceImage.Width - x, bounds.Width);
        int height = (int)Math.Min(SourceImage.Height - y, bounds.Height);

        if (width <= 0 || height <= 0) return;

        var croppedBitmap = new SKBitmap(width, height);
        SourceImage.ExtractSubset(croppedBitmap, new SKRectI(x, y, x + width, y + height));

        // Remove crop annotation
        _annotations.Remove(cropAnnotation);
        
        // Replace source image
        SourceImage.Dispose();
        SourceImage = croppedBitmap;
        CanvasSize = new SKSize(width, height);

        StatusTextChanged?.Invoke("Image cropped");
        InvalidateRequested?.Invoke();
    }

    #endregion
}
