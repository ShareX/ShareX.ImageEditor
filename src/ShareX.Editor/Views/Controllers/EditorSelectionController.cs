using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ShareX.Editor.Annotations;
using ShareX.Editor.Controls;
using ShareX.Editor.ViewModels;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace ShareX.Editor.Views.Controllers;

public class EditorSelectionController
{
    private readonly EditorView _view;
    private Control? _selectedShape;
    private List<Control> _selectionHandles = new();
    private bool _isDraggingHandle;
    private Control? _draggedHandle;
    private Point _lastDragPoint;
    private Point _startPoint; // Used for resizing deltas
    private bool _isDraggingShape;

    // Hover tracking for ant lines
    private Control? _hoveredShape;
    private global::Avalonia.Controls.Shapes.Rectangle? _hoverOutlineBlack;
    private global::Avalonia.Controls.Shapes.Rectangle? _hoverOutlineWhite;
    private global::Avalonia.Controls.Shapes.Polyline? _hoverPolylineBlack;
    private global::Avalonia.Controls.Shapes.Polyline? _hoverPolylineWhite;
    private global::Avalonia.Controls.Shapes.Ellipse? _hoverEllipseBlack;
    private global::Avalonia.Controls.Shapes.Ellipse? _hoverEllipseWhite;

    // Store arrow/line endpoints for editing
    private Dictionary<Control, (Point Start, Point End)> _shapeEndpoints = new();
    private Dictionary<Control, Point> _speechBalloonTailPoints = new();
    private TextBox? _balloonTextEditor;

    public Control? SelectedShape => _selectedShape;

    // Event invoked when visual needs update (for Effects)
    public event Action<Control>? RequestUpdateEffect;

    public EditorSelectionController(EditorView view)
    {
        _view = view;
    }

    public void ClearSelection()
    {
        _selectedShape = null;
        _isDraggingHandle = false;
        _draggedHandle = null;
        _isDraggingShape = false;
        ClearHoverOutline();
        UpdateSelectionHandles();
    }

