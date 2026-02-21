using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ShareX.ImageEditor.Annotations;
using ShareX.ImageEditor.Controls;
using ShareX.ImageEditor.Helpers;
using ShareX.ImageEditor.ViewModels;
using SkiaSharp;

namespace ShareX.ImageEditor.Views.Controllers;

public class EditorInputController
{
    private readonly EditorView _view;
    private readonly EditorSelectionController _selectionController;
    private readonly EditorZoomController _zoomController;

    // Minimum shape size to prevent accidental creation of tiny shapes
    private const double MinShapeSize = 5;

    private Point _startPoint;
    private Control? _currentShape;
    private bool _isDrawing;
    private bool _isCreatingEffect;

    // Track cut-out direction (null = not determined yet, true = vertical, false = horizontal)
    private bool? _cutOutDirection;

    // Cached SKBitmap for effect updates
    private SKBitmap? _cachedSkBitmap;

    // Crop handle state: after drawing the crop rect, before confirming
    private bool _cropActive;
    private List<Control> _cropHandles = new();
    private bool _isDraggingCropHandle;
    private string? _draggedCropHandleTag;
    private Point _cropDragStartPoint;
    private Rect _cropDragStartRect;
    private readonly List<Rectangle> _cropShadeRects = new();
    private readonly List<Line> _cropGuideLines = new();

    public EditorInputController(EditorView view, EditorSelectionController selectionController, EditorZoomController zoomController)
    {
        _view = view;
        _selectionController = selectionController;
        _zoomController = zoomController;
    }

    private MainViewModel? ViewModel => _view.DataContext as MainViewModel;

    public async void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        var canvas = _view.FindControl<Canvas>("AnnotationCanvas") ?? sender as Canvas;
        if (canvas == null) return;

        var props = e.GetCurrentPoint(canvas).Properties;
        if (props.IsMiddleButtonPressed)
        {
            _zoomController.OnScrollViewerPointerPressed(_view.FindControl<ScrollViewer>("CanvasScrollViewer"), e);
            return;
        }

