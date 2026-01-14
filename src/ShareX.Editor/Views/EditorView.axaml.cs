#nullable enable
#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout; // Added for HorizontalAlignment/VerticalAlignment
using Avalonia.Media;
using ShareX.Editor.Annotations;
using ShareX.Editor.Helpers;
using ShareX.Editor.ViewModels;
using ShareX.Editor.Controls;
using ShareX.Editor.Views.Controllers;
using SkiaSharp;
using System.ComponentModel;
using System.Linq; // Added for Enumerable.Select
using ShareX.Editor.Views.Dialogs;
using ShareX.Editor.ImageEffects;

namespace ShareX.Editor.Views
{
    public partial class EditorView : UserControl
    {
        private readonly EditorZoomController _zoomController;
        private readonly EditorSelectionController _selectionController;
        private readonly EditorInputController _inputController;

        internal EditorCore EditorCore => _editorCore;
        // SIP0018: Hybrid Rendering
        private SKCanvasControl? _canvasControl;
        private readonly EditorCore _editorCore;

        public EditorView()
        {
            InitializeComponent();

            _editorCore = new EditorCore();

            _zoomController = new EditorZoomController(this);
            _selectionController = new EditorSelectionController(this);
            _inputController = new EditorInputController(this, _selectionController, _zoomController);

            // Subscribe to selection controller events
            _selectionController.RequestUpdateEffect += OnRequestUpdateEffect;

            // SIP0018: Subscribe to Core events
            _editorCore.InvalidateRequested += () => Avalonia.Threading.Dispatcher.UIThread.Post(RenderCore);
            _editorCore.ImageChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                if (_canvasControl != null)
                {
                    _canvasControl.Initialize((int)_editorCore.CanvasSize.Width, (int)_editorCore.CanvasSize.Height);
                    RenderCore();
                    if (DataContext is MainViewModel vm)
                    {
                        UpdateViewModelHistoryState(vm);
                        UpdateViewModelMetadata(vm);
                    }
                }
            });
            _editorCore.AnnotationsRestored += () => Avalonia.Threading.Dispatcher.UIThread.Post(OnAnnotationsRestored);
            _editorCore.HistoryChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                if (DataContext is MainViewModel vm) UpdateViewModelHistoryState(vm);
            });

            // Capture wheel events in tunneling phase so ScrollViewer doesn't scroll when using Ctrl+wheel zoom.
            AddHandler(PointerWheelChangedEvent, OnPreviewPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        }
        
        private void UpdateViewModelHistoryState(MainViewModel vm)
        {
            vm.UpdateCoreHistoryState(_editorCore.CanUndo, _editorCore.CanRedo);
        }
        

        private void UpdateViewModelMetadata(MainViewModel vm)
        {
            // Initial sync of metadata if needed
            UpdateViewModelHistoryState(vm);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (DataContext is MainViewModel vm)
            {
                vm.UndoRequested += (s, args) => PerformUndo();
                vm.RedoRequested += (s, args) => PerformRedo();
                vm.DeleteRequested += (s, args) => PerformDelete();
                vm.ClearAnnotationsRequested += (s, args) => ClearAllAnnotations();
                vm.SnapshotRequested += () =>
                {
                    var skBitmap = GetSnapshot();
                    var snapshot = skBitmap != null ? BitmapConversionHelpers.ToAvaloniBitmap(skBitmap) : null;
                    return Task.FromResult<Avalonia.Media.Imaging.Bitmap?>(snapshot);
                };

                // Original code subscribed to vm.PropertyChanged
                vm.PropertyChanged += OnViewModelPropertyChanged;

                // Initialize zoom
                _zoomController.InitLastZoom(vm.Zoom);
                
                // Initial load
                if (vm.PreviewImage != null)
                {
                    LoadImageFromViewModel(vm);
                }
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _selectionController.RequestUpdateEffect -= OnRequestUpdateEffect;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is MainViewModel vm)
            {
                if (e.PropertyName == nameof(MainViewModel.SelectedColor))
                {
                    ApplySelectedColor(vm.SelectedColor);
                }
                else if (e.PropertyName == nameof(MainViewModel.StrokeWidth))
                {
                    ApplySelectedStrokeWidth(vm.StrokeWidth);
                }
                else if (e.PropertyName == nameof(MainViewModel.PreviewImage))
                {
                    _zoomController.ResetScrollViewerOffset();
                    LoadImageFromViewModel(vm);
                }
                else if (e.PropertyName == nameof(MainViewModel.Zoom))
                {
                    _zoomController.HandleZoomPropertyChanged(vm);
                }
                else if (e.PropertyName == nameof(MainViewModel.IsRotateCustomAngleDialogOpen))
                {
                    if (vm.IsRotateCustomAngleDialogOpen)
                    {
                        var dialog = new RotateCustomAngleDialog();
                        vm.ModalContent = dialog;
                        vm.IsModalOpen = true;
                    }
                }
                else if (e.PropertyName == nameof(MainViewModel.ActiveTool))
                {
                    _selectionController.ClearSelection();
                }
            }
        }

        // --- Public/Internal Methods for Controllers ---

        internal void PushUndo(Control shape)
        {
            // Legacy support: Handled by EditorCore history
        }

        internal void ClearRedoStack()
        {
            // Legacy support: Handled by EditorCore history
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            _canvasControl = this.FindControl<SKCanvasControl>("CanvasControl");
        }

        private void LoadImageFromViewModel(MainViewModel vm)
        {
            if (vm.PreviewImage == null || _canvasControl == null) return;

            // One-time conversion from Avalonia Bitmap to SKBitmap for the Core
            // In a full refactor, VM would hold SKBitmap source of truth
            using var skBitmap = BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
            if (skBitmap != null)
            {
                // We must copy because ToSKBitmap might return a disposable wrapper or we need ownership
                _editorCore.LoadImage(skBitmap.Copy());
                
                _canvasControl.Initialize(skBitmap.Width, skBitmap.Height);
                RenderCore();
            }
        }

        private void RenderCore()
        {
            if (_canvasControl == null) return;
            // Hybrid rendering: Render only background + raster effects from Core
            // Vector annotations are handled by Avalonia Canvas
            _canvasControl.Draw(canvas => _editorCore.Render(canvas, false));
        }

        /// <summary>
        /// Sample pixel color from the rendered canvas (including annotations) at the specified canvas coordinates
        /// </summary>
        internal async System.Threading.Tasks.Task<string?> GetPixelColorFromRenderedCanvas(Point canvasPoint)
        {
            if (DataContext is not MainViewModel vm || vm.PreviewImage == null) return null;

            try
            {
                var container = this.FindControl<Grid>("CanvasContainer");
                if (container == null || container.Width <= 0 || container.Height <= 0) return null;

                var rtb = new global::Avalonia.Media.Imaging.RenderTargetBitmap(
                    new PixelSize((int)container.Width, (int)container.Height),
                    new Vector(96, 96));

                rtb.Render(container);

                using var skBitmap = BitmapConversionHelpers.ToSKBitmap(rtb);

                int x = (int)Math.Round(canvasPoint.X);
                int y = (int)Math.Round(canvasPoint.Y);

                if (x < 0 || y < 0 || x >= skBitmap.Width || y >= skBitmap.Height)
                    return null;

                var skColor = skBitmap.GetPixel(x, y);
                return $"#{skColor.Red:X2}{skColor.Green:X2}{skColor.Blue:X2}";
            }
            catch
            {
                return null;
            }
        }

        // --- Event Handlers Delegated to Controllers ---

        private void OnPreviewPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            _zoomController.OnPreviewPointerWheelChanged(sender, e);
        }

        private void OnScrollViewerPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _zoomController.OnScrollViewerPointerPressed(sender, e);
        }

        private void OnScrollViewerPointerMoved(object? sender, PointerEventArgs e)
        {
            _zoomController.OnScrollViewerPointerMoved(sender, e);
        }

        private void OnScrollViewerPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _zoomController.OnScrollViewerPointerReleased(sender, e);
        }

        private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _inputController.OnCanvasPointerPressed(sender, e);
        }

        private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
        {
            _inputController.OnCanvasPointerMoved(sender, e);
        }

        private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _inputController.OnCanvasPointerReleased(sender, e);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Source is TextBox) return;

            if (DataContext is MainViewModel vm)
            {
                if (e.Key == Key.Delete)
                {
                    vm.DeleteSelectedCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    if (e.Key == Key.Z)
                    {
                        vm.UndoCommand.Execute(null);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Y)
                    {
                        vm.RedoCommand.Execute(null);
                        e.Handled = true;
                    }
                }
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z)
                {
                    vm.RedoCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.KeyModifiers == KeyModifiers.None)
                {
                    // Tool shortcuts
                    switch (e.Key)
                    {
                        case Key.V: vm.SelectToolCommand.Execute(EditorTool.Select); e.Handled = true; break;
                        case Key.R: vm.SelectToolCommand.Execute(EditorTool.Rectangle); e.Handled = true; break;
                        case Key.E: vm.SelectToolCommand.Execute(EditorTool.Ellipse); e.Handled = true; break;
                        case Key.A: vm.SelectToolCommand.Execute(EditorTool.Arrow); e.Handled = true; break;
                        case Key.L: vm.SelectToolCommand.Execute(EditorTool.Line); e.Handled = true; break;
                        case Key.T: vm.SelectToolCommand.Execute(EditorTool.Text); e.Handled = true; break;
                        case Key.S: vm.SelectToolCommand.Execute(EditorTool.Spotlight); e.Handled = true; break;
                        case Key.B: vm.SelectToolCommand.Execute(EditorTool.Blur); e.Handled = true; break;
                        case Key.P: vm.SelectToolCommand.Execute(EditorTool.Pixelate); e.Handled = true; break;
                        case Key.I: vm.SelectToolCommand.Execute(EditorTool.Image); e.Handled = true; break;
                        case Key.F: vm.SelectToolCommand.Execute(EditorTool.Pen); e.Handled = true; break; // Freehand
                        case Key.H: vm.SelectToolCommand.Execute(EditorTool.Highlighter); e.Handled = true; break;
                        case Key.M: vm.SelectToolCommand.Execute(EditorTool.Magnify); e.Handled = true; break;
                        case Key.C: vm.SelectToolCommand.Execute(EditorTool.Crop); e.Handled = true; break;
                    }
                }
            }
        }

        // --- Private Helpers (Undo/Redo, Delete, etc that involve view state) ---

        private void PerformUndo()
        {
            if (_editorCore.CanUndo)
            {
                _editorCore.Undo();
                // AnnotationsRestored event will handle UI sync
            }
        }

        private void PerformRedo()
        {
            if (_editorCore.CanRedo)
            {
                _editorCore.Redo();
            }
        }

        private void OnAnnotationsRestored()
        {
            // Fully rebuild annotation layer from Core state
            // 1. Clear current UI annotations
            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas == null) return;
            
            canvas.Children.Clear();
            _selectionController.ClearSelection();

            // 2. Re-create UI for all vector annotations in Core
            foreach (var annotation in _editorCore.Annotations)
            {
                // Only create UI for vector annotations (Hybrid model)
                Control? shape = CreateControlForAnnotation(annotation);
                if (shape != null)
                {
                    canvas.Children.Add(shape);
                }
            }
            
            RenderCore();
        }

        private Control? CreateControlForAnnotation(Annotation annotation)
        {
             // Factory for restoring vector visuals
             if (annotation is RectangleAnnotation rect) {
                var r = new global::Avalonia.Controls.Shapes.Rectangle { 
                    Stroke = new SolidColorBrush(Color.Parse(rect.StrokeColor)),
                    StrokeThickness = rect.StrokeWidth,
                    Tag = rect
                };
                Canvas.SetLeft(r, rect.GetBounds().Left);
                Canvas.SetTop(r, rect.GetBounds().Top);
                r.Width = rect.GetBounds().Width;
                r.Height = rect.GetBounds().Height;
                return r;
             }
             else if (annotation is EllipseAnnotation ellipse) {
                var e = new global::Avalonia.Controls.Shapes.Ellipse {
                    Stroke = new SolidColorBrush(Color.Parse(ellipse.StrokeColor)),
                    StrokeThickness = ellipse.StrokeWidth,
                    Tag = ellipse
                };
                Canvas.SetLeft(e, ellipse.GetBounds().Left);
                Canvas.SetTop(e, ellipse.GetBounds().Top);
                e.Width = ellipse.GetBounds().Width;
                e.Height = ellipse.GetBounds().Height;
                return e;
             }
             else if (annotation is LineAnnotation line) {
                var l = new global::Avalonia.Controls.Shapes.Line {
                    StartPoint = new Point(line.StartPoint.X, line.StartPoint.Y),
                    EndPoint = new Point(line.EndPoint.X, line.EndPoint.Y),
                    Stroke = new SolidColorBrush(Color.Parse(line.StrokeColor)),
                    StrokeThickness = line.StrokeWidth,
                    Tag = line
                };
                return l;
             }
             else if (annotation is ArrowAnnotation arrow) {
                var path = new global::Avalonia.Controls.Shapes.Path {
                    Fill = new SolidColorBrush(Color.Parse(arrow.StrokeColor)),
                    Stroke = new SolidColorBrush(Color.Parse(arrow.StrokeColor)),
                    StrokeThickness = 1, // Arrow handles thickness in geometry
                    Tag = arrow
                };
                path.Data = arrow.CreateArrowGeometry(new Point(arrow.StartPoint.X, arrow.StartPoint.Y), new Point(arrow.EndPoint.X, arrow.EndPoint.Y), arrow.StrokeWidth * 3);
                return path;
             }
             else if (annotation is TextAnnotation text) {
                // For text, we might need a TextBox with IsReadOnly or similar
                // For now, restoring as a TextBox
                var tb = new TextBox {
                    Text = text.Text,
                    Foreground = new SolidColorBrush(Color.Parse(text.StrokeColor)),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = Math.Max(12, text.StrokeWidth * 4),
                    Padding = new Thickness(4),
                    Tag = text
                };
                Canvas.SetLeft(tb, text.StartPoint.X);
                Canvas.SetTop(tb, text.StartPoint.Y);
                return tb;
             }
             else if (annotation is SpotlightAnnotation spotlight) {
                var s = new SpotlightControl { Annotation = spotlight, Tag = spotlight };
                Canvas.SetLeft(s, 0);
                Canvas.SetTop(s, 0);
                s.Width = spotlight.CanvasSize.Width;
                s.Height = spotlight.CanvasSize.Height;
                return s;
             }
             // Effect annotations (Blur, Pixelate, Magnify, Highlight)
            else if (annotation is BaseEffectAnnotation effect) {
                 Control? factorControl = null;
                 if (annotation is BlurAnnotation blur) factorControl = new global::Avalonia.Controls.Shapes.Rectangle { Tag = blur };
                 else if (annotation is PixelateAnnotation pix) factorControl = new global::Avalonia.Controls.Shapes.Rectangle { Tag = pix };
                 else if (annotation is MagnifyAnnotation mag) factorControl = new global::Avalonia.Controls.Shapes.Rectangle { Tag = mag };
                 else if (annotation is HighlightAnnotation high) factorControl = new global::Avalonia.Controls.Shapes.Rectangle { Tag = high };
                 
                 if (factorControl is Shape s) {
                    s.Stroke = new SolidColorBrush(Color.Parse(annotation.StrokeColor));
                    s.StrokeThickness = annotation.StrokeWidth;
                    
                    if (annotation is HighlightAnnotation) {
                        s.Stroke = Brushes.Transparent;
                        // Fill will be handled by UpdateEffectVisual or similar
                        // Note: Effect creation usually requires calling logic to render effect from bitmap
                        // We might need to trigger OnRequestUpdateEffect here
                        // For now we create the control, and expect UpdateEffect to run if we call it
                    }
                 }
                 
                 if (factorControl != null) {
                    Canvas.SetLeft(factorControl, effect.GetBounds().Left);
                    Canvas.SetTop(factorControl, effect.GetBounds().Top);
                    factorControl.Width = effect.GetBounds().Width;
                    factorControl.Height = effect.GetBounds().Height;
                    
                    // Trigger visual update
                    // We can defer this or call explicit update
                    OnRequestUpdateEffect(factorControl);
                    return factorControl;
                 }
             }
             else if (annotation is SpeechBalloonAnnotation balloon) {
                var b = new SpeechBalloonControl { Annotation = balloon, Tag = balloon };
                Canvas.SetLeft(b, balloon.GetBounds().Left);
                Canvas.SetTop(b, balloon.GetBounds().Top);
                b.Width = balloon.GetBounds().Width;
                b.Height = balloon.GetBounds().Height;
                return b;
             }
             else if (annotation is NumberAnnotation number) {
                var brush = new SolidColorBrush(Color.Parse(number.StrokeColor));
                var grid = new Grid
                {
                    Width = number.Radius * 2,
                    Height = number.Radius * 2,
                    Tag = number
                };

                var bg = new Avalonia.Controls.Shapes.Ellipse
                {
                    Fill = brush,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };

                var numText = new TextBlock
                {
                    Text = number.Number.ToString(),
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeight.Bold,
                    FontSize = number.FontSize / 2
                };

                grid.Children.Add(bg);
                grid.Children.Add(numText);

                Canvas.SetLeft(grid, number.StartPoint.X - 15);
                Canvas.SetTop(grid, number.StartPoint.Y - 15);
                return grid;
             }
             else if (annotation is ImageAnnotation imgAnn) {
                 var img = new Image { Tag = imgAnn };
                 if (imgAnn.ImageBitmap != null) {
                    img.Source = BitmapConversionHelpers.ToAvaloniBitmap(imgAnn.ImageBitmap);
                    img.Width = imgAnn.ImageBitmap.Width;
                    img.Height = imgAnn.ImageBitmap.Height;
                 }
                 Canvas.SetLeft(img, imgAnn.StartPoint.X);
                 Canvas.SetTop(img, imgAnn.StartPoint.Y);
                 return img;
             }
             else if (annotation is FreehandAnnotation freehand) {
                 var polyline = new Polyline {
                    Stroke = new SolidColorBrush(Color.Parse(freehand.StrokeColor)),
                    StrokeThickness = freehand.StrokeWidth,
                    Points = new Points(freehand.Points.Select(p => new Point(p.X, p.Y))),
                    Tag = freehand
                 };
                 return polyline;
             }
             else if (annotation is SmartEraserAnnotation eraser) {
                 var polyline = new Polyline {
                    Stroke = new SolidColorBrush(Color.Parse(eraser.StrokeColor)),
                    StrokeThickness = 10, // hardcoded in input logic
                    Points = new Points(eraser.Points.Select(p => new Point(p.X, p.Y))),
                    Tag = eraser
                 };
                 return polyline;
             }
             
             return null; 
        }

        private Color SKColorToAvalonia(SKColor color)
        {
            return Color.FromUInt32((uint)color);
        }

        private void PerformDelete()
        {
            var selected = _selectionController.SelectedShape;
            if (selected != null)
            {
                var canvas = this.FindControl<Canvas>("AnnotationCanvas");
                if (canvas != null && canvas.Children.Contains(selected))
                {
                    canvas.Children.Remove(selected);

                    // Remove from view - Undo not fully supported for delete in view layer for now
                    // as it requires recreating the proper shape from history which is handled by core sync
                    _selectionController.ClearSelection();
                }
            }
        }

        private void ClearAllAnnotations()
        {
            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas != null)
            {
                canvas.Children.Clear();
                _selectionController.ClearSelection();
                _editorCore.ClearAll();
                RenderCore();
            }
        }

        private SkiaSharp.SKBitmap? GetSnapshot()
        {
            // Snapshot logic
            var container = this.FindControl<Grid>("CanvasContainer");
            if (container == null || container.Width <= 0 || container.Height <= 0) return null;

            var rtb = new global::Avalonia.Media.Imaging.RenderTargetBitmap(
                new PixelSize((int)container.Width, (int)container.Height),
                new Vector(96, 96));

            rtb.Render(container);
            return BitmapConversionHelpers.ToSKBitmap(rtb);
        }

        // This is called by SelectionController/InputController via event when an effect logic needs update
        // We replicate the UpdateEffectVisual logic here or expose it
        private void OnRequestUpdateEffect(Control shape)
        {
            if (shape == null || shape.Tag is not BaseEffectAnnotation annotation) return;
            if (DataContext is not MainViewModel vm || vm.PreviewImage == null) return;

            // Logic to update effect bitmap
            try
            {
                double left = Canvas.GetLeft(shape);
                double top = Canvas.GetTop(shape);
                double width = shape.Bounds.Width;
                double height = shape.Bounds.Height;
                if (width <= 0 || height <= 0) return;

                // Map to SKPoint
                annotation.StartPoint = new SKPoint((float)left, (float)top);
                annotation.EndPoint = new SKPoint((float)(left + width), (float)(top + height));

                // We don't have the cached bitmap here, create fresh or pass from controller?
                // Original logic cached it. InputController caches it.
                // This handler is for "OnPointerReleased" from SelectionController (dragging an existing effect).
                // SelectionController doesn't have the cached bitmap.
                using var skBitmap = BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
                annotation.UpdateEffect(skBitmap);

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

        private void OnColorChanged(object? sender, IBrush color)
        {
            if (DataContext is MainViewModel vm && color is SolidColorBrush solidBrush)
            {
                var hexColor = $"#{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                vm.SetColorCommand.Execute(hexColor);
            }
        }

        public void PerformCrop()
        {
            var cropOverlay = this.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
            if (cropOverlay != null && cropOverlay.IsVisible && DataContext is MainViewModel vm)
            {
                var rect = new SkiaSharp.SKRect(
                    (float)Canvas.GetLeft(cropOverlay),
                    (float)Canvas.GetTop(cropOverlay),
                    (float)(Canvas.GetLeft(cropOverlay) + cropOverlay.Width),
                    (float)(Canvas.GetTop(cropOverlay) + cropOverlay.Height));

                if (rect.Width > 0 && rect.Height > 0)
                {
                    var scaling = 1.0;
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel != null) scaling = topLevel.RenderScaling;

                    var physX = (int)(rect.Left * scaling);
                    var physY = (int)(rect.Top * scaling);
                    var physW = (int)(rect.Width * scaling);
                    var physH = (int)(rect.Height * scaling);

                    vm.CropImage(physX, physY, physW, physH);
                    vm.StatusText = "Image cropped";
                }
                cropOverlay.IsVisible = false;
            }
        }

        private void OnWidthChanged(object? sender, int width)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetStrokeWidthCommand.Execute(width);
            }
        }

        private void OnZoomChanged(object? sender, double zoom)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Zoom = zoom;
            }
        }

        private void OnSidebarScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            // TODO: Restore sidebar scrollbar overlay logic
        }

        private void OnToolbarScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            // TODO: Restore toolbar scrollbar overlay logic
        }

        // --- Restored from ref\3babd33_EditorView.axaml.cs lines 767-829 ---

        private void ApplySelectedColor(string colorHex)
        {
            var selected = _selectionController.SelectedShape;
            if (selected == null) return;

            var brush = new SolidColorBrush(Color.Parse(colorHex));

            switch (selected)
            {
                case Shape shape:
                    if (shape.Tag is HighlightAnnotation)
                    {
                        var highlightColor = Color.Parse(colorHex);
                        shape.Stroke = Brushes.Transparent;
                        shape.Fill = new SolidColorBrush(ApplyHighlightAlpha(highlightColor));
                        break;
                    }

                    shape.Stroke = brush;

                    if (shape is global::Avalonia.Controls.Shapes.Path path)
                    {
                        path.Fill = brush;
                    }
                    break;
                case TextBox textBox:
                    textBox.Foreground = brush;
                    break;
                case Grid grid:
                    foreach (var child in grid.Children)
                    {
                        if (child is Ellipse ellipse)
                        {
                            ellipse.Fill = brush;
                        }
                    }
                    break;
            }
        }

        private void ApplySelectedStrokeWidth(int width)
        {
            var selected = _selectionController.SelectedShape;
            if (selected == null) return;

            switch (selected)
            {
                case Shape shape:
                    shape.StrokeThickness = width;
                    break;
                case TextBox textBox:
                    textBox.FontSize = Math.Max(12, width * 4);
                    textBox.BorderThickness = new Thickness(Math.Max(1, width / 2));
                    break;
                case Grid grid:
                    foreach (var child in grid.Children)
                    {
                        if (child is Ellipse ellipse)
                        {
                            ellipse.StrokeThickness = Math.Max(1, width);
                        }
                    }
                    break;
            }
        }

        private static Color ApplyHighlightAlpha(Color baseColor)
        {
            return Color.FromArgb(0x55, baseColor.R, baseColor.G, baseColor.B);
        }

        // --- Edit Menu Event Handlers ---

        private void OnResizeImageRequested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.PreviewImage != null)
            {
                var dialog = new ResizeImageDialog();
                dialog.Initialize((int)vm.ImageWidth, (int)vm.ImageHeight);
                
                dialog.ApplyRequested += (s, args) =>
                {
                    vm.ResizeImage(args.NewWidth, args.NewHeight, args.Quality);
                    vm.CloseModalCommand.Execute(null);
                };
                
                dialog.CancelRequested += (s, args) =>
                {
                    vm.CloseModalCommand.Execute(null);
                };
                
                vm.ModalContent = dialog;
                vm.IsModalOpen = true;
            }
        }

        private void OnResizeCanvasRequested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.PreviewImage != null)
            {
                var dialog = new ResizeCanvasDialog();
                // Get edge color from image for "Match image edge" option
                SKColor? edgeColor = null;
                try
                {
                    using var skBitmap = BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
                    if (skBitmap != null)
                    {
                        edgeColor = skBitmap.GetPixel(0, 0);
                    }
                }
                catch { }
                
                dialog.Initialize(edgeColor);
                
                dialog.ApplyRequested += (s, args) =>
                {
                    vm.ResizeCanvas(args.Top, args.Right, args.Bottom, args.Left, args.BackgroundColor);
                    vm.CloseModalCommand.Execute(null);
                };
                
                dialog.CancelRequested += (s, args) =>
                {
                    vm.CloseModalCommand.Execute(null);
                };
                
                vm.ModalContent = dialog;
                vm.IsModalOpen = true;
            }
        }

        private void OnCropImageRequested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.PreviewImage != null)
            {
                var dialog = new CropImageDialog();
                dialog.Initialize((int)vm.ImageWidth, (int)vm.ImageHeight);
                
                dialog.ApplyRequested += (s, args) =>
                {
                    vm.CropImage(args.X, args.Y, args.Width, args.Height);
                    vm.CloseModalCommand.Execute(null);
                };
                
                dialog.CancelRequested += (s, args) =>
                {
                    vm.CloseModalCommand.Execute(null);
                };
                
                vm.ModalContent = dialog;
                vm.IsModalOpen = true;
            }
        }

        private void OnAutoCropImageRequested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.AutoCropImageCommand.Execute(null);
            }
        }

        private void OnRotate90CWRequested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Rotate90ClockwiseCommand.Execute(null);
            }
        }

        private void OnRotate90CCWRequested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Rotate90CounterClockwiseCommand.Execute(null);
            }
        }

        private void OnRotate180Requested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Rotate180Command.Execute(null);
            }
        }

        private void OnRotateCustomAngleRequested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.OpenRotateCustomAngleDialogCommand.Execute(null);
            }
        }

        private void OnFlipHorizontalRequested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.FlipHorizontalCommand.Execute(null);
            }
        }

        private void OnFlipVerticalRequested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.FlipVerticalCommand.Execute(null);
            }
        }

        // --- Effects Menu Handlers ---

        // --- Effects Menu Handlers ---

        private void OnBrightnessRequested(object? sender, EventArgs e) => ShowEffectDialog(new BrightnessDialog());
        private void OnContrastRequested(object? sender, EventArgs e) => ShowEffectDialog(new ContrastDialog());
        private void OnHueRequested(object? sender, EventArgs e) => ShowEffectDialog(new HueDialog());
        private void OnSaturationRequested(object? sender, EventArgs e) => ShowEffectDialog(new SaturationDialog());
        private void OnGammaRequested(object? sender, EventArgs e) => ShowEffectDialog(new GammaDialog());
        private void OnAlphaRequested(object? sender, EventArgs e) => ShowEffectDialog(new AlphaDialog());
        private void OnColorizeRequested(object? sender, EventArgs e) => ShowEffectDialog(new ColorizeDialog());
        private void OnSelectiveColorRequested(object? sender, EventArgs e) => ShowEffectDialog(new SelectiveColorDialog());
        private void OnReplaceColorRequested(object? sender, EventArgs e) => ShowEffectDialog(new ReplaceColorDialog());
        private void OnGrayscaleRequested(object? sender, EventArgs e) => ShowEffectDialog(new GrayscaleDialog());

        private void OnInvertRequested(object? sender, EventArgs e) { if (DataContext is MainViewModel vm) vm.InvertColorsCommand.Execute(null); }
        private void OnBlackAndWhiteRequested(object? sender, EventArgs e) { if (DataContext is MainViewModel vm) vm.BlackAndWhiteCommand.Execute(null); }
        private void OnSepiaRequested(object? sender, EventArgs e) { if (DataContext is MainViewModel vm) vm.SepiaCommand.Execute(null); }
        private void OnPolaroidRequested(object? sender, EventArgs e) { if (DataContext is MainViewModel vm) vm.PolaroidCommand.Execute(null); }


        private void ShowEffectDialog<T>(T dialog) where T : UserControl
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            // Initialize logic
            vm.StartEffectPreview();

            // Reflection-based event wiring since we didn't use an interface
            // Just kidding, let's use dynamic or hard casting if we know types.
            // Since we have multiple types, reflection is easiest to avoid boilerplate, 
            // OR proper pattern matching if they share a common base.
            // Usage of `dynamic` in simple scenarios:
            
            dynamic d = dialog;
            
            // Wire events
            d.PreviewRequested += new EventHandler<EffectEventArgs>((s, e) => vm.PreviewEffect(e.EffectOperation));
            d.ApplyRequested += new EventHandler<EffectEventArgs>((s, e) => 
            { 
                vm.ApplyEffect(e.EffectOperation, e.StatusMessage); 
                vm.CloseModalCommand.Execute(null);
            });
            d.CancelRequested += new EventHandler((s, e) => 
            { 
                vm.CancelEffectPreview(); 
                vm.CloseModalCommand.Execute(null);
            });

            vm.ModalContent = dialog;
            vm.IsModalOpen = true;
        }

        private void OnModalBackgroundPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // Only close if clicking on the background, not the dialog content
            if (e.Source == sender && DataContext is MainViewModel vm)
            {
                vm.CloseModalCommand.Execute(null);
            }
        }
    }
}
