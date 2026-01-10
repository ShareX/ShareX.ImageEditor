using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ShareX.Editor.Annotations;
using ShareX.Editor.Controls;
using ShareX.Editor.Helpers;
using ShareX.Editor.ViewModels;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShareX.Editor.Views.Controllers;

public class EditorInputController
{
    private readonly EditorView _view;
    private readonly EditorSelectionController _selectionController;
    private readonly EditorZoomController _zoomController;

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
            vm.StatusText = "Selection cancelled";
            e.Handled = true;
            return;
        }

        if (_selectionController.OnPointerPressed(sender, e))
        {
            return;
        }

        if (props.IsRightButtonPressed)
        {
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
                 if (_selectionController.SelectedShape == hitTarget)
                 {
                     _selectionController.ClearSelection();
                 }
                 
                 canvas.Children.Remove(hitTarget);
                 vm.StatusText = "Shape deleted";
                 e.Handled = true;
                 return;
             }
             return;
        }

        _view.ClearRedoStack();

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
                var rectAnnotation = new RectangleAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) };
                _currentShape = rectAnnotation.CreateVisual();
                break;
            case EditorTool.Ellipse:
                var ellipseAnnotation = new EllipseAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) };
                _currentShape = ellipseAnnotation.CreateVisual();
                break;
            case EditorTool.Line:
                var lineAnnotation = new LineAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) };
                _currentShape = lineAnnotation.CreateVisual();
                break;
            case EditorTool.Arrow:
                var arrowAnnotation = new ArrowAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) };
                _currentShape = arrowAnnotation.CreateVisual();
                _selectionController.RegisterArrowEndpoint(_currentShape, _startPoint, _startPoint);
                break;
            case EditorTool.Text:
                HandleTextTool(canvas, brush, vm.StrokeWidth);
                return;
            case EditorTool.Spotlight:
                var spotlightAnnotation = new SpotlightAnnotation { StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint), CanvasSize = ToSKSize(canvas.Bounds.Size) };
                var spotlightControl = spotlightAnnotation.CreateVisual();
                spotlightControl.Width = canvas.Bounds.Width;
                spotlightControl.Height = canvas.Bounds.Height;
                Canvas.SetLeft(spotlightControl, 0);
                Canvas.SetTop(spotlightControl, 0);
                _currentShape = spotlightControl;
                break;
            case EditorTool.Blur:
                _currentShape = new BlurAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) }.CreateVisual();
                _isCreatingEffect = true;
                break;
            case EditorTool.Pixelate:
                _currentShape = new PixelateAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) }.CreateVisual();
                _isCreatingEffect = true;
                break;
            case EditorTool.Magnify:
                _currentShape = new MagnifyAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) }.CreateVisual();
                _isCreatingEffect = true;
                break;
            case EditorTool.Highlighter:
                _currentShape = new HighlightAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) }.CreateVisual();
                _isCreatingEffect = true;
                break;
            case EditorTool.SpeechBalloon:
                 var balloonAnnotation = new SpeechBalloonAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, FillColor = "#FFFFFFFF", StartPoint = ToSKPoint(_startPoint), EndPoint = ToSKPoint(_startPoint) };
                 var balloonControl = balloonAnnotation.CreateVisual();
                 balloonControl.Width = 0;
                 balloonControl.Height = 0;
                 Canvas.SetLeft(balloonControl, _startPoint.X);
                 Canvas.SetTop(balloonControl, _startPoint.Y);
                 _currentShape = balloonControl;
                 break;
            case EditorTool.Pen:
            case EditorTool.SmartEraser:
                var polyline = new Polyline
                {
                    Stroke = (vm.ActiveTool == EditorTool.SmartEraser) ? new SolidColorBrush(Color.Parse("#80FF0000")) : brush,
                    StrokeThickness = (vm.ActiveTool == EditorTool.SmartEraser) ? 10 : vm.StrokeWidth,
                    Points = new Points { _startPoint }
                };
                polyline.SetValue(Panel.ZIndexProperty, 1);
                
                if (vm.ActiveTool == EditorTool.SmartEraser)
                {
                    var sampledColor = await _view.GetPixelColorFromRenderedCanvas(_startPoint);
                    var smartEraser = new SmartEraserAnnotation { StrokeColor = sampledColor ?? "#FFFFFFFF", StrokeWidth = 10, Points = new List<SKPoint> { ToSKPoint(_startPoint) } };
                    polyline.Tag = smartEraser;
                }
                else
                {
                    var freehand = new FreehandAnnotation { StrokeColor = vm.SelectedColor, StrokeWidth = vm.StrokeWidth, Points = new List<SKPoint> { ToSKPoint(_startPoint) } };
                    polyline.Tag = freehand;
                }
                _currentShape = polyline;
                break;
        }

        if (_currentShape != null)
        {
            if (Canvas.GetLeft(_currentShape) == 0 && Canvas.GetTop(_currentShape) == 0 && vm.ActiveTool != EditorTool.Spotlight && vm.ActiveTool != EditorTool.SpeechBalloon)
            {
                Canvas.SetLeft(_currentShape, _startPoint.X);
                Canvas.SetTop(_currentShape, _startPoint.Y);
            }
            
            canvas.Children.Add(_currentShape);
            _view.PushUndo(_currentShape);
        }
    }

    public void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
         _zoomController.OnScrollViewerPointerMoved(_view.FindControl<ScrollViewer>("CanvasScrollViewer"), e);
        
         if (_selectionController.OnPointerMoved(sender, e)) return;

         if (!_isDrawing || _currentShape == null) return;

         var canvas = _view.FindControl<Canvas>("AnnotationCanvas") ?? sender as Canvas;
         if (canvas == null) return;

         var currentPoint = e.GetPosition(canvas);
         var vm = ViewModel;
         if (vm == null) return;

         if (_currentShape is Polyline polyline)
         {
             polyline.Points.Add(currentPoint);
             if (polyline.Tag is FreehandAnnotation freehand) freehand.Points.Add(ToSKPoint(currentPoint));
             else if (polyline.Tag is SmartEraserAnnotation eraser) eraser.Points.Add(ToSKPoint(currentPoint));
             return;
         }

         if (_currentShape.Name == "CutOutOverlay")
         {
             if (_cutOutDirection == null)
             {
                 if (Math.Abs(currentPoint.X - _startPoint.X) > 5) _cutOutDirection = false; // Horizontal
                 else if (Math.Abs(currentPoint.Y - _startPoint.Y) > 5) _cutOutDirection = true; // Vertical
                 
                 if (_cutOutDirection != null) _currentShape.IsVisible = true;
             }
             else
             {
                 if (_cutOutDirection == true) // Vertical
                 {
                     double cutTop = Math.Min(_startPoint.Y, currentPoint.Y);
                     double cutHeight = Math.Abs(currentPoint.Y - _startPoint.Y);
                     Canvas.SetTop(_currentShape, cutTop);
                     _currentShape.Height = cutHeight;
                     Canvas.SetLeft(_currentShape, 0);
                     _currentShape.Width = canvas.Bounds.Width;
                 }
                 else // Horizontal
                 {
                     double cutLeft = Math.Min(_startPoint.X, currentPoint.X);
                     double cutWidth = Math.Abs(currentPoint.X - _startPoint.X);
                     Canvas.SetLeft(_currentShape, cutLeft);
                     _currentShape.Width = cutWidth;
                     Canvas.SetTop(_currentShape, 0);
                     _currentShape.Height = canvas.Bounds.Height;
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
         }
         else if (_currentShape is global::Avalonia.Controls.Shapes.Line line)
         {
             line.EndPoint = currentPoint;
             if (line.Tag is LineAnnotation lineAnn) lineAnn.EndPoint = ToSKPoint(currentPoint);
         }
         else if (_currentShape is global::Avalonia.Controls.Shapes.Path path) // Arrow
         {
             path.Data = new ArrowAnnotation().CreateArrowGeometry(_startPoint, currentPoint, vm.StrokeWidth * 3);
             
             if (path.Tag is ArrowAnnotation arrowAnn) { arrowAnn.EndPoint = ToSKPoint(currentPoint); }
             _selectionController.RegisterArrowEndpoint(path, _startPoint, currentPoint);
         }
         else if (_currentShape is ShareX.Editor.Controls.SpotlightControl spotlight)
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
        if (_selectionController.OnPointerReleased(sender, e)) return;

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
                }
                else if (vm.ActiveTool == EditorTool.CutOut)
                {
                    PerformCutOut(canvas);
                }
                else if (_currentShape != null)
                {

                     var shapeToSelect = _currentShape;
                     // Use Dispatcher to let the visual tree update (layout pass) before creating handles
                     Dispatcher.UIThread.Post(() => 
                     {
                         _selectionController.SetSelectedShape(shapeToSelect);
                     });
                     
                     // Auto-switch to Select tool for single-shot shapes to allow immediate manipulation
                     if (vm.ActiveTool != EditorTool.Pen && 
                         vm.ActiveTool != EditorTool.SmartEraser && 
                         vm.ActiveTool != EditorTool.Highlighter &&
                         vm.ActiveTool != EditorTool.Number &&
                         vm.ActiveTool != EditorTool.Step)
                     {
                         vm.ActiveTool = EditorTool.Select;
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
         _zoomController.OnScrollViewerPointerPressed(_view.FindControl<ScrollViewer>("CanvasScrollViewer"), null);
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
        if (!_isCreatingEffect || ViewModel == null) return;
        if (ViewModel.PreviewImage == null || shape.Tag is not BaseEffectAnnotation annotation) return;

        if (_cachedSkBitmap == null)
        {
             _cachedSkBitmap = BitmapConversionHelpers.ToSKBitmap(ViewModel.PreviewImage);
        }

        if (width <= 0 || height <= 0) return;

        annotation.StartPoint = new SKPoint((float)x, (float)y);
        annotation.EndPoint = new SKPoint((float)(x + width), (float)(y + height));
        
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
        if (_currentShape is global::Avalonia.Controls.Shapes.Rectangle cropOverlay && ViewModel != null)
        {
             var rect = new SKRect(
                 (float)Canvas.GetLeft(cropOverlay),
                 (float)Canvas.GetTop(cropOverlay),
                 (float)(Canvas.GetLeft(cropOverlay) + cropOverlay.Width),
                 (float)(Canvas.GetTop(cropOverlay) + cropOverlay.Height));
                 
             if (rect.Width > 0 && rect.Height > 0)
             {
                 var scaling = 1.0;
                 var topLevel = TopLevel.GetTopLevel(_view);
                 if (topLevel != null) scaling = topLevel.RenderScaling;

                 var physX = (int)(rect.Left * scaling);
                 var physY = (int)(rect.Top * scaling);
                 var physW = (int)(rect.Width * scaling);
                 var physH = (int)(rect.Height * scaling);

                 ViewModel.CropImage(physX, physY, physW, physH);
                 ViewModel.StatusText = "Image cropped";
             }
             cropOverlay.IsVisible = false;
        }
    }

    private void PerformCutOut(Canvas canvas)
    {
        if (_currentShape is global::Avalonia.Controls.Shapes.Rectangle cutOverlay && ViewModel != null)
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
                     ViewModel.CutOutImage(startX, endX, true);
                 }
                 else // Horizontal
                 {
                     var top = Canvas.GetTop(cutOverlay);
                     var h = cutOverlay.Height;
                     int startY = (int)(top * scaling);
                     int endY = (int)((top + h) * scaling);
                     ViewModel.CutOutImage(startY, endY, false);
                 }
             }
             canvas.Children.Remove(cutOverlay);
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
                _view.PushUndo(imageControl);
                
                _selectionController.SetSelectedShape(imageControl);
            }
        }
    }

    private void HandleTextTool(Canvas canvas, SolidColorBrush brush, double strokeWidth)
    {
        var textBox = new TextBox
        {
            Foreground = brush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.White,
            FontSize = Math.Max(12, strokeWidth * 4),
            Text = string.Empty,
            Padding = new Thickness(4),
            MinWidth = 50,
            AcceptsReturn = false
        };

        Canvas.SetLeft(textBox, _startPoint.X);
        Canvas.SetTop(textBox, _startPoint.Y);

        textBox.LostFocus += (s, args) =>
        {
            if (s is TextBox tb)
            {
                tb.BorderThickness = new Thickness(0);
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    ((Panel)tb.Parent!).Children.Remove(tb);
                }
            }
        };

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
        _view.PushUndo(textBox);
    }
    
    private static SKPoint ToSKPoint(Point point) => new((float)point.X, (float)point.Y);
    private static SKSize ToSKSize(Size size) => new((float)size.Width, (float)size.Height);
}