        if (_isDrawing && props.IsRightButtonPressed && (vm.ActiveTool == EditorTool.Crop || vm.ActiveTool == EditorTool.CutOut))
        {
            CancelActiveRegionDrawing(canvas);
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        // Right-click cancels an active (drawn but unconfirmed) crop
        if (_cropActive && props.IsRightButtonPressed && vm.ActiveTool == EditorTool.Crop)
        {
            CancelCrop();
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (props.IsRightButtonPressed)
        {
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

            if (hitTarget == null)
            {
                hitTarget = _selectionController.HitTestShape(canvas, e.GetPosition(canvas));
            }

            if (hitTarget != null)
            {
                // Select the shape if not already selected (standard right-click behavior)
                if (_selectionController.SelectedShape != hitTarget)
                {
                    _selectionController.SetSelectedShape(hitTarget);
                }

                _view.OpenContextMenu(hitTarget);
                e.Handled = true;
                return;
            }

            // If clicked on empty space, open context menu on canvas
            _view.OpenContextMenu(canvas);
            e.Handled = true;
            return;
        }

        // When a crop rect is drawn and awaiting confirmation, intercept pointer events
        if (_cropActive)
        {
            var overlayCanvas = _view.FindControl<Canvas>("OverlayCanvas");
            var cropOverlay = _view.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");

            // Check if a crop resize/move handle was clicked (walk up from source so child Path/Rectangle still counts)
            if (overlayCanvas != null)
            {
                var hitSource = e.Source as Control;
                Control? cropBorder = null;
                string? cropTag = null;
                while (hitSource != null && hitSource != overlayCanvas)
                {
                    if (hitSource.Tag is string tag && tag.StartsWith("Crop_"))
                    {
                        cropBorder = hitSource;
                        cropTag = tag;
                        break;
                    }
                    hitSource = hitSource.GetVisualParent() as Control;
                }
                if (cropBorder != null && cropTag != null && overlayCanvas.Children.Contains(cropBorder) && cropOverlay != null)
                {
                    _cropDragStartRect = new Rect(Canvas.GetLeft(cropOverlay), Canvas.GetTop(cropOverlay), cropOverlay.Width, cropOverlay.Height);
                    _cropDragStartPoint = e.GetPosition(canvas);
                    _draggedCropHandleTag = cropTag;
                    _isDraggingCropHandle = true;
                    e.Pointer.Capture(cropBorder);
                    e.Handled = true;
                    return;
                }
            }

            if (cropOverlay != null)
            {
                var clickPos = e.GetPosition(canvas);
                var cropBounds = new Rect(Canvas.GetLeft(cropOverlay), Canvas.GetTop(cropOverlay), cropOverlay.Width, cropOverlay.Height);

                // Double-click inside crop area → confirm
                if (e.ClickCount == 2 && cropBounds.Contains(clickPos))
                {
                    TryConfirmCrop();
                    e.Handled = true;
                    return;
                }

                // Single-click inside crop area → drag/move entire crop rect
                if (cropBounds.Contains(clickPos))
                {
                    _cropDragStartRect = cropBounds;
                    _cropDragStartPoint = clickPos;
                    _draggedCropHandleTag = "Crop_Move";
                    _isDraggingCropHandle = true;
                    e.Pointer.Capture(canvas);
                    e.Handled = true;
                    return;
                }
            }

            // Click outside crop area → cancel and fall through to start a new crop
            CancelCrop();
        }

        var selectionSender = sender ?? canvas;
        if (_selectionController.OnPointerPressed(selectionSender, e))
        {
            return;
        }

        // ISSUE-019 fix: Dead code removed - redo stack cleared by EditorCore

        var point = e.GetPosition(canvas);
        _startPoint = point;
        _isDrawing = true;
        e.Pointer.Capture(canvas);

        var brush = new SolidColorBrush(Color.Parse(vm.SelectedColor));

        if (vm.ActiveTool == EditorTool.Crop)
        {
            // If crop not yet active, full-image overlay + handles are shown when tool was selected (ActivateCropToFullImage).
            // This pointer down is then for dragging a handle or moving the crop rect (handled in _cropActive block above).
            if (!_cropActive)
            {
                ActivateCropToFullImage();
            }
            return;
        }

        if (vm.ActiveTool == EditorTool.CutOut)
        {
            // Clamp start point to canvas bounds
            var clampedX = Math.Max(0, Math.Min(_startPoint.X, canvas.Bounds.Width));
            var clampedY = Math.Max(0, Math.Min(_startPoint.Y, canvas.Bounds.Height));
            _startPoint = new Point(clampedX, clampedY);

            _cutOutDirection = null;
            var cutOutOverlay = new global::Avalonia.Controls.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                StrokeDashArray = new global::Avalonia.Collections.AvaloniaList<double> { 5, 3 },
                Name = "CutOutOverlay",
                IsVisible = false
            };
            Canvas.SetLeft(cutOutOverlay, _startPoint.X);
            Canvas.SetTop(cutOutOverlay, _startPoint.Y);
            cutOutOverlay.Width = 0;
            cutOutOverlay.Height = 0;
            canvas.Children.Add(cutOutOverlay);
            _currentShape = cutOutOverlay;
            return;
        }

        if (vm.ActiveTool == EditorTool.Image)
        {
            _isDrawing = false;
            await HandleImageTool(canvas, point);
            return;
        }

        switch (vm.ActiveTool)
        {
            case EditorTool.Rectangle:
                var rectAnnotation = new RectangleAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, FillColor = vm.FillColor, ShadowEnabled = vm.ShadowEnabled, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) };
                _currentShape = rectAnnotation.CreateVisual();
                break;
            case EditorTool.Ellipse:
                var ellipseAnnotation = new EllipseAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, FillColor = vm.FillColor, ShadowEnabled = vm.ShadowEnabled, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) };
                _currentShape = ellipseAnnotation.CreateVisual();
                break;
            case EditorTool.Line:
                var lineAnnotation = new LineAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, ShadowEnabled = vm.ShadowEnabled, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) };
                _currentShape = lineAnnotation.CreateVisual();
                _currentShape.IsHitTestVisible = false;
                break;
            case EditorTool.Arrow:
                var arrowAnnotation = new ArrowAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, ShadowEnabled = vm.ShadowEnabled, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) };
                _currentShape = arrowAnnotation.CreateVisual();
                _currentShape.IsHitTestVisible = false;
                _selectionController.RegisterArrowEndpoint(_currentShape, _startPoint, _startPoint);
                break;
            case EditorTool.Text:
                HandleTextTool(canvas, brush, vm.StrokeWidth);
                return;
            case EditorTool.Spotlight:
                // Map EffectStrength (0-30) to DarkenOpacity (0-255)
                var opacity = (byte)Math.Clamp(vm.EffectStrength / 30.0 * 255, 0, 255);
                var spotlightAnnotation = new SpotlightAnnotation
                {
                    StartPoint = ToSKPoint(_startPoint),
                    EndPoint = ToSKPoint(_startPoint),
                    CanvasSize = ToSKSize(canvas.Bounds.Size),
                    DarkenOpacity = opacity
                };
                var spotlightControl = spotlightAnnotation.CreateVisual();
                spotlightControl.Width = canvas.Bounds.Width;
                spotlightControl.Height = canvas.Bounds.Height;
                Canvas.SetLeft(spotlightControl, 0);
                Canvas.SetTop(spotlightControl, 0);
                _currentShape = spotlightControl;
                break;
            case EditorTool.Blur:
                _currentShape = new BlurAnnotation { Amount = vm.EffectStrength, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) }.CreateVisual();
                _isCreatingEffect = true;
                break;
            case EditorTool.Pixelate:
                _currentShape = new PixelateAnnotation { Amount = vm.EffectStrength, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) }.CreateVisual();
                _isCreatingEffect = true;
                break;
            case EditorTool.Magnify:
                _currentShape = new MagnifyAnnotation { Amount = vm.EffectStrength, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) }.CreateVisual();
                _isCreatingEffect = true;
                break;
            case EditorTool.Highlight:
                _currentShape = new HighlightAnnotation { StrokeColor = vm.SelectedColor, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) }.CreateVisual();
                _isCreatingEffect = true;
                break;
            case EditorTool.SpeechBalloon:
                var fillColor = vm.FillColor;
                // Smart default: If user selected transparent fill, default to White or Black based on Stroke contrast
                if (string.IsNullOrEmpty(fillColor) || fillColor == "#00000000" || fillColor == "Transparent")
                {
                    fillColor = IsColorLight(vm.SelectedColor) ? "#FF000000" : "#FFFFFFFF";
                }
                var balloonAnnotation = new SpeechBalloonAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, FillColor = fillColor, FontSize = vm.FontSize, ShadowEnabled = vm.ShadowEnabled, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) };
                var balloonControl = balloonAnnotation.CreateVisual();
                balloonControl.Width = 0;
                balloonControl.Height = 0;
                Canvas.SetLeft(balloonControl, _startPoint.X);
                Canvas.SetTop(balloonControl, _startPoint.Y);
                _currentShape = balloonControl;
                break;
            case EditorTool.Step:
                var numberAnnotation = new NumberAnnotation
                {
                    StrokeColor = vm.SelectedColor,
                    StrokeWidth = vm.StrokeWidth,
                    FillColor = vm.FillColor,
                    FontSize = vm.FontSize,
                    ShadowEnabled = vm.ShadowEnabled,
                    StartPoint = ToSKPoint(_startPoint),
                    Number = vm.NumberCounter
                }; ;

                _currentShape = numberAnnotation.CreateVisual();

                // Center the number on the click point using calculated radius
                var numberRadius = numberAnnotation.Radius;
                Canvas.SetLeft(_currentShape, _startPoint.X - numberRadius);
                Canvas.SetTop(_currentShape, _startPoint.Y - numberRadius);

                vm.NumberCounter++;
                _isDrawing = true; // Keep true so released handler can select it (or we explicitly select it)?
                // Legacy said: "Keep _isDrawing true so it goes through OnCanvasPointerReleased for auto-selection"
                break;
            case EditorTool.Freehand:
            case EditorTool.SmartEraser:
                var path = new global::Avalonia.Controls.Shapes.Path
                {
                    Stroke = (vm.ActiveTool == EditorTool.SmartEraser) ? new SolidColorBrush(Color.Parse("#80FF0000")) : brush,
                    StrokeThickness = vm.StrokeWidth,
                    StrokeLineCap = PenLineCap.Round,
                    StrokeJoin = PenLineJoin.Round,
                    UseLayoutRounding = false,
                    IsHitTestVisible = false
                    // Data will be set on move
                };

                if (vm.ShadowEnabled && vm.ActiveTool != EditorTool.SmartEraser)
                {
                    path.Effect = new Avalonia.Media.DropShadowEffect
                    {
                        OffsetX = 3,
                        OffsetY = 3,
                        BlurRadius = 4,
                        Color = Avalonia.Media.Color.FromArgb(128, 0, 0, 0)
                    };
                }

                path.SetValue(Panel.ZIndexProperty, 1);

                if (vm.ActiveTool == EditorTool.SmartEraser)
                {
                    // Restored from ref\EditorView_master.axaml.cs lines 1658-1669
                    // Sample pixel color from rendered canvas (including annotations)
                    var sampledColor = _view.EditorCore.SampleCanvasColor(ToSKPoint(_startPoint));

                    var smartEraser = new SmartEraserAnnotation { StrokeColor = sampledColor ?? "#FFFFFFFF", StrokeWidth = vm.StrokeWidth, Points = new List<SKPoint> { ToSKPoint(_startPoint) } };
                    path.Tag = smartEraser;
                    path.Data = smartEraser.CreateSmoothedGeometry();

                    // If we got a valid color, use it as solid color; otherwise fall back to semi-transparent red
                    if (!string.IsNullOrEmpty(sampledColor))
                    {
                        // Update polyline to use solid sampled color
                        path.Stroke = new SolidColorBrush(Color.Parse(sampledColor));
                    }
                }
                else
                {
                    var freehand = new FreehandAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, ShadowEnabled = vm.ShadowEnabled, Points = new List<SKPoint> { ToSKPoint(_startPoint) } };
                    path.Tag = freehand;
                    path.Data = freehand.CreateSmoothedGeometry();
                }
                _currentShape = path;
                break;
        }

        if (_currentShape != null)
        {
            var currentLeft = Canvas.GetLeft(_currentShape);
            var currentTop = Canvas.GetTop(_currentShape);

            // Check for 0 OR NaN (default can be either depending on platform/version)
            bool isPositionUnset = (currentLeft == 0 || double.IsNaN(currentLeft)) &&
                                   (currentTop == 0 || double.IsNaN(currentTop));

            if (isPositionUnset
                && vm.ActiveTool != EditorTool.Spotlight
                && vm.ActiveTool != EditorTool.SpeechBalloon
                && vm.ActiveTool != EditorTool.Line
                && vm.ActiveTool != EditorTool.Arrow
                && vm.ActiveTool != EditorTool.Freehand
                && vm.ActiveTool != EditorTool.SmartEraser
                && vm.ActiveTool != EditorTool.Step)
            {
                Canvas.SetLeft(_currentShape, _startPoint.X);
                Canvas.SetTop(_currentShape, _startPoint.Y);
            }

            // HighlightAnnotation visuals are inserted before non-effect shapes so they render
            // below vector shapes (arrows, rectangles, text) by default.
            if (_currentShape.Tag is HighlightAnnotation)
            {
                int insertIdx = canvas.Children.Count;
                for (int i = 0; i < canvas.Children.Count; i++)
                {
                    if (canvas.Children[i] is Control child && child.Tag is Annotation ann && ann is not BaseEffectAnnotation)
                    {
                        insertIdx = i;
                        break;
                    }
                }
                canvas.Children.Insert(insertIdx, _currentShape);
            }
            else
            {
                canvas.Children.Add(_currentShape);
            }
            // ISSUE-019 fix: Dead code removed - undo handled by EditorCore
        }
    }

    public void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        _zoomController.OnScrollViewerPointerMoved(_view.FindControl<ScrollViewer>("CanvasScrollViewer"), e);

        var selectionSender = sender ?? _view;
        if (_selectionController.OnPointerMoved(selectionSender, e)) return;

        // Handle active crop handle / move drag
        if (_isDraggingCropHandle)
        {
            var cvs = _view.FindControl<Canvas>("AnnotationCanvas") ?? sender as Canvas;
            if (cvs != null)
            {
                var cropCurrent = e.GetPosition(cvs);
                var newRect = ComputeCropHandleResizedRect(_draggedCropHandleTag!, _cropDragStartPoint, cropCurrent, _cropDragStartRect, cvs.Bounds.Width, cvs.Bounds.Height);
                UpdateCropOverlayBounds(newRect);
            }
            e.Handled = true;
            return;
        }

        if (!_isDrawing || _currentShape == null) return;

        var canvas = _view.FindControl<Canvas>("AnnotationCanvas") ?? sender as Canvas;
        if (canvas == null) return;

        var currentPoint = e.GetPosition(canvas);

        // Clamp current point to canvas bounds for crop and cut-out tools
        var vm = ViewModel;
        if (vm != null && (vm.ActiveTool == EditorTool.Crop || vm.ActiveTool == EditorTool.CutOut))
        {
            // Allow cancelling selection by right-clicking while holding left button
            var props = e.GetCurrentPoint(canvas).Properties;
            if (props.IsRightButtonPressed)
            {
                CancelActiveRegionDrawing(canvas);
                e.Pointer.Capture(null);
                return;
            }

            currentPoint = new Point(
                Math.Max(0, Math.Min(currentPoint.X, canvas.Bounds.Width)),
                Math.Max(0, Math.Min(currentPoint.Y, canvas.Bounds.Height))
            );
        }

        if (vm == null) return;

        if (_currentShape is global::Avalonia.Controls.Shapes.Path freehandPath && (freehandPath.Tag is FreehandAnnotation || freehandPath.Tag is SmartEraserAnnotation))
        {
            var freehand = freehandPath.Tag as FreehandAnnotation;
            if (freehand != null)
            {
                freehand.Points.Add(ToSKPoint(currentPoint));
                freehandPath.Data = freehand.CreateSmoothedGeometry();
                freehandPath.InvalidateVisual();
            }
            return;
        }

        if (_currentShape.Name == "CutOutOverlay")
        {
            // Restored from ref\EditorView_master.axaml.cs lines 2024-2075
            // Calculate deltas from start point
            var deltaX = Math.Abs(currentPoint.X - _startPoint.X);
            var deltaY = Math.Abs(currentPoint.Y - _startPoint.Y);

            const double directionThreshold = 15;

            // ISSUE-014 fix: Always show overlay to provide immediate visual feedback
            _currentShape.IsVisible = true;

            // Determine direction based on current movement
            bool currentIsVertical = deltaX > deltaY;

            // Below threshold: show preview feedback (small indicator at start point)
            if (deltaX < directionThreshold && deltaY < directionThreshold)
            {
                _cutOutDirection = null;

                // Show small preview square at start point (30x30px) to indicate tool is active
                const double previewSize = 30;
                Canvas.SetLeft(_currentShape, _startPoint.X - previewSize / 2);
                Canvas.SetTop(_currentShape, _startPoint.Y - previewSize / 2);
                _currentShape.Width = previewSize;
                _currentShape.Height = previewSize;
                return;
            }

            // Update direction once threshold exceeded (can change if user changes drag direction)
            if (deltaX > directionThreshold || deltaY > directionThreshold)
            {
                _cutOutDirection = currentIsVertical;
            }

            // Show and position the cut-out overlay rectangle in determined direction
            if (_cutOutDirection.HasValue)
            {
                if (_cutOutDirection.Value)
                {
                    // Vertical cut - show full-height rectangle between start and current X
                    var cutLeft = Math.Min(_startPoint.X, currentPoint.X);
                    var cutWidth = Math.Abs(currentPoint.X - _startPoint.X);

                    Canvas.SetLeft(_currentShape, cutLeft);
                    Canvas.SetTop(_currentShape, 0); // Full height from top
                    _currentShape.Width = cutWidth;
                    _currentShape.Height = canvas.Bounds.Height; // Full canvas height
                }
                else
                {
                    // Horizontal cut - show full-width rectangle between start and current Y
                    var cutTop = Math.Min(_startPoint.Y, currentPoint.Y);
                    var cutHeight = Math.Abs(currentPoint.Y - _startPoint.Y);

                    Canvas.SetLeft(_currentShape, 0); // Full width from left
                    Canvas.SetTop(_currentShape, cutTop);
                    _currentShape.Width = canvas.Bounds.Width; // Full canvas width
                    _currentShape.Height = cutHeight;
                }
            }
            return;
        }

        // Standard shape resizing
        var left = Math.Min(_startPoint.X, currentPoint.X);
        var top = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            width = Math.Max(width, height);
            height = width;
            if (currentPoint.X < _startPoint.X) left = _startPoint.X - width;
            if (currentPoint.Y < _startPoint.Y) top = _startPoint.Y - height;
        }

        if (_currentShape is global::Avalonia.Controls.Shapes.Rectangle || _currentShape is global::Avalonia.Controls.Shapes.Ellipse)
        {
            Canvas.SetLeft(_currentShape, left);
            Canvas.SetTop(_currentShape, top);
            _currentShape.Width = width;
            _currentShape.Height = height;

            UpdateEffectVisual(_currentShape, left, top, width, height);

            if (_currentShape.Tag is RectangleAnnotation rectAnn) { rectAnn.StartPoint = ToSKPoint(new Point(left, top)); rectAnn.EndPoint = ToSKPoint(new Point(left + width, top + height)); }
            else if (_currentShape.Tag is EllipseAnnotation ellAnn) { ellAnn.StartPoint = ToSKPoint(new Point(left, top)); ellAnn.EndPoint = ToSKPoint(new Point(left + width, top + height)); }
            // Update bounds for all effect annotations (Blur, Pixelate, Magnify, Highlight)
            else if (_currentShape.Tag is BaseEffectAnnotation effectAnn) { effectAnn.StartPoint = ToSKPoint(new Point(left, top)); effectAnn.EndPoint = ToSKPoint(new Point(left + width, top + height)); }
        }
        else if (_currentShape is global::Avalonia.Controls.Shapes.Line line)
        {
            line.EndPoint = currentPoint;
            if (line.Tag is LineAnnotation lineAnn) lineAnn.EndPoint = ToSKPoint(currentPoint);
        }
        else if (_currentShape is global::Avalonia.Controls.Shapes.Path path) // Arrow
        {
            if (path.Tag is ArrowAnnotation arrowAnn)
            {
                arrowAnn.EndPoint = ToSKPoint(currentPoint);
                AnnotationVisualFactory.UpdateVisualControl(path, arrowAnn);
            }

            _selectionController.RegisterArrowEndpoint(path, _startPoint, currentPoint);
        }
        else if (_currentShape is ShareX.ImageEditor.Controls.SpotlightControl spotlight)
        {
            if (spotlight.Annotation is SpotlightAnnotation spotAnn)
            {
                spotAnn.StartPoint = ToSKPoint(new Point(left, top));
                spotAnn.EndPoint = ToSKPoint(new Point(left + width, top + height));
                spotlight.InvalidateVisual();
            }
        }
        else if (_currentShape is SpeechBalloonControl balloon)
        {
            Canvas.SetLeft(balloon, left);
            Canvas.SetTop(balloon, top);
            balloon.Width = width;
            balloon.Height = height;
            if (balloon.Annotation is SpeechBalloonAnnotation balloonAnn)
            {
                balloonAnn.StartPoint = ToSKPoint(new Point(left, top));
                balloonAnn.EndPoint = ToSKPoint(new Point(left + width, top + height));
            }
            balloon.InvalidateVisual();
        }
    }

    public void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _zoomController.OnScrollViewerPointerReleased(_view.FindControl<ScrollViewer>("CanvasScrollViewer"), e);
        var selectionSender = sender ?? _view;
        if (_selectionController.OnPointerReleased(selectionSender, e)) return;

        // Stop crop handle / move drag
        if (_isDraggingCropHandle)
        {
            _isDraggingCropHandle = false;
            _draggedCropHandleTag = null;
            e.Pointer.Capture(null);
            return;
        }

        if (_isDrawing)
        {
            var canvas = _view.FindControl<Canvas>("AnnotationCanvas") ?? sender as Canvas;
            if (canvas == null) return;

            e.Pointer.Capture(null);
            _isDrawing = false;

            var vm = ViewModel;
            if (vm != null)
            {
                if (vm.ActiveTool == EditorTool.Crop)
                {
                    var cropOverlay = _view.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
                    if (cropOverlay != null && cropOverlay.IsVisible && cropOverlay.Width >= MinShapeSize && cropOverlay.Height >= MinShapeSize)
                    {
                        var overlayCanvas = _view.FindControl<Canvas>("OverlayCanvas");
                        if (overlayCanvas != null)
                        {
                            var cropRect = new Rect(Canvas.GetLeft(cropOverlay), Canvas.GetTop(cropOverlay), cropOverlay.Width, cropOverlay.Height);
                            ShowCropHandles(overlayCanvas, cropRect);
                            _cropActive = true;
                        }
                    }
                    else if (cropOverlay != null)
                    {
                        cropOverlay.IsVisible = false;
                        cropOverlay.Width = 0;
                        cropOverlay.Height = 0;
                    }
                    _currentShape = null;
                    return;
                }
                else if (vm.ActiveTool == EditorTool.CutOut)
                {
                    PerformCutOut(canvas);
                    return;
                }
                else if (_currentShape != null)
                {
                    // Check MinSize for shapes that support size validation
                    // Skip check for Number (single-click), Pen, SmartEraser (freehand stroke)
                    bool isSizeBased = vm.ActiveTool != EditorTool.Step
                                    && vm.ActiveTool != EditorTool.Freehand
                                    && vm.ActiveTool != EditorTool.SmartEraser
                                    && vm.ActiveTool != EditorTool.Text;

                    if (isSizeBased)
                    {
                        // Calculate size based on pointer position difference (most reliable method)
                        var releasePoint = e.GetPosition(canvas);
                        double shapeWidth = Math.Abs(releasePoint.X - _startPoint.X);
                        double shapeHeight = Math.Abs(releasePoint.Y - _startPoint.Y);

                        // Discard shape if too small (prevents accidental clicks creating tiny shapes)
                        if (shapeWidth < MinShapeSize && shapeHeight < MinShapeSize)
                        {
                            canvas.Children.Remove(_currentShape);
                            _currentShape = null;
                            _cachedSkBitmap?.Dispose();
                            _cachedSkBitmap = null;
                            _isCreatingEffect = false;
                            return;
                        }
                    }

                    // Restored from ref\EditorView_master.axaml.cs lines 2211-2238
                    // Auto-select newly created shape so resize handles appear immediately,
                    // but skip selection for freehand pen/eraser strokes (Path) which
                    // are not resizable with our current handle logic.
                    if (!(_currentShape is global::Avalonia.Controls.Shapes.Path && _currentShape.Tag is FreehandAnnotation))
                    {
                        // Apply final effect for effect tools
                        if (_currentShape.Tag is BaseEffectAnnotation)
                        {
                            UpdateEffectVisual(_currentShape,
                                Canvas.GetLeft(_currentShape),
                                Canvas.GetTop(_currentShape),
                                _currentShape.Width,
                                _currentShape.Height);
                        }

                        // Restore hit testing for the finalized shape (was disabled for performance during drawing)
                        _currentShape.IsHitTestVisible = true;

                        _selectionController.SetSelectedShape(_currentShape);
                    }

                    // Ensure annotation is added to Core history
                    if (_currentShape.Tag is Annotation annotation && vm.ActiveTool != EditorTool.Crop && vm.ActiveTool != EditorTool.CutOut)
                    {
                        _view.EditorCore.AddAnnotation(annotation);

                        // Update HasAnnotations state for Clear button
                        vm.HasAnnotations = true;
                    }
                }
            }

            _currentShape = null;
            _cachedSkBitmap?.Dispose();
            _cachedSkBitmap = null;
            _isCreatingEffect = false;
        }
    }

    public void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Keyboard shortcuts are handled elsewhere; no pointer emulation needed here.
    }

    private void CancelActiveRegionDrawing(Canvas canvas)
    {
        if (_cropActive) CancelCrop();
        if (_currentShape is global::Avalonia.Controls.Shapes.Rectangle rect)
        {
            if (rect.Name == "CropOverlay") { rect.IsVisible = false; rect.Width = 0; rect.Height = 0; }
            else if (rect.Name == "CutOutOverlay") { canvas.Children.Remove(rect); }
        }
        _currentShape = null;
        _cutOutDirection = null;
        _isDrawing = false;
    }

    private void UpdateEffectVisual(Control shape, double x, double y, double width, double height)
    {
        // ISSUE-004 fix: Store ViewModel locally to prevent null reference if it changes
        var vm = ViewModel;
        if (!_isCreatingEffect || vm == null) return;
        if (vm.PreviewImage == null || shape.Tag is not BaseEffectAnnotation annotation) return;

        if (_cachedSkBitmap == null)
        {
            _cachedSkBitmap = BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
        }

        if (width <= 0 || height <= 0) return;

        // Canvas coordinates are already in image-pixel space (AnnotationCanvas is
        // sized to bitmap.Width/Height). No DPI scaling needed.
        // Pixel-align bounds so effect bitmap dimensions remain stable while drawing.
        double right = x + width;
        double bottom = y + height;
        double normalizedLeft = Math.Floor(Math.Min(x, right));
        double normalizedTop = Math.Floor(Math.Min(y, bottom));
        double normalizedRight = Math.Ceiling(Math.Max(x, right));
        double normalizedBottom = Math.Ceiling(Math.Max(y, bottom));

        double alignedW = Math.Max(1, normalizedRight - normalizedLeft);
        double alignedH = Math.Max(1, normalizedBottom - normalizedTop);

        annotation.StartPoint = new SKPoint((float)normalizedLeft, (float)normalizedTop);
        annotation.EndPoint = new SKPoint((float)(normalizedLeft + alignedW), (float)(normalizedTop + alignedH));

        try
        {
            annotation.UpdateEffect(_cachedSkBitmap);
            if (annotation.EffectBitmap != null && shape is Shape shapeControl)
            {
                var avaloniaBitmap = BitmapConversionHelpers.ToAvaloniBitmap(annotation.EffectBitmap);
                double bw = annotation.EffectBitmap.Width;
                double bh = annotation.EffectBitmap.Height;
                shapeControl.Fill = new ImageBrush(avaloniaBitmap)
                {
                    Stretch = Stretch.Fill,
                    SourceRect = new RelativeRect(0, 0, bw, bh, RelativeUnit.Absolute)
                };
            }
        }
        catch { }
    }

    /// <summary>
    /// Executes the pending crop operation. Returns true if a crop was confirmed.
    /// </summary>
    public bool TryConfirmCrop()
    {
        if (!_cropActive) return false;
        var overlayCanvas = _view.FindControl<Canvas>("OverlayCanvas");
        if (overlayCanvas != null)
        {
            HideCropHandles(overlayCanvas);
            HideCropAdorners();
        }
        ResetCropDragState();
        _cropActive = false;
        PerformCrop();
        return true;
    }

    /// <summary>
    /// Cancels the pending crop, hiding the overlay and handles. Returns true if a crop was active.
    /// </summary>
    public bool CancelCrop()
    {
        if (!_cropActive) return false;
        var overlayCanvas = _view.FindControl<Canvas>("OverlayCanvas");
        if (overlayCanvas != null)
        {
            HideCropHandles(overlayCanvas);
            HideCropAdorners();
        }
        ResetCropDragState();
        _cropActive = false;
        var cropOverlay = _view.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
        if (cropOverlay != null)
        {
            cropOverlay.IsVisible = false;
            cropOverlay.Width = 0;
            cropOverlay.Height = 0;
        }
        return true;
    }

    /// <summary>
    /// Shows the crop overlay at full image bounds with 8 handles so the user can drag them inwards immediately.
    /// Call when the user selects the Crop tool.
    /// </summary>
    public void ActivateCropToFullImage()
    {
        var canvas = _view.FindControl<Canvas>("AnnotationCanvas");
        var overlayCanvas = _view.FindControl<Canvas>("OverlayCanvas");
        var cropOverlay = _view.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
        if (canvas == null || overlayCanvas == null || cropOverlay == null) return;

        double w = canvas.Bounds.Width;
        double h = canvas.Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var fullRect = new Rect(0, 0, w, h);
        cropOverlay.Fill = Brushes.Transparent;
        cropOverlay.Stroke = Brushes.White;
        cropOverlay.StrokeThickness = 1.5;
        cropOverlay.StrokeDashArray = new global::Avalonia.Collections.AvaloniaList<double>();
        cropOverlay.SetValue(Panel.ZIndexProperty, CropOverlayZIndex);
        cropOverlay.IsVisible = true;
        Canvas.SetLeft(cropOverlay, 0);
        Canvas.SetTop(cropOverlay, 0);
        cropOverlay.Width = w;
        cropOverlay.Height = h;
        _cropActive = true;
        EnsureCropAdorners(overlayCanvas);
        UpdateCropAdorners(overlayCanvas, fullRect);
        ShowCropHandles(overlayCanvas, fullRect);
    }

    private void ShowCropHandles(Canvas overlay, Rect cropRect)
    {
        HideCropHandles(overlay);
        EnsureCropAdorners(overlay);
        UpdateCropAdorners(overlay, cropRect);
        _cropHandles.Add(CreateCropHandle(overlay, cropRect.Left, cropRect.Top, "Crop_TopLeft"));
        _cropHandles.Add(CreateCropHandle(overlay, cropRect.Left + cropRect.Width / 2, cropRect.Top, "Crop_TopCenter"));
        _cropHandles.Add(CreateCropHandle(overlay, cropRect.Right, cropRect.Top, "Crop_TopRight"));
        _cropHandles.Add(CreateCropHandle(overlay, cropRect.Right, cropRect.Top + cropRect.Height / 2, "Crop_RightCenter"));
        _cropHandles.Add(CreateCropHandle(overlay, cropRect.Right, cropRect.Bottom, "Crop_BottomRight"));
        _cropHandles.Add(CreateCropHandle(overlay, cropRect.Left + cropRect.Width / 2, cropRect.Bottom, "Crop_BottomCenter"));
        _cropHandles.Add(CreateCropHandle(overlay, cropRect.Left, cropRect.Bottom, "Crop_BottomLeft"));
        _cropHandles.Add(CreateCropHandle(overlay, cropRect.Left, cropRect.Top + cropRect.Height / 2, "Crop_LeftCenter"));
        overlay.InvalidateArrange();
        overlay.InvalidateVisual();
    }

    private void HideCropHandles(Canvas overlay)
    {
        foreach (var handle in _cropHandles)
            overlay.Children.Remove(handle);
        _cropHandles.Clear();
    }

    private void ResetCropDragState()
    {
        _isDraggingCropHandle = false;
        _draggedCropHandleTag = null;
    }

    // Crop UI layers and dimensions tuned after surveying common editor patterns.
    private const int CropShadeZIndex = 5000;
    private const int CropOverlayZIndex = 6000;
    private const int CropGuideZIndex = 6500;
    private const int CropHandleZIndex = 7000;
    private const double CropHandleCornerHitSize = 32;
    private const double CropHandleEdgeLong = 30;
    private const double CropHandleEdgeShort = 14;
    private const double CropHandleLArmLength = 12;
    private const double CropHandleStrokeThickness = 2.75;
    private const double CropHandleLInset = 2;
    private const double MinCropGuideSize = 24;

    private static readonly Color CropHandleFill = Color.FromRgb(255, 255, 255);
    private static readonly Color CropHandleStroke = Color.FromRgb(20, 20, 20);
    private static readonly Color CropShadeFill = Color.FromArgb(140, 0, 0, 0);
    private static readonly Color CropGuideStroke = Color.FromArgb(210, 255, 255, 255);

    private void EnsureCropAdorners(Canvas overlay)
    {
        if (_cropShadeRects.Count == 0)
        {
            for (int i = 0; i < 4; i++)
            {
                var shade = new Rectangle
                {
                    Fill = new SolidColorBrush(CropShadeFill),
                    Stroke = null,
                    IsHitTestVisible = false,
                    IsVisible = false
                };
                shade.SetValue(Panel.ZIndexProperty, CropShadeZIndex);
                _cropShadeRects.Add(shade);
                overlay.Children.Add(shade);
            }
        }
        else
        {
            foreach (var shade in _cropShadeRects)
            {
                if (shade.Parent != overlay)
                {
                    (shade.Parent as Panel)?.Children.Remove(shade);
                    overlay.Children.Add(shade);
                }
            }
        }

        if (_cropGuideLines.Count == 0)
        {
            for (int i = 0; i < 4; i++)
            {
                var guide = new Line
                {
                    Stroke = new SolidColorBrush(CropGuideStroke),
                    StrokeThickness = 1,
                    StrokeDashArray = new global::Avalonia.Collections.AvaloniaList<double> { 3, 3 },
                    IsHitTestVisible = false,
                    IsVisible = false
                };
                guide.SetValue(Panel.ZIndexProperty, CropGuideZIndex);
                _cropGuideLines.Add(guide);
                overlay.Children.Add(guide);
            }
        }
        else
        {
            foreach (var guide in _cropGuideLines)
            {
                if (guide.Parent != overlay)
                {
                    (guide.Parent as Panel)?.Children.Remove(guide);
                    overlay.Children.Add(guide);
                }
            }
        }
    }

    private void HideCropAdorners()
    {
        foreach (var shade in _cropShadeRects)
        {
            shade.IsVisible = false;
        }

        foreach (var guide in _cropGuideLines)
        {
            guide.IsVisible = false;
        }
    }

    private void UpdateCropAdorners(Canvas overlay, Rect cropRect)
    {
        EnsureCropAdorners(overlay);

        var annotationCanvas = _view.FindControl<Canvas>("AnnotationCanvas");
        double canvasWidth = annotationCanvas?.Bounds.Width ?? overlay.Bounds.Width;
        double canvasHeight = annotationCanvas?.Bounds.Height ?? overlay.Bounds.Height;

        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            HideCropAdorners();
            return;
        }

        double left = ClampSafe(cropRect.Left, 0, canvasWidth);
        double top = ClampSafe(cropRect.Top, 0, canvasHeight);
        double right = ClampSafe(cropRect.Right, 0, canvasWidth);
        double bottom = ClampSafe(cropRect.Bottom, 0, canvasHeight);
        double width = Math.Max(0, right - left);
        double height = Math.Max(0, bottom - top);

        SetCropAdornerRect(_cropShadeRects[0], 0, 0, canvasWidth, top);
        SetCropAdornerRect(_cropShadeRects[1], 0, top, left, height);
        SetCropAdornerRect(_cropShadeRects[2], right, top, Math.Max(0, canvasWidth - right), height);
        SetCropAdornerRect(_cropShadeRects[3], 0, bottom, canvasWidth, Math.Max(0, canvasHeight - bottom));

        bool showGuides = width >= MinCropGuideSize && height >= MinCropGuideSize;
        if (!showGuides)
        {
            foreach (var guide in _cropGuideLines)
            {
                guide.IsVisible = false;
            }
            return;
        }

        double v1 = left + width / 3.0;
        double v2 = left + (2.0 * width / 3.0);
        double h1 = top + height / 3.0;
        double h2 = top + (2.0 * height / 3.0);

        _cropGuideLines[0].StartPoint = new Point(v1, top);
        _cropGuideLines[0].EndPoint = new Point(v1, bottom);
        _cropGuideLines[1].StartPoint = new Point(v2, top);
        _cropGuideLines[1].EndPoint = new Point(v2, bottom);
        _cropGuideLines[2].StartPoint = new Point(left, h1);
        _cropGuideLines[2].EndPoint = new Point(right, h1);
        _cropGuideLines[3].StartPoint = new Point(left, h2);
        _cropGuideLines[3].EndPoint = new Point(right, h2);

        foreach (var guide in _cropGuideLines)
        {
            guide.IsVisible = true;
        }
    }

    private static void SetCropAdornerRect(Rectangle rect, double left, double top, double width, double height)
    {
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        rect.Width = Math.Max(0, width);
        rect.Height = Math.Max(0, height);
        rect.IsVisible = rect.Width > 0 && rect.Height > 0;
    }

    /// <summary>
    /// Computes the anchor offset within a crop handle's hit area so the visual element
    /// (L-vertex for corners, center for edge bars) aligns with the crop rectangle edge/corner.
    /// </summary>
    private static (double X, double Y) GetCropHandleAnchorOffset(string tag)
    {
        const double size = CropHandleCornerHitSize;
        const double inset = CropHandleLInset;

        if (tag.EndsWith("TopLeft", StringComparison.Ordinal))
            return (inset, inset);
        if (tag.EndsWith("TopRight", StringComparison.Ordinal))
            return (size - inset, inset);
        if (tag.EndsWith("BottomRight", StringComparison.Ordinal))
            return (size - inset, size - inset);
        if (tag.EndsWith("BottomLeft", StringComparison.Ordinal))
            return (inset, size - inset);

        // Edge handles: centered on the edge midpoint
        if (tag.Contains("Top") || tag.Contains("Bottom"))
            return (CropHandleEdgeLong / 2.0, CropHandleEdgeShort / 2.0);
        return (CropHandleEdgeShort / 2.0, CropHandleEdgeLong / 2.0);
    }

    private void UpdateCropHandlePositions(Canvas overlay, Rect cropRect)
    {
        // Order: TopLeft, TopCenter, TopRight, RightCenter, BottomRight, BottomCenter, BottomLeft, LeftCenter
        var positions = new (double X, double Y)[]
        {
            (cropRect.Left,                          cropRect.Top),
            (cropRect.Left + cropRect.Width / 2,     cropRect.Top),
            (cropRect.Right,                         cropRect.Top),
            (cropRect.Right,                         cropRect.Top + cropRect.Height / 2),
            (cropRect.Right,                         cropRect.Bottom),
            (cropRect.Left + cropRect.Width / 2,     cropRect.Bottom),
            (cropRect.Left,                          cropRect.Bottom),
            (cropRect.Left,                          cropRect.Top + cropRect.Height / 2),
        };

        for (int i = 0; i < _cropHandles.Count && i < positions.Length; i++)
        {
            var tag = _cropHandles[i].Tag as string ?? "";
            var anchor = GetCropHandleAnchorOffset(tag);
            Canvas.SetLeft(_cropHandles[i], positions[i].X - anchor.X);
            Canvas.SetTop(_cropHandles[i], positions[i].Y - anchor.Y);
        }
    }

    private Border CreateCropHandle(Canvas overlay, double x, double y, string tag)
    {
        Cursor cursor = new Cursor(StandardCursorType.SizeNorthSouth);
        if (tag.Contains("TopLeft") || tag.Contains("BottomRight")) cursor = new Cursor(StandardCursorType.TopLeftCorner);
        else if (tag.Contains("TopRight") || tag.Contains("BottomLeft")) cursor = new Cursor(StandardCursorType.TopRightCorner);
        else if (tag.Contains("Top") || tag.Contains("Bottom")) cursor = new Cursor(StandardCursorType.SizeNorthSouth);
        else if (tag.Contains("Left") || tag.Contains("Right")) cursor = new Cursor(StandardCursorType.SizeWestEast);

        bool isCorner = tag.EndsWith("TopLeft", StringComparison.Ordinal) || tag.EndsWith("TopRight", StringComparison.Ordinal)
            || tag.EndsWith("BottomRight", StringComparison.Ordinal) || tag.EndsWith("BottomLeft", StringComparison.Ordinal);

        double width, height;
        Control visual;
        if (isCorner)
        {
            width = height = CropHandleCornerHitSize;
            visual = CreateCropCornerLShape(tag);
        }
        else
        {
            if (tag.Contains("Top") || tag.Contains("Bottom"))
            { width = CropHandleEdgeLong; height = CropHandleEdgeShort; }
            else
            { width = CropHandleEdgeShort; height = CropHandleEdgeLong; }
            visual = CreateCropEdgeBar(width, height);
        }

        var handle = new Border
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Tag = tag,
            Cursor = cursor,
            Child = visual,
            ClipToBounds = false
        };
        handle.SetValue(Panel.ZIndexProperty, CropHandleZIndex);

        var anchor = GetCropHandleAnchorOffset(tag);
        Canvas.SetLeft(handle, x - anchor.X);
        Canvas.SetTop(handle, y - anchor.Y);
        overlay.Children.Add(handle);
        return handle;
    }

    /// <summary>
    /// Creates an L-shaped corner handle (like Photoshop/GIMP) for visibility and clear resize affordance.
    /// Outline stroke ensures visibility on both light and dark image areas.
    /// </summary>
    private static Control CreateCropCornerLShape(string tag)
    {
        const double size = CropHandleCornerHitSize;
        const double inset = CropHandleLInset;
        const double arm = CropHandleLArmLength;
        const double thickness = 2.5;

        // Base orientation is BottomLeft (classic "L"): horizontal right, vertical up.
        // Other corners are clockwise rotations.
        double cornerX;
        double cornerY;
        double hx;
        double vy;

        if (tag.EndsWith("BottomLeft", StringComparison.Ordinal))
        {
            cornerX = inset;
            cornerY = size - inset;
            hx = arm;
            vy = -arm;
        }
        else if (tag.EndsWith("TopLeft", StringComparison.Ordinal))
        {
            cornerX = inset;
            cornerY = inset;
            hx = arm;
            vy = arm;
        }
        else if (tag.EndsWith("TopRight", StringComparison.Ordinal))
        {
            cornerX = size - inset;
            cornerY = inset;
            hx = -arm;
            vy = arm;
        }
        else
        {
            cornerX = size - inset;
            cornerY = size - inset;
            hx = -arm;
            vy = -arm;
        }

        Line BuildArmLine(double sx, double sy, double ex, double ey, IBrush stroke, double strokeThickness) => new()
        {
            StartPoint = new Point(sx, sy),
            EndPoint = new Point(ex, ey),
            Stroke = stroke,
            StrokeThickness = strokeThickness,
            StrokeLineCap = PenLineCap.Flat,
            IsHitTestVisible = false
        };

        var fillBrush = new SolidColorBrush(CropHandleFill);
        var outlineBrush = new SolidColorBrush(CropHandleStroke);
        double outlineThickness = thickness + 2;

        var panel = new Canvas { Width = size, Height = size, IsHitTestVisible = false };
        panel.Children.Add(BuildArmLine(cornerX, cornerY, cornerX + hx, cornerY, outlineBrush, outlineThickness));
        panel.Children.Add(BuildArmLine(cornerX, cornerY, cornerX, cornerY + vy, outlineBrush, outlineThickness));
        panel.Children.Add(BuildArmLine(cornerX, cornerY, cornerX + hx, cornerY, fillBrush, thickness));
        panel.Children.Add(BuildArmLine(cornerX, cornerY, cornerX, cornerY + vy, fillBrush, thickness));

        return panel;
    }

    /// <summary>
    /// Creates a short bar handle on the edge (top/right/bottom/left center) for single-edge resize.
    /// </summary>
    private static Control CreateCropEdgeBar(double width, double height)
    {
        double rw = Math.Max(2, width - 8);
        double rh = Math.Max(2, height - 8);
        var rect = new Rectangle
        {
            Width = rw,
            Height = rh,
            Fill = new SolidColorBrush(CropHandleFill),
            Stroke = new SolidColorBrush(CropHandleStroke),
            StrokeThickness = 2,
            RadiusX = 2,
            RadiusY = 2,
            IsHitTestVisible = false
        };
        rect.Classes.Add("crop-handle-edge");
        var canvas = new Canvas { Width = width, Height = height, IsHitTestVisible = false };
        Canvas.SetLeft(rect, (width - rw) / 2);
        Canvas.SetTop(rect, (height - rh) / 2);
        canvas.Children.Add(rect);
        return canvas;
    }

    private void UpdateCropOverlayBounds(Rect newRect)
    {
        var cropOverlay = _view.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
        if (cropOverlay == null) return;
        Canvas.SetLeft(cropOverlay, newRect.Left);
        Canvas.SetTop(cropOverlay, newRect.Top);
        cropOverlay.Width = newRect.Width;
        cropOverlay.Height = newRect.Height;
        var overlay = _view.FindControl<Canvas>("OverlayCanvas");
        if (overlay != null)
        {
            UpdateCropHandlePositions(overlay, newRect);
            UpdateCropAdorners(overlay, newRect);
        }
    }

    private const double MinCropSize = 16;

    private static Rect ComputeCropHandleResizedRect(string handleTag, Point dragStart, Point current, Rect originalRect, double canvasW, double canvasH)
    {
        double left = originalRect.Left;
        double top = originalRect.Top;
        double right = originalRect.Right;
        double bottom = originalRect.Bottom;
        double cx = ClampSafe(current.X, 0, canvasW);
        double cy = ClampSafe(current.Y, 0, canvasH);

        switch (handleTag)
        {
            case "Crop_TopLeft":
                left = ClampSafe(cx, 0, right - MinCropSize);
                top = ClampSafe(cy, 0, bottom - MinCropSize);
                break;
            case "Crop_TopCenter":
                top = ClampSafe(cy, 0, bottom - MinCropSize);
                break;
            case "Crop_TopRight":
                right = ClampSafe(cx, left + MinCropSize, canvasW);
                top = ClampSafe(cy, 0, bottom - MinCropSize);
                break;
            case "Crop_RightCenter":
                right = ClampSafe(cx, left + MinCropSize, canvasW);
                break;
            case "Crop_BottomRight":
                right = ClampSafe(cx, left + MinCropSize, canvasW);
                bottom = ClampSafe(cy, top + MinCropSize, canvasH);
                break;
            case "Crop_BottomCenter":
                bottom = ClampSafe(cy, top + MinCropSize, canvasH);
                break;
            case "Crop_BottomLeft":
                left = ClampSafe(cx, 0, right - MinCropSize);
                bottom = ClampSafe(cy, top + MinCropSize, canvasH);
                break;
            case "Crop_LeftCenter":
                left = ClampSafe(cx, 0, right - MinCropSize);
                break;
            case "Crop_Move":
                double deltaX = current.X - dragStart.X;
                double deltaY = current.Y - dragStart.Y;
                double maxLeft = Math.Max(0, canvasW - originalRect.Width);
                double maxTop = Math.Max(0, canvasH - originalRect.Height);
                double newLeft = ClampSafe(originalRect.Left + deltaX, 0, maxLeft);
                double newTop = ClampSafe(originalRect.Top + deltaY, 0, maxTop);
                return new Rect(newLeft, newTop, originalRect.Width, originalRect.Height);
            default:
                return originalRect;
        }

        left = ClampSafe(left, 0, canvasW - MinCropSize);
        top = ClampSafe(top, 0, canvasH - MinCropSize);
        right = ClampSafe(right, left + MinCropSize, canvasW);
        bottom = ClampSafe(bottom, top + MinCropSize, canvasH);

        return new Rect(left, top, Math.Max(MinCropSize, right - left), Math.Max(MinCropSize, bottom - top));
    }

    private static double ClampSafe(double value, double min, double max)
    {
        if (max < min) return min;
        return Math.Clamp(value, min, max);
    }

    private void PerformCrop()
    {
        var cropOverlay = _view.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
        // ISSUE-004 fix: Store ViewModel locally to prevent null reference if it changes
        var vm = ViewModel;
        if (cropOverlay == null || !cropOverlay.IsVisible || vm == null) return;

        var x = Canvas.GetLeft(cropOverlay);
        var y = Canvas.GetTop(cropOverlay);
        var w = cropOverlay.Width;
        var h = cropOverlay.Height;

        if (w <= 0 || h <= 0)
        {
            cropOverlay.IsVisible = false;
            return;
        }

        // Canvas coordinates are already in image-pixel space (AnnotationCanvas is sized
        // to CanvasSize = bitmap.Width/Height). No DPI scaling needed — RenderScaling
        // only affects physical display pixels, not the logical layout coordinate space.
        var cropX = (int)Math.Round(x);
        var cropY = (int)Math.Round(y);
        var cropW = (int)Math.Round(w);
        var cropH = (int)Math.Round(h);

        vm.CropImage(cropX, cropY, cropW, cropH);

        cropOverlay.IsVisible = false;
        _currentShape = null; // Ensure we clear current shape
    }

    private void PerformCutOut(Canvas canvas)
    {
        // ISSUE-004 fix: Store ViewModel locally to prevent null reference if it changes
        var vm = ViewModel;
        if (_currentShape is global::Avalonia.Controls.Shapes.Rectangle cutOverlay && vm != null)
        {
            if (cutOverlay.Width > 0 && cutOverlay.Height > 0 && _cutOutDirection.HasValue)
            {
                var scaling = 1.0;
                var topLevel = TopLevel.GetTopLevel(_view);
                if (topLevel != null) scaling = topLevel.RenderScaling;

                if (_cutOutDirection.Value) // Vertical
                {
                    var left = Canvas.GetLeft(cutOverlay);
                    var w = cutOverlay.Width;
                    int startX = (int)(left * scaling);
                    int endX = (int)((left + w) * scaling);
                    vm.CutOutImage(startX, endX, true);
                }
                else // Horizontal
                {
                    var top = Canvas.GetTop(cutOverlay);
                    var h = cutOverlay.Height;
                    int startY = (int)(top * scaling);
                    int endY = (int)((top + h) * scaling);
                    vm.CutOutImage(startY, endY, false);
                }
            }
            canvas.Children.Remove(cutOverlay);
            _currentShape = null;
        }
    }

    private async Task HandleImageTool(Canvas canvas, Point point)
    {
        var topLevel = TopLevel.GetTopLevel(_view);
        if (topLevel?.StorageProvider != null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Image",
                AllowMultiple = false,
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            });

            if (files.Count > 0)
            {
                using var stream = await files[0].OpenReadAsync();
                var bitmap = new global::Avalonia.Media.Imaging.Bitmap(stream);
                var imageControl = new Image { Source = bitmap, Width = bitmap.Size.Width, Height = bitmap.Size.Height };
                var annotation = new ImageAnnotation();
                annotation.SetImage(BitmapConversionHelpers.ToSKBitmap(bitmap));
                imageControl.Tag = annotation;

                Canvas.SetLeft(imageControl, point.X - bitmap.Size.Width / 2);
                Canvas.SetTop(imageControl, point.Y - bitmap.Size.Height / 2);

                canvas.Children.Add(imageControl);
                // ISSUE-019 fix: Dead code removed - undo handled by EditorCore

                // Add to Core history
                _view.EditorCore.AddAnnotation(annotation);

                // Update HasAnnotations state for Clear button
                if (ViewModel != null) ViewModel.HasAnnotations = true;

                _selectionController.SetSelectedShape(imageControl);
            }
        }
    }

    private void HandleTextTool(Canvas canvas, SolidColorBrush brush, double strokeWidth)
    {
        var vm = ViewModel;
        if (vm == null) return;

        // Use a visible default when current color is transparent (user feedback: text was invisible).
        string strokeColor = vm.SelectedColor;
        if (Avalonia.Media.Color.TryParse(strokeColor, out var parsed) && parsed.A == 0)
        {
            var fallback = vm.Options?.TextColor ?? Avalonia.Media.Color.FromArgb(255, 0, 0, 0);
            strokeColor = $"#{fallback.A:X2}{fallback.R:X2}{fallback.G:X2}{fallback.B:X2}";
        }

        var textAnnotation = new TextAnnotation
        {
            StrokeColor = strokeColor,
            StrokeWidth = (float)strokeWidth,
            FontSize = vm.FontSize,
            ShadowEnabled = vm.ShadowEnabled,
            StartPoint = ToSKPoint(_startPoint),
            EndPoint = ToSKPoint(_startPoint) // Will be updated when text is finalized
        };

        var textBrush = Avalonia.Media.Color.TryParse(strokeColor, out var c) ? new SolidColorBrush(c) : brush;
        var textBox = new TextBox
        {
            Foreground = textBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.White,
            FontSize = vm.FontSize,
            Text = string.Empty,
            Padding = new Thickness(4),
            AcceptsReturn = false,
            Tag = textAnnotation,
            MinWidth = 0
        };

        if (vm.ShadowEnabled)
        {
            textBox.Effect = new Avalonia.Media.DropShadowEffect
            {
                OffsetX = 3,
                OffsetY = 3,
                BlurRadius = 4,
                Color = Avalonia.Media.Color.FromArgb(128, 0, 0, 0)
            };
        }

        Canvas.SetLeft(textBox, _startPoint.X);
        Canvas.SetTop(textBox, _startPoint.Y);

        void OnCreationLostFocus(object? s, global::Avalonia.Interactivity.RoutedEventArgs args)
        {
            if (s is TextBox tb && tb.Tag is TextAnnotation annotation)
            {
                tb.LostFocus -= OnCreationLostFocus;

                tb.BorderThickness = new Thickness(0);
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    if (tb.Parent is Panel panel)
                    {
                        panel.Children.Remove(tb);
                    }
                }
                else
                {
                    // Update annotation with final text and bounds
                    annotation.Text = tb.Text ?? string.Empty;
                    annotation.EndPoint = new SKPoint(
                        (float)(Canvas.GetLeft(tb) + tb.Bounds.Width),
                        (float)(Canvas.GetTop(tb) + tb.Bounds.Height));

                    // Add to EditorCore to enable undo/redo
                    // ISSUE-012 fix: Null check for EditorCore in closure
                    if (_view?.EditorCore != null)
                    {
                        _view.EditorCore.AddAnnotation(annotation);

                        // Update HasAnnotations state for Clear button
                        if (_view?.DataContext is MainViewModel viewModel)
                        {
                            viewModel.HasAnnotations = true;
                        }
                    }

                    // Auto-select the newly created text
                    _selectionController.SetSelectedShape(tb);

                    // Convert to display mode (select-only)
                    tb.IsHitTestVisible = false;

                    // Attach standard LostFocus handler for future edits (via double-click)
                    tb.LostFocus += (sender, e) =>
                    {
                        if (sender is TextBox t) t.IsHitTestVisible = false;
                    };
                }
            }
        }

        textBox.LostFocus += OnCreationLostFocus;

        textBox.KeyDown += (s, args) =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                _view.Focus();
            }
        };

        canvas.Children.Add(textBox);
        textBox.Focus();
        _isDrawing = false;
    }

    private static bool IsColorLight(string colorHex)
    {
        if (Avalonia.Media.Color.TryParse(colorHex, out var color))
        {
            double lum = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            return lum > 0.5;
        }
        return true; // Default to light if parse fails
    }

    private static SKPoint ToSKPoint(Point point) => new((float)point.X, (float)point.Y);
    private static SKSize ToSKSize(Size size) => new((float)size.Width, (float)size.Height);
}
