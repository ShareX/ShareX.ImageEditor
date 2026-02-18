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
            var cropOverlay = _view.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
            if (cropOverlay != null)
            {
                // Clamp start point to canvas bounds
                var clampedX = Math.Max(0, Math.Min(_startPoint.X, canvas.Bounds.Width));
                var clampedY = Math.Max(0, Math.Min(_startPoint.Y, canvas.Bounds.Height));
                _startPoint = new Point(clampedX, clampedY);

                cropOverlay.IsVisible = true;
                Canvas.SetLeft(cropOverlay, _startPoint.X);
                Canvas.SetTop(cropOverlay, _startPoint.Y);
                cropOverlay.Width = 0;
                cropOverlay.Height = 0;
                _currentShape = cropOverlay;
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
                    var sampledColor = await _view.GetPixelColorFromRenderedCanvas(_startPoint);

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

            canvas.Children.Add(_currentShape);
            // ISSUE-019 fix: Dead code removed - undo handled by EditorCore
        }
    }

    public void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        _zoomController.OnScrollViewerPointerMoved(_view.FindControl<ScrollViewer>("CanvasScrollViewer"), e);

        var selectionSender = sender ?? _view;
        if (_selectionController.OnPointerMoved(selectionSender, e)) return;

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
                    PerformCrop();
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

        // ISSUE-008 fix: Apply DPI scaling for high-DPI displays
        var scaling = 1.0;
        var topLevel = TopLevel.GetTopLevel(_view);
        if (topLevel != null) scaling = topLevel.RenderScaling;

        annotation.StartPoint = new SKPoint((float)(x * scaling), (float)(y * scaling));
        annotation.EndPoint = new SKPoint((float)((x + width) * scaling), (float)((y + height) * scaling));

        try
        {
            annotation.UpdateEffect(_cachedSkBitmap);
            if (annotation.EffectBitmap != null && shape is Shape shapeControl)
            {
                var avaloniaBitmap = BitmapConversionHelpers.ToAvaloniBitmap(annotation.EffectBitmap);
                shapeControl.Fill = new ImageBrush(avaloniaBitmap)
                {
                    Stretch = Stretch.None,
                    SourceRect = new RelativeRect(0, 0, width, height, RelativeUnit.Absolute)
                };
            }
        }
        catch { }
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

        var scaling = 1.0;
        var topLevel = TopLevel.GetTopLevel(_view);
        if (topLevel != null) scaling = topLevel.RenderScaling;

        var physX = (int)(x * scaling);
        var physY = (int)(y * scaling);
        var physW = (int)(w * scaling);
        var physH = (int)(h * scaling);

        vm.CropImage(physX, physY, physW, physH);

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

        var textAnnotation = new TextAnnotation
        {
            StrokeColor = vm.SelectedColor,
            StrokeWidth = (float)strokeWidth,
            FontSize = vm.FontSize,
            ShadowEnabled = vm.ShadowEnabled,
            StartPoint = ToSKPoint(_startPoint),
            EndPoint = ToSKPoint(_startPoint) // Will be updated when text is finalized
        };

        var textBox = new TextBox
        {
            Foreground = brush,
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