    public void SetSelectedShape(Control shape)
    {
        _selectedShape = shape;
        // Set the hovered shape to the selected shape so ant lines appear
        _hoveredShape = shape;
        UpdateHoverOutline();
        UpdateSelectionHandles();
        
        // Auto-enter text edit mode for speech balloon
        if (shape is SpeechBalloonControl balloonControl)
        {
            var canvas = _view.FindControl<Canvas>("AnnotationCanvas");
            if (canvas != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ShowSpeechBalloonTextEditor(balloonControl, canvas);
                }, DispatcherPriority.Normal);
            }
        }
    }

    public bool OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        var canvas = _view.FindControl<Canvas>("AnnotationCanvas");
        if (canvas == null) return false;

        var point = e.GetPosition(canvas);
        var props = e.GetCurrentPoint(canvas).Properties;

        // Check if clicked on a handle
        var overlay = _view.FindControl<Canvas>("OverlayCanvas");
        if (overlay != null)
        {
            var handleSource = e.Source as Control;
            if (handleSource != null && overlay.Children.Contains(handleSource) && handleSource is Border)
            {
                _isDraggingHandle = true;
                _draggedHandle = handleSource;
                _startPoint = point; // Capture start for resize delta
                e.Pointer.Capture(handleSource);
                e.Handled = true;
                return true;
            }
        }

        // Check if clicked on an existing shape (Select or Drag)
        // If we are in Select tool OR user holds Ctrl (multi-select not impl yet) or just clicking existing shapes to move
        // BUT: If active tool is a drawing tool, we usually prioritize drawing NEW shapes unless we click ON a selected shape?
        // ShareX logic: If in Select Tool, selecting works. If in other tools, usually drawing takes precedence unless we click a handle.
        // But logic in EditorView.axaml.cs had: 
        // if (vm.ActiveTool == EditorTool.Select ...) -> Try select.
        
        if (_view.DataContext is MainViewModel vm)
        {
            // Allow dragging selected shapes even when not in Select tool mode
            // This enables immediate repositioning after creating an annotation
            if (_selectedShape != null && vm.ActiveTool != EditorTool.Select)
            {
                // Hit test - find the direct child of the canvas
                var hitSource = e.Source as global::Avalonia.Visual;
                Control? hitTarget = null;
                while (hitSource != null && hitSource != canvas)
                {
                    var candidate = hitSource as Control;
                    if (candidate != null && canvas.Children.Contains(candidate))
                    {
                        hitTarget = candidate;
                        break;
                    }
                    hitSource = hitSource.GetVisualParent();
                }

                if (hitTarget == _selectedShape)
                {
                    _isDraggingShape = true;
                    _lastDragPoint = point;
                    UpdateSelectionHandles();
                    e.Pointer.Capture(hitTarget);
                    e.Handled = true;
                    return true;
                }
            }

            if (vm.ActiveTool == EditorTool.Select)
            {
                 // Hit test
                 var hitSource = e.Source as global::Avalonia.Visual;
                 Control? hitTarget = null;
                 while (hitSource != null && hitSource != canvas)
                 {
                     var candidate = hitSource as Control;
                     if (candidate != null && canvas.Children.Contains(candidate))
                     {
                         hitTarget = candidate;
                         break;
                     }
                     hitSource = hitSource.GetVisualParent();
                 }

                 if (hitTarget != null)
                 {
                     if (hitTarget is TextBox tb && e.ClickCount == 2)
                     {
                         tb.IsHitTestVisible = true;
                         tb.Focus();
                         e.Handled = true;
                         return true;
                     }

                     _selectedShape = hitTarget;
                     _isDraggingShape = true;
                     _lastDragPoint = point;
                     UpdateSelectionHandles();
                     e.Pointer.Capture(hitTarget);
                     e.Handled = true;
                     return true;
                 }
                 else
                 {
                     // Fallback: Manual hit detection (for Polylines with lenient stroke hit test)
                     // If Avalonia hit test failed (e.g. clicked slightly off-stroke on polyline), use our custom tolerance.
                     var manualHit = HitTestShape(canvas, point);
                     if (manualHit != null)
                     {
                          if (manualHit is TextBox tb && e.ClickCount == 2)
                          {
                              tb.IsHitTestVisible = true;
                              tb.Focus();
                              e.Handled = true;
                              return true;
                          }

                          _selectedShape = manualHit;
                          _isDraggingShape = true;
                          _lastDragPoint = point;
                          UpdateSelectionHandles();
                          e.Pointer.Capture(manualHit);
                          e.Handled = true;
                          return true;
                     }

                     ClearSelection();
                     // Don't return true, allowing rubber band selection (if implemented) or just clearing
                     return false;
                 }
            }
        }

        return false;
    }

    public bool OnPointerMoved(object sender, PointerEventArgs e)
    {
        var canvas = _view.FindControl<Canvas>("AnnotationCanvas");
        if (canvas == null) return false;

        var currentPoint = e.GetPosition(canvas);

        if (_isDraggingHandle && _draggedHandle != null && _selectedShape != null)
        {
            HandleResize(currentPoint);
            e.Handled = true;
            return true;
        }

        if (_isDraggingShape && _selectedShape != null)
        {
            HandleMove(currentPoint);
            e.Handled = true;
            return true;
        }

        // Update hover state when not dragging
        UpdateHoverState(canvas, currentPoint);

        return false;
    }

    public bool OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (_isDraggingHandle)
        {
            _isDraggingHandle = false;
            _draggedHandle = null;
            e.Pointer.Capture(null);
            
            if (_selectedShape?.Tag is BaseEffectAnnotation)
            {
                RequestUpdateEffect?.Invoke(_selectedShape);
            }
            return true;
        }

        if (_isDraggingShape)
        {
            _isDraggingShape = false;
            e.Pointer.Capture(null);

            if (_selectedShape?.Tag is BaseEffectAnnotation)
            {
                RequestUpdateEffect?.Invoke(_selectedShape);
            }
            return true;
        }

        return false;
    }

    private void HandleResize(Point currentPoint)
    {
        if (_selectedShape == null || _draggedHandle == null)
        {
            return;
        }

        var handleTag = _draggedHandle.Tag?.ToString();
        if (string.IsNullOrEmpty(handleTag))
        {
            return;
        }

        var deltaX = currentPoint.X - _startPoint.X;
        var deltaY = currentPoint.Y - _startPoint.Y;

        // Special handling for Line endpoints
        if (_selectedShape is global::Avalonia.Controls.Shapes.Line targetLine)
        {
            if (handleTag == "LineStart") targetLine.StartPoint = currentPoint;
            else if (handleTag == "LineEnd") targetLine.EndPoint = currentPoint;
            
            _startPoint = currentPoint;
            UpdateSelectionHandles();
            return;
        }

        // Special handling for Arrow endpoints
        if (_selectedShape is global::Avalonia.Controls.Shapes.Path arrowPath && _view.DataContext is MainViewModel vm)
        {
            if (_shapeEndpoints.TryGetValue(arrowPath, out var endpoints))
            {
                Point arrowStart = endpoints.Start;
                Point arrowEnd = endpoints.End;

                if (handleTag == "ArrowStart") arrowStart = currentPoint;
                else if (handleTag == "ArrowEnd") arrowEnd = currentPoint;

                _shapeEndpoints[arrowPath] = (arrowStart, arrowEnd);
                arrowPath.Data = new ArrowAnnotation().CreateArrowGeometry(arrowStart, arrowEnd, vm.StrokeWidth * 3);
            }
            _startPoint = currentPoint;
            UpdateSelectionHandles();
            return;
        }

        // Special handling for SpeechBalloonControl tail dragging
        if (_selectedShape is SpeechBalloonControl balloonControl && balloonControl.Annotation is SpeechBalloonAnnotation balloon && handleTag == "BalloonTail")
        {
            balloon.TailPoint = new SKPoint((float)currentPoint.X, (float)currentPoint.Y);
            balloonControl.InvalidateVisual();
            _startPoint = currentPoint;
            UpdateSelectionHandles();
            return;
        }

        // Regular shapes
        var left = Canvas.GetLeft(_selectedShape);
        var top = Canvas.GetTop(_selectedShape);
        var width = _selectedShape.Bounds.Width;
        var height = _selectedShape.Bounds.Height;
        if (double.IsNaN(width)) width = _selectedShape.Width;
        if (double.IsNaN(height)) height = _selectedShape.Height;

        // Special handling for SpeechBalloonControl resizing
        if (_selectedShape is SpeechBalloonControl resizeBalloonControl && resizeBalloonControl.Annotation is SpeechBalloonAnnotation resizeBalloon)
        {
             double newLeft = left;
             double newTop = top;
             double newWidth = width;
             double newHeight = height;

             if (handleTag.Contains("Right")) newWidth = Math.Max(20, width + deltaX);
             else if (handleTag.Contains("Left")) { var change = Math.Min(width - 20, deltaX); newLeft += change; newWidth -= change; }

             if (handleTag.Contains("Bottom")) newHeight = Math.Max(20, height + deltaY);
             else if (handleTag.Contains("Top")) { var change = Math.Min(height - 20, deltaY); newTop += change; newHeight -= change; }

             resizeBalloon.StartPoint = ToSKPoint(new Point(newLeft, newTop));
             resizeBalloon.EndPoint = ToSKPoint(new Point(newLeft + newWidth, newTop + newHeight));

             Canvas.SetLeft(resizeBalloonControl, newLeft);
             Canvas.SetTop(resizeBalloonControl, newTop);
             resizeBalloonControl.Width = newWidth;
             resizeBalloonControl.Height = newHeight;
             resizeBalloonControl.InvalidateVisual();

             _startPoint = currentPoint;
             UpdateSelectionHandles();
             return;
        }

        if (_selectedShape is global::Avalonia.Controls.Shapes.Rectangle || _selectedShape is global::Avalonia.Controls.Shapes.Ellipse || _selectedShape is Grid)
        {
             double newLeft = left;
             double newTop = top;
             double newWidth = width;
             double newHeight = height;

             if (handleTag.Contains("Right")) newWidth = Math.Max(1, width + deltaX);
             else if (handleTag.Contains("Left")) { var change = Math.Min(width - 1, deltaX); newLeft += change; newWidth -= change; }

             if (handleTag.Contains("Bottom")) newHeight = Math.Max(1, height + deltaY);
             else if (handleTag.Contains("Top")) { var change = Math.Min(height - 1, deltaY); newTop += change; newHeight -= change; }

             Canvas.SetLeft(_selectedShape, newLeft);
             Canvas.SetTop(_selectedShape, newTop);
             _selectedShape.Width = newWidth;
             _selectedShape.Height = newHeight;
        }

        _startPoint = currentPoint;
        UpdateSelectionHandles();

        // Real-time effect update during resize
        if (_selectedShape?.Tag is BaseEffectAnnotation)
        {
            RequestUpdateEffect?.Invoke(_selectedShape);
        }
    }

    private void HandleMove(Point currentPoint)
    {
        if (_selectedShape == null)
        {
            return;
        }

        var deltaX = currentPoint.X - _lastDragPoint.X;
        var deltaY = currentPoint.Y - _lastDragPoint.Y;

        if (_selectedShape is global::Avalonia.Controls.Shapes.Line targetLine)
        {
            targetLine.StartPoint = new Point(targetLine.StartPoint.X + deltaX, targetLine.StartPoint.Y + deltaY);
            targetLine.EndPoint = new Point(targetLine.EndPoint.X + deltaX, targetLine.EndPoint.Y + deltaY);
            _lastDragPoint = currentPoint;
            UpdateSelectionHandles();
            return;
        }

        if (_selectedShape is global::Avalonia.Controls.Shapes.Path arrowPath && _view.DataContext is MainViewModel vm)
        {
            if (_shapeEndpoints.TryGetValue(arrowPath, out var endpoints))
            {
                var newStart = new Point(endpoints.Start.X + deltaX, endpoints.Start.Y + deltaY);
                var newEnd = new Point(endpoints.End.X + deltaX, endpoints.End.Y + deltaY);

                _shapeEndpoints[arrowPath] = (newStart, newEnd);
                arrowPath.Data = new ArrowAnnotation().CreateArrowGeometry(newStart, newEnd, vm.StrokeWidth * 3);
            }
            _lastDragPoint = currentPoint;
            UpdateSelectionHandles();
            return;
        }

        if (_selectedShape is SpeechBalloonControl balloonControl && balloonControl.Annotation is SpeechBalloonAnnotation balloon)
        {
            var currentStart = balloon.StartPoint;
            var currentEnd = balloon.EndPoint;

            var newStartPoint = new SKPoint(currentStart.X + (float)deltaX, currentStart.Y + (float)deltaY);
            var newEndPoint = new SKPoint(currentEnd.X + (float)deltaX, currentEnd.Y + (float)deltaY);

            balloon.StartPoint = newStartPoint;
            balloon.EndPoint = newEndPoint;

            balloon.TailPoint = new SKPoint(balloon.TailPoint.X + (float)deltaX, balloon.TailPoint.Y + (float)deltaY);

            var newLeft = Canvas.GetLeft(balloonControl) + deltaX;
            var newTop = Canvas.GetTop(balloonControl) + deltaY;
            Canvas.SetLeft(balloonControl, newLeft);
            Canvas.SetTop(balloonControl, newTop);

            balloonControl.InvalidateVisual();
            _lastDragPoint = currentPoint;
            UpdateSelectionHandles();
            return;
        }

        // Handle Polyline (freehand/eraser) movement by translating all points
        if (_selectedShape is Polyline polyline)
        {
            var newPoints = new Points();
            foreach (var pt in polyline.Points)
            {
                newPoints.Add(new Point(pt.X + deltaX, pt.Y + deltaY));
            }
            polyline.Points = newPoints;
            
            // Also update the annotation's points if present
            if (polyline.Tag is FreehandAnnotation freehand)
            {
                for (int i = 0; i < freehand.Points.Count; i++)
                {
                    var oldPt = freehand.Points[i];
                    freehand.Points[i] = new SKPoint(oldPt.X + (float)deltaX, oldPt.Y + (float)deltaY);
                }
            }
            else if (polyline.Tag is SmartEraserAnnotation eraser)
            {
                for (int i = 0; i < eraser.Points.Count; i++)
                {
                    var oldPt = eraser.Points[i];
                    eraser.Points[i] = new SKPoint(oldPt.X + (float)deltaX, oldPt.Y + (float)deltaY);
                }
            }
            
            polyline.InvalidateVisual();
            _lastDragPoint = currentPoint;
            UpdateSelectionHandles();
            return;
        }

        var left = Canvas.GetLeft(_selectedShape);
        var top = Canvas.GetTop(_selectedShape);
        Canvas.SetLeft(_selectedShape, left + deltaX);
        Canvas.SetTop(_selectedShape, top + deltaY);

        _lastDragPoint = currentPoint;
        UpdateSelectionHandles();

        // Real-time effect update during move
        if (_selectedShape?.Tag is BaseEffectAnnotation)
        {
            RequestUpdateEffect?.Invoke(_selectedShape);
        }
    }

    public void UpdateSelectionHandles()
    {
        var overlay = _view.FindControl<Canvas>("OverlayCanvas");
        if (overlay == null) return;

        foreach (var handle in _selectionHandles)
        {
            overlay.Children.Remove(handle);
        }
        _selectionHandles.Clear();

        if (_selectedShape == null) return;

        if (_selectedShape is global::Avalonia.Controls.Shapes.Line line)
        {
            CreateHandle(line.StartPoint.X, line.StartPoint.Y, "LineStart");
            CreateHandle(line.EndPoint.X, line.EndPoint.Y, "LineEnd");
            UpdateHoverOutline();
            return;
        }

        if (_selectedShape is global::Avalonia.Controls.Shapes.Path arrowPath)
        {
            if (_shapeEndpoints.TryGetValue(arrowPath, out var endpoints))
            {
                CreateHandle(endpoints.Start.X, endpoints.Start.Y, "ArrowStart");
                CreateHandle(endpoints.End.X, endpoints.End.Y, "ArrowEnd");
            }
            UpdateHoverOutline();
            return;
        }

        if (_selectedShape is SpeechBalloonControl balloonControl && balloonControl.Annotation is SpeechBalloonAnnotation balloon)
        {
             var balloonLeft = Canvas.GetLeft(_selectedShape);
             var balloonTop = Canvas.GetTop(_selectedShape);
             var balloonWidth = _selectedShape.Bounds.Width;
             var balloonHeight = _selectedShape.Bounds.Height;
             if (double.IsNaN(balloonWidth)) balloonWidth = _selectedShape.Width;
             if (double.IsNaN(balloonHeight)) balloonHeight = _selectedShape.Height;

             CreateHandle(balloonLeft, balloonTop, "TopLeft");
             CreateHandle(balloonLeft + balloonWidth / 2, balloonTop, "TopCenter");
             CreateHandle(balloonLeft + balloonWidth, balloonTop, "TopRight");
             CreateHandle(balloonLeft + balloonWidth, balloonTop + balloonHeight / 2, "RightCenter");
             CreateHandle(balloonLeft + balloonWidth, balloonTop + balloonHeight, "BottomRight");
             CreateHandle(balloonLeft + balloonWidth / 2, balloonTop + balloonHeight, "BottomCenter");
             CreateHandle(balloonLeft, balloonTop + balloonHeight, "BottomLeft");
             CreateHandle(balloonLeft, balloonTop + balloonHeight / 2, "LeftCenter");

             if (balloon.TailPoint.X == 0 && balloon.TailPoint.Y == 0)
             {
                 balloon.TailPoint = new SKPoint(
                     balloon.StartPoint.X + (balloon.EndPoint.X - balloon.StartPoint.X) / 2,
                     balloon.EndPoint.Y + 30
                 );
                 balloonControl.InvalidateVisual();
             }

             var tailX = (double)balloon.TailPoint.X;
             var tailY = (double)balloon.TailPoint.Y;
             CreateHandle(tailX, tailY, "BalloonTail");
             UpdateHoverOutline();
             return;
        }

        if (_selectedShape is Polyline || _selectedShape is TextBox)
        {
            UpdateHoverOutline();
            return;
        }

        if (_selectedShape is Grid || _selectedShape is ShareX.Editor.Controls.SpotlightControl) return;

        // Fallback to explicit Width/Height if Bounds are not yet calculated (e.g. before layout pass)
        var width = _selectedShape.Bounds.Width;
        var height = _selectedShape.Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            width = _selectedShape.Width;
            height = _selectedShape.Height;
        }

        if (width <= 0 || height <= 0) return;

        var shapeLeft = Canvas.GetLeft(_selectedShape);
        var shapeTop = Canvas.GetTop(_selectedShape);
        if (double.IsNaN(width)) width = _selectedShape.Width;
        if (double.IsNaN(height)) height = _selectedShape.Height;

        CreateHandle(shapeLeft, shapeTop, "TopLeft");
        CreateHandle(shapeLeft + width / 2, shapeTop, "TopCenter");
        CreateHandle(shapeLeft + width, shapeTop, "TopRight");
        CreateHandle(shapeLeft + width, shapeTop + height / 2, "RightCenter");
        CreateHandle(shapeLeft + width, shapeTop + height, "BottomRight");
        CreateHandle(shapeLeft + width / 2, shapeTop + height, "BottomCenter");
        CreateHandle(shapeLeft, shapeTop + height, "BottomLeft");
        CreateHandle(shapeLeft, shapeTop + height / 2, "LeftCenter");
        UpdateHoverOutline();
    }

    private void CreateHandle(double x, double y, string tag)
    {
        var overlay = _view.FindControl<Canvas>("OverlayCanvas");
        if (overlay == null) return;

        Cursor cursor = Cursor.Parse("Hand");
        if (tag.Contains("TopLeft") || tag.Contains("BottomRight")) cursor = new Cursor(StandardCursorType.TopLeftCorner);
        else if (tag.Contains("TopRight") || tag.Contains("BottomLeft")) cursor = new Cursor(StandardCursorType.TopRightCorner);
        else if (tag.Contains("Top") || tag.Contains("Bottom")) cursor = new Cursor(StandardCursorType.SizeNorthSouth);
        else if (tag.Contains("Left") || tag.Contains("Right")) cursor = new Cursor(StandardCursorType.SizeWestEast);

        var handleBorder = new Border
        {
            Width = 15,
            Height = 15,
            CornerRadius = new CornerRadius(10),
            Background = Brushes.White,
            Tag = tag,
            Cursor = cursor,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 0,
                Blur = 8,
                Spread = 0,
                Color = Color.FromArgb(100, 0, 0, 0)
            })
        };

        Canvas.SetLeft(handleBorder, x - handleBorder.Width / 2);
        Canvas.SetTop(handleBorder, y - handleBorder.Height / 2);

        overlay.Children.Add(handleBorder);
        _selectionHandles.Add(handleBorder);
    }
    
    private void ShowSpeechBalloonTextEditor(SpeechBalloonControl balloonControl, Canvas canvas)
    {
        if (balloonControl.Annotation == null) return;

        if (_balloonTextEditor != null)
        {
            canvas.Children.Remove(_balloonTextEditor);
            _balloonTextEditor = null;
        }

        var annotation = balloonControl.Annotation;
        var balloonLeft = Canvas.GetLeft(balloonControl);
        var balloonTop = Canvas.GetTop(balloonControl);
        var balloonWidth = balloonControl.Width;
        var balloonHeight = balloonControl.Height;

        var textBox = new TextBox
        {
            Text = annotation.Text,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.Parse(annotation.StrokeColor)),
            FontSize = annotation.FontSize,
            Padding = new Thickness(12),
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            AcceptsReturn = false,
            TextWrapping = TextWrapping.Wrap
        };

        Canvas.SetLeft(textBox, balloonLeft);
        Canvas.SetTop(textBox, balloonTop);
        textBox.Width = balloonWidth;
        textBox.Height = balloonHeight;

        textBox.LostFocus += (s, args) =>
        {
            if (s is TextBox tb)
            {
                annotation.Text = tb.Text ?? string.Empty;
                balloonControl.InvalidateVisual();
                canvas.Children.Remove(tb);
                _balloonTextEditor = null;
            }
        };

        textBox.KeyDown += (s, args) =>
        {
            if (args.Key == Key.Enter || args.Key == Key.Escape)
            {
                args.Handled = true;
                _view.Focus();
            }
        };

        canvas.Children.Add(textBox);
        _balloonTextEditor = textBox;
        textBox.Focus();
        textBox.SelectAll();
    }
    
    public void PerformDelete()
    {
        if (_selectedShape != null)
        {
             var canvas = _view.FindControl<Canvas>("AnnotationCanvas");
             if (canvas != null && canvas.Children.Contains(_selectedShape))
             {
                 canvas.Children.Remove(_selectedShape);
             
                 if (_selectedShape is SpeechBalloonControl && _balloonTextEditor != null)
                 {
                     canvas.Children.Remove(_balloonTextEditor);
                     _balloonTextEditor = null;
                 }
                 
                 // Note: Undo stack logic not fully integrated here since EditorView owned it.
                 // For now we assume EditorView handles undo/redo wrapping or we rely on events.
                 // BUT: PerformDelete in EditorView used to push to redo stack?
                 // Wait, EditorView.PerformDelete handles undo stack logic.
                 // To avoid breaking Undo, we should perhaps keep PerformDelete in EditorView
                 // OR EditorSelectionController delegates back to EditorView/UndoService?
                 // Given constraints to NO logic changes, I should probably expose the inner delete logic
                 // or call back to EditorView to delete.
                 // But cleaning up handles is definitely SelectionController job.
             }
             ClearSelection();
        }
    }

    public void RegisterArrowEndpoint(Control path, Point start, Point end)
    {
        _shapeEndpoints[path] = (start, end);
    }


    private void UpdateHoverState(Canvas canvas, Point currentPoint)
    {
        // Only show hover outlines when Select tool is active
        if (_view.DataContext is MainViewModel vm && vm.ActiveTool != EditorTool.Select)
        {
            ClearHoverOutline();
            return;
        }
        
        // Find shape under cursor (hit test)
        Control? hitShape = HitTestShape(canvas, currentPoint);
        
        // If we're hovering over the selected shape, keep showing ant lines on it
        // Otherwise, show ant lines on the hovered (unselected) shape
        if (hitShape == _selectedShape && _selectedShape != null)
        {
            // Keep showing ant lines on selected shape
            if (_hoveredShape != _selectedShape)
            {
                ClearHoverOutline();
                _hoveredShape = _selectedShape;
            }
            UpdateHoverOutline();
        }
        else if (hitShape != _hoveredShape)
        {
            // Hovering over a different shape (or no shape)
            ClearHoverOutline();
            _hoveredShape = hitShape;
            
            if (_hoveredShape != null)
            {
                UpdateHoverOutline();
            }
        }
        else if (_hoveredShape != null)
        {
            // Shape is still hovered, update outline position (in case shape moved)
            UpdateHoverOutline();
        }
    }
    
    public Control? HitTestShape(Canvas canvas, Point currentPoint)
    {
        // Iterate through canvas children in reverse (top-most first)
        for (int i = canvas.Children.Count - 1; i >= 0; i--)
        {
            var child = canvas.Children[i] as Control;
            if (child == null) continue;
            
            // Skip non-moveable overlays and text editors
            if (child.Name == "CropOverlay" || child.Name == "CutOutOverlay") continue;
            // TextBox excluded? No, we want to select it now.
            // if (child is TextBox) continue;
            
            // Check if point is within the bounds of this control
            var bounds = child.Bounds;
            var left = Canvas.GetLeft(child);
            var top = Canvas.GetTop(child);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            
            var shapeBounds = new Rect(left, top, bounds.Width, bounds.Height);
            
            // Special handling for Line
            if (child is global::Avalonia.Controls.Shapes.Line line)
            {
                var minX = Math.Min(line.StartPoint.X, line.EndPoint.X) - 5;
                var minY = Math.Min(line.StartPoint.Y, line.EndPoint.Y) - 5;
                var maxX = Math.Max(line.StartPoint.X, line.EndPoint.X) + 5;
                var maxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y) + 5;
                shapeBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            // Special handling for Path (Arrow)
            else if (child is global::Avalonia.Controls.Shapes.Path && _shapeEndpoints.TryGetValue(child, out var endpoints))
            {
                var minX = Math.Min(endpoints.Start.X, endpoints.End.X) - 10;
                var minY = Math.Min(endpoints.Start.Y, endpoints.End.Y) - 10;
                var maxX = Math.Max(endpoints.Start.X, endpoints.End.X) + 10;
                var maxY = Math.Max(endpoints.Start.Y, endpoints.End.Y) + 10;
                shapeBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            // Special handling for Polyline (Freehand)
            else if (child is global::Avalonia.Controls.Shapes.Polyline polyline && polyline.Points != null)
            {
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                foreach (var p in polyline.Points)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }
                if (minX == double.MaxValue) 
                { 
                    minX = 0; minY = 0; maxX = 0; maxY = 0; 
                }
                else
                {
                    minX -= 5; minY -= 5; maxX += 5; maxY += 5;
                }
                shapeBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            
            if (shapeBounds.Contains(currentPoint))
            {
                // Refined Hit Test for Polyline
                if (child is global::Avalonia.Controls.Shapes.Polyline polylineObj && polylineObj.Points != null)
                {
                     if (!IsPointNearPolyline(currentPoint, polylineObj, 5 + polylineObj.StrokeThickness / 2))
                     {
                         continue; // Point is in bounds but not near stroke
                     }
                }
                
                return child;
            }
        }
        return null;
    }

    private void ClearHoverOutline()
    {
        var overlay = _view.FindControl<Canvas>("OverlayCanvas");
        if (_hoverOutlineBlack != null)
        {
            overlay?.Children.Remove(_hoverOutlineBlack);
            _hoverOutlineBlack = null;
        }
        if (_hoverOutlineWhite != null)
        {
            overlay?.Children.Remove(_hoverOutlineWhite);
            _hoverOutlineWhite = null;
        }
        if (_hoverPolylineBlack != null)
        {
            overlay?.Children.Remove(_hoverPolylineBlack);
            _hoverPolylineBlack = null;
        }
        if (_hoverPolylineWhite != null)
        {
            overlay?.Children.Remove(_hoverPolylineWhite);
            _hoverPolylineWhite = null;
        }
        if (_hoverEllipseBlack != null)
        {
            overlay?.Children.Remove(_hoverEllipseBlack);
            _hoverEllipseBlack = null;
        }
        if (_hoverEllipseWhite != null)
        {
            overlay?.Children.Remove(_hoverEllipseWhite);
            _hoverEllipseWhite = null;
        }
        _hoveredShape = null;
    }
    
    private void UpdateHoverOutline()
    {
        if (_hoveredShape == null) return;
        
        var overlay = _view.FindControl<Canvas>("OverlayCanvas");
        if (overlay == null) return;
        
        double left, top, width, height;
        
        // Get bounds based on shape type
        if (_hoveredShape is global::Avalonia.Controls.Shapes.Line line)
        {
            left = Math.Min(line.StartPoint.X, line.EndPoint.X);
            top = Math.Min(line.StartPoint.Y, line.EndPoint.Y);
            width = Math.Abs(line.EndPoint.X - line.StartPoint.X);
            height = Math.Abs(line.EndPoint.Y - line.StartPoint.Y);
            // Add some padding for thin lines
            if (width < 10) { left -= 5; width += 10; }
            if (height < 10) { top -= 5; height += 10; }
        }
        else if (_hoveredShape is global::Avalonia.Controls.Shapes.Path && _shapeEndpoints.TryGetValue(_hoveredShape, out var endpoints))
        {
            left = Math.Min(endpoints.Start.X, endpoints.End.X);
            top = Math.Min(endpoints.Start.Y, endpoints.End.Y);
            width = Math.Abs(endpoints.End.X - endpoints.Start.X);
            height = Math.Abs(endpoints.End.Y - endpoints.Start.Y);
            // Add some padding for thin lines
            if (width < 10) { left -= 5; width += 10; }
            if (height < 10) { top -= 5; height += 10; }
        }
        else
        {
            left = Canvas.GetLeft(_hoveredShape);
            top = Canvas.GetTop(_hoveredShape);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            width = _hoveredShape.Bounds.Width;
            height = _hoveredShape.Bounds.Height;
            if (width <= 0) width = _hoveredShape.Width;
            if (height <= 0) height = _hoveredShape.Height;
        }
        
        if (width <= 0 || height <= 0) return;
        
        if (width <= 0 || height <= 0) return;
        
        // Helper to safely remove generic outline rects if we switched to Polyline mode or vice versa?
        // Actually UpdateHoverState calls ClearHoverOutline when shape changes, so we start fresh.
        // But for safety/robustness we can ensure only the correct type exists.
        
        if (_hoveredShape is Ellipse || (_hoveredShape is Grid && _hoveredShape.Tag is NumberAnnotation))
        {
             if (_hoverEllipseBlack == null)
             {
                 _hoverEllipseBlack = new Ellipse
                 {
                     Stroke = Brushes.Black,
                     StrokeThickness = 1,
                     StrokeDashArray = new global::Avalonia.Collections.AvaloniaList<double> { 3, 3 },
                     IsHitTestVisible = false
                 };
                 overlay.Children.Add(_hoverEllipseBlack);
             }
             if (_hoverEllipseWhite == null)
             {
                 _hoverEllipseWhite = new Ellipse
                 {
                     Stroke = Brushes.White,
                     StrokeThickness = 1,
                     StrokeDashArray = new global::Avalonia.Collections.AvaloniaList<double> { 3, 3 },
                     StrokeDashOffset = 3,
                     IsHitTestVisible = false
                 };
                 overlay.Children.Add(_hoverEllipseWhite);
             }

             Canvas.SetLeft(_hoverEllipseBlack, left - 2);
             Canvas.SetTop(_hoverEllipseBlack, top - 2);
             _hoverEllipseBlack.Width = width + 4;
             _hoverEllipseBlack.Height = height + 4;

             Canvas.SetLeft(_hoverEllipseWhite, left - 2);
             Canvas.SetTop(_hoverEllipseWhite, top - 2);
             _hoverEllipseWhite.Width = width + 4;
             _hoverEllipseWhite.Height = height + 4;
             return;
        }

        if (_hoveredShape is Polyline polyline)
        {
             if (_hoverPolylineBlack == null)
             {
                 _hoverPolylineBlack = new Polyline
                 {
                     Stroke = Brushes.Black,
                     StrokeThickness = 1,
                     StrokeDashArray = new global::Avalonia.Collections.AvaloniaList<double> { 3, 3 },
                     IsHitTestVisible = false
                 };
                 overlay.Children.Add(_hoverPolylineBlack);
             }
             if (_hoverPolylineWhite == null)
             {
                 _hoverPolylineWhite = new Polyline
                 {
                     Stroke = Brushes.White,
                     StrokeThickness = 1,
                     StrokeDashArray = new global::Avalonia.Collections.AvaloniaList<double> { 3, 3 },
                     StrokeDashOffset = 3,
                     IsHitTestVisible = false
                 };
                 overlay.Children.Add(_hoverPolylineWhite);
             }
             
             // Sync points
             _hoverPolylineBlack.Points = polyline.Points;
             _hoverPolylineWhite.Points = polyline.Points;
             return;
        }
        
        // Create or update the hover outline (two overlapping rectangles for black/white ant pattern)
        if (_hoverOutlineBlack == null)
        {
            _hoverOutlineBlack = new global::Avalonia.Controls.Shapes.Rectangle
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                StrokeDashArray = new global::Avalonia.Collections.AvaloniaList<double> { 3, 3 },
                Fill = null,
                IsHitTestVisible = false
            };
            overlay.Children.Add(_hoverOutlineBlack);
        }
        
        if (_hoverOutlineWhite == null)
        {
            _hoverOutlineWhite = new global::Avalonia.Controls.Shapes.Rectangle
            {
                Stroke = Brushes.White,
                StrokeThickness = 1,
                StrokeDashArray = new global::Avalonia.Collections.AvaloniaList<double> { 3, 3 },
                StrokeDashOffset = 3, // Offset by dash length to alternate
                Fill = null,
                IsHitTestVisible = false
            };
            overlay.Children.Add(_hoverOutlineWhite);
        }
        
        Canvas.SetLeft(_hoverOutlineBlack, left - 2);
        Canvas.SetTop(_hoverOutlineBlack, top - 2);
        _hoverOutlineBlack.Width = width + 4;
        _hoverOutlineBlack.Height = height + 4;
        
        Canvas.SetLeft(_hoverOutlineWhite, left - 2);
        Canvas.SetTop(_hoverOutlineWhite, top - 2);
        _hoverOutlineWhite.Width = width + 4;
        _hoverOutlineWhite.Height = height + 4;
    }

    private static SKPoint ToSKPoint(Point point) => new((float)point.X, (float)point.Y);

    private bool IsPointNearPolyline(Point point, Polyline polyline, double threshold)
    {
        if (polyline.Points == null || polyline.Points.Count < 2) return false;

        var thresholdSq = threshold * threshold;

        for (int i = 0; i < polyline.Points.Count - 1; i++)
        {
            var p1 = polyline.Points[i];
            var p2 = polyline.Points[i + 1];

            var l2 = DistanceSquared(p1, p2);
            if (l2 == 0)
            {
                if (DistanceSquared(point, p1) <= thresholdSq) return true;
                continue;
            }

            var t = ((point.X - p1.X) * (p2.X - p1.X) + (point.Y - p1.Y) * (p2.Y - p1.Y)) / l2;
            t = Math.Max(0, Math.Min(1, t));

            var projX = p1.X + t * (p2.X - p1.X);
            var projY = p1.Y + t * (p2.Y - p1.Y);

            var distSq = (point.X - projX) * (point.X - projX) + (point.Y - projY) * (point.Y - projY);
            if (distSq <= thresholdSq) return true;
        }

        return false;
    }

    private double DistanceSquared(Point p1, Point p2)
    {
        return (p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y);
    }
}
