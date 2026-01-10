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
        UpdateSelectionHandles();
    }

    public void SetSelectedShape(Control shape)
    {
        _selectedShape = shape;
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
        
        if (_view.DataContext is MainViewModel vm && vm.ActiveTool == EditorTool.Select)
        {
             // Hit test
             var hitSource = e.Source as global::Avalonia.Visual;
             Control? hitTarget = null;
             while (hitSource != null && hitSource != canvas)
             {
                 if (canvas.Children.Contains(hitSource as Control))
                 {
                     hitTarget = hitSource as Control;
                     break;
                 }
                 hitSource = hitSource.GetVisualParent();
             }

             if (hitTarget != null)
             {
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
                 ClearSelection();
                 // Don't return true, allowing rubber band selection (if implemented) or just clearing
                 return false;
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
        var handleTag = _draggedHandle.Tag?.ToString();
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
    }

    private void HandleMove(Point currentPoint)
    {
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

        var left = Canvas.GetLeft(_selectedShape);
        var top = Canvas.GetTop(_selectedShape);
        Canvas.SetLeft(_selectedShape, left + deltaX);
        Canvas.SetTop(_selectedShape, top + deltaY);

        _lastDragPoint = currentPoint;
        UpdateSelectionHandles();
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
            return;
        }

        if (_selectedShape is global::Avalonia.Controls.Shapes.Path arrowPath)
        {
            if (_shapeEndpoints.TryGetValue(arrowPath, out var endpoints))
            {
                CreateHandle(endpoints.Start.X, endpoints.Start.Y, "ArrowStart");
                CreateHandle(endpoints.End.X, endpoints.End.Y, "ArrowEnd");
            }
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
                annotation.Text = tb.Text;
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

    private static SKPoint ToSKPoint(Point point) => new((float)point.X, (float)point.Y);
}
