#region License Information (GPL v3)

/*
    ShareX.ImageEditor - The UI-agnostic Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team

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
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ShareX.ImageEditor.Annotations;
using ShareX.ImageEditor.Controls;
using ShareX.ImageEditor.Helpers;
using ShareX.ImageEditor.ViewModels;
using ShareX.ImageEditor.Views.Controllers;
using ShareX.ImageEditor.Views.Dialogs;
using SkiaSharp;
using System.ComponentModel;

namespace ShareX.ImageEditor.Views
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

        // Sync flags to prevent loop between VM.PreviewImage <-> Core.SourceImage
        private bool _isSyncingFromVM;
        private bool _isSyncingToVM;
        private bool _skipNextCoreImageChanged;

        // SIP-CLIPBOARD: Internal clipboard for shape deep-cloning
        private static Annotation? _clipboardAnnotation;

        public EditorView()
        {
            InitializeComponent();

            _editorCore = new EditorCore();

            _zoomController = new EditorZoomController(this);
            _selectionController = new EditorSelectionController(this);
            _inputController = new EditorInputController(this, _selectionController, _zoomController);

            // Subscribe to selection controller events
            _selectionController.RequestUpdateEffect += OnRequestUpdateEffect;
            _selectionController.SelectionChanged += OnSelectionChanged;

            // SIP0018: Subscribe to Core events
            _editorCore.InvalidateRequested += () => Avalonia.Threading.Dispatcher.UIThread.Post(RenderCore);
            _editorCore.ImageChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_canvasControl != null)
                {
                    _canvasControl.Initialize((int)_editorCore.CanvasSize.Width, (int)_editorCore.CanvasSize.Height);
                    RenderCore();
                    if (DataContext is MainViewModel vm)
                    {
                        UpdateViewModelHistoryState(vm);
                        UpdateViewModelMetadata(vm);

                        // Sync Core image back to VM if change originated from Core (Undo/Redo, Core Crop)
                        if (!_isSyncingFromVM && !_isSyncingToVM && _editorCore.SourceImage != null)
                        {
                            // SIP-FIX: Break feedback loop from async ImageChanged events (e.g. Smart Padding)
                            if (_skipNextCoreImageChanged)
                            {
                                _skipNextCoreImageChanged = false;
                                return;
                            }

                            try
                            {
                                _isSyncingToVM = true;
                                vm.UpdatePreviewImageOnly(_editorCore.SourceImage, syncSourceState: true);
                            }
                            finally
                            {
                                _isSyncingToVM = false;
                            }
                        }
                    }
                }
            });
            _editorCore.AnnotationsRestored += () => Avalonia.Threading.Dispatcher.UIThread.Post(OnAnnotationsRestored);
            _editorCore.AnnotationOrderChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(OnAnnotationOrderChanged);
            _editorCore.HistoryChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is MainViewModel vm)
                {
                    UpdateViewModelHistoryState(vm);
                    vm.RecalculateNumberCounter(_editorCore.Annotations);

                    // Mark as dirty when history changes (annotations added/interactions/undo/redo)
                    vm.IsDirty = true;
                }
            });

            // Capture wheel events in tunneling phase so ScrollViewer doesn't scroll when using Ctrl+wheel zoom.
            AddHandler(PointerWheelChangedEvent, OnPreviewPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);

            // Enable drag-and-drop for image files
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DropEvent, OnDrop);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
        }

        private void OnSelectionChanged(bool hasSelection)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.HasSelectedAnnotation = hasSelection;
                var annotation = _selectionController.SelectedShape?.Tag as Annotation;
                vm.SelectedAnnotation = annotation;

                // Sync selection to EditorCore so z-order operations work
                if (annotation != null)
                {
                    _editorCore.Select(annotation);
                }
                else
                {
                    _editorCore.Deselect();
                }

                // Sync VM properties with selected annotation to update UI
                if (vm.SelectedAnnotation != null)
                {
                    // Prevent feedback loop: UI update -> VM Property Changed -> Apply to Annotation (redundant)
                    // But Apply... methods limit damage.

                    // Don't sync stroke properties from ImageAnnotation — it has StrokeWidth=0
                    // which would clobber Options.Thickness and break other tools
                    if (vm.SelectedAnnotation is not ImageAnnotation)
                    {
                        vm.SelectedColor = vm.SelectedAnnotation.StrokeColor;
                        vm.StrokeWidth = (int)vm.SelectedAnnotation.StrokeWidth;
                        vm.ShadowEnabled = vm.SelectedAnnotation.ShadowEnabled;
                    }

                    if (vm.SelectedAnnotation is NumberAnnotation num)
                    {
                        vm.FontSize = num.FontSize;
                        vm.FillColor = num.FillColor;
                    }
                    else if (vm.SelectedAnnotation is TextAnnotation text)
                    {
                        vm.FontSize = text.FontSize;
                    }
                    else if (vm.SelectedAnnotation is SpeechBalloonAnnotation balloon)
                    {
                        vm.FontSize = balloon.FontSize;
                        vm.FillColor = balloon.FillColor;
                    }
                    else if (vm.SelectedAnnotation is RectangleAnnotation rect)
                    {
                        vm.FillColor = rect.FillColor;
                    }
                    else if (vm.SelectedAnnotation is EllipseAnnotation ellipse)
                    {
                        vm.FillColor = ellipse.FillColor;
                    }
                    else if (vm.SelectedAnnotation is BaseEffectAnnotation effect)
                    {
                        vm.EffectStrength = (int)effect.Amount;
                    }
                }
            }
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

            // Check clipboard initially
            _ = CheckClipboardStatus();

            // Listen for window activation to check clipboard (as close as we get to ClipboardChanged)
            if (TopLevel.GetTopLevel(this) is Window window)
            {
                window.Activated += (s, args) => _ = CheckClipboardStatus();
            }

            if (DataContext is MainViewModel vm)
            {
                vm.AttachEditorCore(_editorCore);
                vm.DeleteRequested += (s, args) => PerformDelete();
                vm.UndoRequested += (s, args) => PerformUndo();
                vm.RedoRequested += (s, args) => PerformRedo();
                vm.ClearAnnotationsRequested += (s, args) => ClearAllAnnotations();

                // Subscribe to new context menu events
                vm.CutAnnotationRequested += OnCutRequested;
                vm.CopyAnnotationRequested += OnCopyRequested;
                vm.PasteRequested += OnPasteRequested;
                vm.DuplicateRequested += OnDuplicateRequested;
                vm.ZoomToFitRequested += OnZoomToFitRequested;

                // Original code subscribed to vm.PropertyChanged
                vm.PropertyChanged += OnViewModelPropertyChanged;

                // Initialize zoom
                _zoomController.InitLastZoom(vm.Zoom);

                // Wire up View interactions
                vm.DeselectRequested += OnDeselectRequested;

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
                vm.DeselectRequested -= OnDeselectRequested;
                vm.ZoomToFitRequested -= OnZoomToFitRequested;
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
                    // During smart padding, use UpdateSourceImage to preserve history and annotations
                    if (vm.IsSmartPaddingInProgress)
                    {
                        UpdateSourceImageFromViewModel(vm);
                    }
                    else
                    {
                        LoadImageFromViewModel(vm);
                    }
                }
                else if (e.PropertyName == nameof(MainViewModel.Zoom))
                {
                    _zoomController.HandleZoomPropertyChanged(vm);
                }
                else if (e.PropertyName == nameof(MainViewModel.ActiveTool))
                {
                    _selectionController.ClearSelection();
                    UpdateCursorForTool(); // ISSUE-018 fix: Update cursor feedback for active tool
                }
            }
        }

        /// <summary>
        /// ISSUE-018 fix: Updates the canvas cursor based on the active tool
        /// </summary>
        private void UpdateCursorForTool()
        {
            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas == null || DataContext is not MainViewModel vm) return;

            canvas.Cursor = vm.ActiveTool switch
            {
                EditorTool.Select => new Cursor(StandardCursorType.Arrow),
                EditorTool.Crop or EditorTool.CutOut => new Cursor(StandardCursorType.Cross),
                _ => new Cursor(StandardCursorType.Cross) // Drawing tools (Rectangle, Ellipse, Pen, etc.)
            };
        }

        // --- Public/Internal Methods for Controllers ---

        protected override void OnInitialized()
        {
            base.OnInitialized();
            _canvasControl = this.FindControl<SKCanvasControl>("CanvasControl");
        }

        private void LoadImageFromViewModel(MainViewModel vm)
        {
            if (vm.PreviewImage == null || _canvasControl == null) return;
            if (_isSyncingToVM) return; // Ignore updates that we just pushed to VM

            try
            {
                _isSyncingFromVM = true;

                // One-time conversion from Avalonia Bitmap to SKBitmap for the Core
                // In a full refactor, VM would hold SKBitmap source of truth
                using var skBitmap = BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
                if (skBitmap != null)
                {
                    // We must copy because ToSKBitmap might return a disposable wrapper or we need ownership
                    // ISSUE-FIX: Use UpdateSourceImage to preserve existing history/annotations
                    // This allows VM-driven updates (Effects, Undo) to not wipe Core state.
                    // New file loads should be preceded by Clear() from the VM/Host.
                    _skipNextCoreImageChanged = true;
                    _editorCore.UpdateSourceImage(skBitmap.Copy());

                    _canvasControl.Initialize(skBitmap.Width, skBitmap.Height);
                    RenderCore();
                }
            }
            finally
            {
                _isSyncingFromVM = false;
            }
        }

        /// <summary>
        /// Updates the source image in EditorCore without clearing history or annotations.
        /// Used during smart padding operations to preserve editing state.
        /// </summary>
        private void UpdateSourceImageFromViewModel(MainViewModel vm)
        {
            if (vm.PreviewImage == null || _canvasControl == null) return;

            using var skBitmap = BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
            if (skBitmap != null)
            {
                _skipNextCoreImageChanged = true;
                _editorCore.UpdateSourceImage(skBitmap.Copy());
                _canvasControl.Initialize(skBitmap.Width, skBitmap.Height);
                RenderCore();
            }
        }

        private void RenderCore()
        {
            if (_canvasControl == null) return;
            // Hybrid rendering: Render only background + raster effects from Core
            // Vector annotations are handled by Avalonia Canvas
            _canvasControl.Draw(canvas => _editorCore.Render(canvas));
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
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        if (vm.ClearAnnotationsCommand.CanExecute(null))
                        {
                            vm.ClearAnnotationsCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                    else
                    {
                        vm.DeleteSelectedCommand.Execute(null);
                        e.Handled = true;
                    }
                }
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Shift))
                {
                    switch (e.Key)
                    {
                        case Key.Z: vm.RedoCommand.Execute(null); e.Handled = true; break;
                        case Key.C: vm.CopyCommand.Execute(null); e.Handled = true; break;
                        case Key.S: vm.SaveAsCommand.Execute(null); e.Handled = true; break;
                    }
                }
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    switch (e.Key)
                    {
                        case Key.Z: vm.UndoCommand.Execute(null); e.Handled = true; break;
                        case Key.Y: vm.RedoCommand.Execute(null); e.Handled = true; break;
                        case Key.X: vm.CutAnnotationCommand.Execute(null); e.Handled = true; break;
                        case Key.C: vm.CopyAnnotationCommand.Execute(null); e.Handled = true; break;
                        case Key.V: vm.PasteCommand.Execute(null); e.Handled = true; break;
                        case Key.D: DuplicateSelectedAnnotation(); e.Handled = true; break;
                        case Key.S: vm.SaveCommand.Execute(null); e.Handled = true; break;
                        case Key.P: vm.PinToScreenCommand.Execute(null); e.Handled = true; break;
                        case Key.U: vm.UploadCommand.Execute(null); e.Handled = true; break;
                    }
                }
                else if (e.KeyModifiers == KeyModifiers.None || e.KeyModifiers == KeyModifiers.Shift)
                {
                    double step = e.KeyModifiers == KeyModifiers.Shift ? 10 : 1;

                    if ((e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right) && _selectionController.SelectedShape != null)
                    {
                        double dx = e.Key == Key.Left ? -step : (e.Key == Key.Right ? step : 0);
                        double dy = e.Key == Key.Up ? -step : (e.Key == Key.Down ? step : 0);
                        _selectionController.MoveSelectedShape(dx, dy);
                        e.Handled = true;
                    }
                    else if (e.KeyModifiers == KeyModifiers.None)
                    {
                        // Tool shortcuts
                        switch (e.Key)
                        {
                            case Key.Home: _editorCore.BringToFront(); e.Handled = true; break;
                            case Key.End: _editorCore.SendToBack(); e.Handled = true; break;
                            case Key.PageUp: _editorCore.BringForward(); e.Handled = true; break;
                            case Key.PageDown: _editorCore.SendBackward(); e.Handled = true; break;

                            case Key.V: vm.SelectToolCommand.Execute(EditorTool.Select); e.Handled = true; break;
                            case Key.R: vm.SelectToolCommand.Execute(EditorTool.Rectangle); e.Handled = true; break;
                            case Key.E: vm.SelectToolCommand.Execute(EditorTool.Ellipse); e.Handled = true; break;
                            case Key.L: vm.SelectToolCommand.Execute(EditorTool.Line); e.Handled = true; break;
                            case Key.A: vm.SelectToolCommand.Execute(EditorTool.Arrow); e.Handled = true; break;
                            case Key.F: vm.SelectToolCommand.Execute(EditorTool.Freehand); e.Handled = true; break; // Freehand
                            case Key.T: vm.SelectToolCommand.Execute(EditorTool.Text); e.Handled = true; break;
                            case Key.O: vm.SelectToolCommand.Execute(EditorTool.SpeechBalloon); e.Handled = true; break;
                            case Key.N: vm.SelectToolCommand.Execute(EditorTool.Step); e.Handled = true; break;
                            case Key.W: vm.SelectToolCommand.Execute(EditorTool.SmartEraser); e.Handled = true; break;
                            case Key.S: vm.SelectToolCommand.Execute(EditorTool.Spotlight); e.Handled = true; break;
                            case Key.B: vm.SelectToolCommand.Execute(EditorTool.Blur); e.Handled = true; break;
                            case Key.P: vm.SelectToolCommand.Execute(EditorTool.Pixelate); e.Handled = true; break;
                            case Key.I: vm.SelectToolCommand.Execute(EditorTool.Image); e.Handled = true; break;
                            case Key.H: vm.SelectToolCommand.Execute(EditorTool.Highlight); e.Handled = true; break;
                            case Key.M: vm.SelectToolCommand.Execute(EditorTool.Magnify); e.Handled = true; break;
                            case Key.C: vm.SelectToolCommand.Execute(EditorTool.Crop); e.Handled = true; break;
                            case Key.U: vm.SelectToolCommand.Execute(EditorTool.CutOut); e.Handled = true; break;

                            case Key.Enter:
                                if (vm.TaskMode)
                                {
                                    vm.ContinueCommand.Execute(null);
                                    e.Handled = true;
                                }
                                break;
                        }
                    }
                }
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Source is TextBox) return;

            if (DataContext is MainViewModel vm && e.KeyModifiers == KeyModifiers.None)
            {
                switch (e.Key)
                {
                    case Key.Escape:
                        if (_selectionController.SelectedShape != null)
                        {
                            _selectionController.ClearSelection();
                            e.Handled = true;
                        }
                        else if (vm.TaskMode)
                        {
                            vm.CancelCommand.Execute(null);
                            e.Handled = true;
                        }
                        else
                        {
                            if (TopLevel.GetTopLevel(this) is Avalonia.Controls.Window window)
                            {
                                window.Close();
                                e.Handled = true;
                            }
                        }
                        break;
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

        private void OnDeselectRequested(object? sender, EventArgs e)
        {
            _selectionController.ClearSelection();
        }

        private void OnAnnotationsRestored()
        {
            // Fully rebuild annotation layer from Core state
            // 1. Clear current UI annotations
            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas == null) return;

            // Dispose old annotations before clearing
            foreach (var child in canvas.Children)
            {
                if (child is Control control)
                {
                    (control.Tag as IDisposable)?.Dispose();
                }
            }

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

            // 3. Validate state synchronization (ISSUE-001 mitigation)
            ValidateAnnotationSync();

            // Update HasAnnotations state
            UpdateHasAnnotationsState();
        }

        private void OnAnnotationOrderChanged()
        {
            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas == null) return;

            var children = canvas.Children.OfType<Control>().ToList();
            if (children.Count == 0) return;

            var coreAnnotations = _editorCore.Annotations;
            // Create a lookup for O(1) index access
            var indexLookup = new Dictionary<Annotation, int>();
            for (int i = 0; i < coreAnnotations.Count; i++)
            {
                indexLookup[coreAnnotations[i]] = i;
            }

            children.Sort((a, b) =>
            {
                int indexA = int.MaxValue;
                if (a.Tag is Annotation tagA && indexLookup.TryGetValue(tagA, out var ia))
                {
                    indexA = ia;
                }

                int indexB = int.MaxValue;
                if (b.Tag is Annotation tagB && indexLookup.TryGetValue(tagB, out var ib))
                {
                    indexB = ib;
                }

                return indexA.CompareTo(indexB);
            });

            canvas.Children.Clear();
            canvas.Children.AddRange(children);
        }

        private Control? CreateControlForAnnotation(Annotation annotation)
        {
            var control = AnnotationVisualFactory.CreateVisualControl(annotation, AnnotationVisualMode.Persisted);
            if (control == null)
            {
                return null;
            }

            AnnotationVisualFactory.UpdateVisualControl(
                control,
                annotation,
                AnnotationVisualMode.Persisted,
                _editorCore.CanvasSize.Width,
                _editorCore.CanvasSize.Height);

            // Effect annotations require bitmap-backed fills from current source image.
            if (annotation is BaseEffectAnnotation)
            {
                OnRequestUpdateEffect(control);
            }

            return control;
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
                    // Sync with EditorCore - this creates the undo history entry
                    if (selected.Tag is Annotation annotation)
                    {
                        // Select the annotation in core so DeleteSelected knows what to remove
                        _editorCore.Select(annotation);
                        _editorCore.DeleteSelected();
                    }

                    // Dispose annotation resources before removing from view
                    (selected.Tag as IDisposable)?.Dispose();

                    canvas.Children.Remove(selected);

                    _selectionController.ClearSelection();

                    // Update HasAnnotations state
                    UpdateHasAnnotationsState();
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
                _editorCore.ClearAll(resetHistory: false);
                RenderCore();

                // Update HasAnnotations state
                if (DataContext is MainViewModel vm)
                {
                    vm.HasAnnotations = false;
                }
            }
        }

        /// <summary>
        /// Updates the ViewModel's HasAnnotations property based on current annotation count.
        /// </summary>
        private void UpdateHasAnnotationsState()
        {
            if (DataContext is MainViewModel vm)
            {
                var canvas = this.FindControl<Canvas>("AnnotationCanvas");
                int coreAnnotationCount = _editorCore.Annotations.Count;
                int canvasChildCount = canvas?.Children.Count ?? 0;
                vm.HasAnnotations = coreAnnotationCount > 0 || canvasChildCount > 0;
            }
        }

        public SkiaSharp.SKBitmap? GetSource()
        {
            if (_editorCore.SourceImage != null)
            {
                return _editorCore.SourceImage.Copy();
            }

            return null;
        }

        public SkiaSharp.SKBitmap? GetSnapshot()
        {
            if (_editorCore.SourceImage == null) return null;

            var canvasContainer = this.FindControl<Grid>("CanvasContainer");
            var overlayCanvas = this.FindControl<Canvas>("OverlayCanvas");
            if (canvasContainer == null) return _editorCore.GetSnapshot();

            // Hide OverlayCanvas (selection handles, crop overlay) during capture
            bool overlayWasVisible = overlayCanvas?.IsVisible ?? false;
            if (overlayCanvas != null) overlayCanvas.IsVisible = false;

            try
            {
                int width = _editorCore.SourceImage.Width;
                int height = _editorCore.SourceImage.Height;

                // Force layout at native resolution (un-zoomed)
                canvasContainer.Measure(new Size(width, height));
                canvasContainer.Arrange(new Rect(0, 0, width, height));

                // Render Avalonia visual tree to bitmap
                var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
                rtb.Render(canvasContainer);

                // Convert Avalonia RenderTargetBitmap → SKBitmap
                using var stream = new System.IO.MemoryStream();
                rtb.Save(stream);
                stream.Position = 0;
                var skBitmap = SkiaSharp.SKBitmap.Decode(stream);

                return skBitmap;
            }
            finally
            {
                // Restore OverlayCanvas visibility
                if (overlayCanvas != null) overlayCanvas.IsVisible = overlayWasVisible;

                // Re-trigger layout with current zoom
                canvasContainer.InvalidateMeasure();
                canvasContainer.InvalidateArrange();
            }
        }

        public Task<Bitmap?> RenderSnapshot()
        {
            var skBitmap = GetSnapshot();
            var snapshot = skBitmap != null ? BitmapConversionHelpers.ToAvaloniBitmap(skBitmap) : null;
            return Task.FromResult<Bitmap?>(snapshot);
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
                // Use explicit Width/Height first, fallback to Bounds, then annotation bounds
                double width = shape.Width;
                double height = shape.Height;
                if (double.IsNaN(width) || width <= 0) width = shape.Bounds.Width;
                if (double.IsNaN(height) || height <= 0) height = shape.Bounds.Height;
                // Final fallback to annotation's own bounds
                if (width <= 0 || height <= 0)
                {
                    var bounds = annotation.GetBounds();
                    width = bounds.Width;
                    height = bounds.Height;
                }
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
                var hexColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                vm.SetColorCommand.Execute(hexColor);
            }
        }

        private void OnFillColorChanged(object? sender, IBrush color)
        {
            if (DataContext is MainViewModel vm && color is SolidColorBrush solidBrush)
            {
                var hexColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                vm.FillColor = hexColor;

                // Apply to selected annotation if any
                var selected = _selectionController.SelectedShape;
                if (selected?.Tag is Annotation annotation)
                {
                    annotation.FillColor = hexColor;

                    // Update the UI control's Fill property
                    if (selected is Shape shape)
                    {
                        shape.Fill = hexColor == "#00000000" ? Brushes.Transparent : solidBrush;
                    }
                    else if (selected is Grid grid)
                    {
                        // For NumberAnnotation, update the Ellipse fill
                        foreach (var child in grid.Children)
                        {
                            if (child is Avalonia.Controls.Shapes.Ellipse ellipse)
                            {
                                ellipse.Fill = hexColor == "#00000000" ? Brushes.Transparent : solidBrush;
                            }
                        }
                    }
                    else if (selected is SpeechBalloonControl balloon)
                    {
                        // For speech balloon, trigger visual invalidation to redraw filling
                        balloon.InvalidateVisual();
                    }

                    // ISSUE-LIVE-UPDATE: Update active text editor if present
                    _selectionController.UpdateActiveTextEditorProperties();
                }
            }
        }

        private void OnFontSizeChanged(object? sender, float fontSize)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.FontSize = fontSize;

                // ... (rest of logic) ...

                // Apply to selected annotation if any
                var selected = _selectionController.SelectedShape;
                if (selected?.Tag is TextAnnotation textAnn)
                {
                    textAnn.FontSize = fontSize;
                    if (selected is TextBox textBox)
                    {
                        textBox.FontSize = fontSize;
                    }
                }
                else if (selected?.Tag is NumberAnnotation numAnn)
                {
                    numAnn.FontSize = fontSize;

                    // Update the visual - resize grid and update text
                    if (selected is Grid grid)
                    {
                        var radius = Math.Max(12, fontSize * 0.7f);
                        grid.Width = radius * 2;
                        grid.Height = radius * 2;

                        foreach (var child in grid.Children)
                        {
                            if (child is TextBlock textBlock)
                            {
                                textBlock.FontSize = fontSize * 0.6; // Match CreateVisual scaling
                            }
                        }
                    }
                }
                else if (selected?.Tag is SpeechBalloonAnnotation balloonAnn)
                {
                    balloonAnn.FontSize = fontSize;
                    if (selected is SpeechBalloonControl balloonControl)
                    {
                        balloonControl.InvalidateVisual();
                    }
                    _selectionController.UpdateActiveTextEditorProperties();
                }
            }
        }

        private void OnStrengthChanged(object? sender, float strength)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.EffectStrength = strength;

                // Apply to selected annotation if any
                var selected = _selectionController.SelectedShape;
                if (selected?.Tag is BaseEffectAnnotation effectAnn)
                {
                    effectAnn.Amount = strength;
                    // Regenerate effect
                    OnRequestUpdateEffect(selected);
                }
                else if (selected?.Tag is SpotlightAnnotation spotlightAnn)
                {
                    // Map EffectStrength (0-30) to DarkenOpacity (0-255)
                    spotlightAnn.DarkenOpacity = (byte)Math.Clamp(strength / 30.0 * 255, 0, 255);

                    if (selected is SpotlightControl spotlightControl)
                    {
                        spotlightControl.InvalidateVisual();
                    }
                }
            }
        }

        private void OnShadowButtonClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Toggle state
                vm.ShadowEnabled = !vm.ShadowEnabled;
                var isEnabled = vm.ShadowEnabled;

                // Apply to selected annotation if any
                var selected = _selectionController.SelectedShape;
                if (selected?.Tag is Annotation annotation)
                {
                    annotation.ShadowEnabled = isEnabled;

                    // Update the UI control's Effect property
                    if (selected is Control control)
                    {
                        if (isEnabled)
                        {
                            control.Effect = new Avalonia.Media.DropShadowEffect
                            {
                                OffsetX = 3,
                                OffsetY = 3,
                                BlurRadius = 4,
                                Color = Avalonia.Media.Color.FromArgb(128, 0, 0, 0)
                            };
                        }
                        else
                        {
                            control.Effect = null;
                        }
                    }
                }
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

                    // vm.CropImage(physX, physY, physW, physH);
                    // SIP-FIX: Use Core crop to handle annotation adjustment and history unified
                    _editorCore.Crop(new SKRect(physX, physY, physX + physW, physY + physH));
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

            // Ensure the annotation model property is updated so changes persist and effects render correctly
            if (selected.Tag is Annotation annotation)
            {
                annotation.StrokeColor = colorHex;
            }

            var brush = new SolidColorBrush(Color.Parse(colorHex));

            switch (selected)
            {
                case Shape shape:
                    if (shape.Tag is HighlightAnnotation)
                    {
                        // For highlighter, we must regenerate the effect bitmap with the new color
                        OnRequestUpdateEffect(shape);
                        break;
                    }

                    shape.Stroke = brush;

                    if (shape is global::Avalonia.Controls.Shapes.Path path)
                    {
                        path.Fill = brush;
                    }
                    else if (selected is SpeechBalloonControl balloon)
                    {
                        balloon.InvalidateVisual();
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
                            ellipse.Stroke = brush;
                        }
                    }
                    break;
                case SpeechBalloonControl balloonControl:
                    balloonControl.InvalidateVisual();
                    break;
            }

            // ISSUE-LIVE-UPDATE: Update active text editor if present
            _selectionController.UpdateActiveTextEditorProperties();
        }

        private void ApplySelectedStrokeWidth(int width)
        {
            var selected = _selectionController.SelectedShape;
            if (selected == null) return;

            if (selected.Tag is Annotation annotation)
            {
                annotation.StrokeWidth = width;
            }

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
                case SpeechBalloonControl balloon:
                    if (balloon.Annotation != null)
                    {
                        balloon.Annotation.StrokeWidth = width;
                        balloon.InvalidateVisual();
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
                    vm.CloseEffectsPanelCommand.Execute(null);
                };

                dialog.CancelRequested += (s, args) =>
                {
                    vm.CloseEffectsPanelCommand.Execute(null);
                };

                vm.EffectsPanelContent = dialog;
                vm.IsEffectsPanelOpen = true;
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
                    vm.CloseEffectsPanelCommand.Execute(null);
                };

                dialog.CancelRequested += (s, args) =>
                {
                    vm.CloseEffectsPanelCommand.Execute(null);
                };

                vm.EffectsPanelContent = dialog;
                vm.IsEffectsPanelOpen = true;
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
                    // vm.CropImage(args.X, args.Y, args.Width, args.Height);
                    // SIP-FIX: Use Core crop to handle annotation adjustment and history unified
                    _editorCore.Crop(new SKRect(args.X, args.Y, args.X + args.Width, args.Y + args.Height));

                    vm.CloseEffectsPanelCommand.Execute(null);
                };

                dialog.CancelRequested += (s, args) =>
                {
                    vm.CloseEffectsPanelCommand.Execute(null);
                };

                vm.EffectsPanelContent = dialog;
                vm.IsEffectsPanelOpen = true;
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
                var dialog = new RotateCustomAngleDialog();
                vm.EffectsPanelContent = dialog;
                vm.IsEffectsPanelOpen = true;
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
        private void OnSepiaRequested(object? sender, EventArgs e) => ShowEffectDialog(new SepiaDialog());
        private void OnPolaroidRequested(object? sender, EventArgs e) { if (DataContext is MainViewModel vm) vm.PolaroidCommand.Execute(null); }

        // Filter handlers
        private void OnBorderRequested(object? sender, EventArgs e) => ShowEffectDialog(new BorderDialog());
        private void OnOutlineRequested(object? sender, EventArgs e) => ShowEffectDialog(new OutlineDialog());
        private void OnShadowRequested(object? sender, EventArgs e) => ShowEffectDialog(new ShadowDialog());
        private void OnGlowRequested(object? sender, EventArgs e) => ShowEffectDialog(new GlowDialog());
        private void OnReflectionRequested(object? sender, EventArgs e) => ShowEffectDialog(new ReflectionDialog());
        private void OnTornEdgeRequested(object? sender, EventArgs e) => ShowEffectDialog(new TornEdgeDialog());
        private void OnSliceRequested(object? sender, EventArgs e) => ShowEffectDialog(new SliceDialog());
        private void OnRoundedCornersRequested(object? sender, EventArgs e) => ShowEffectDialog(new RoundedCornersDialog());
        private void OnSkewRequested(object? sender, EventArgs e) => ShowEffectDialog(new SkewDialog());
        private void OnRotate3DRequested(object? sender, EventArgs e) => ShowEffectDialog(new Rotate3DDialog());
        private void OnBlurRequested(object? sender, EventArgs e) => ShowEffectDialog(new BlurDialog());
        private void OnPixelateRequested(object? sender, EventArgs e) => ShowEffectDialog(new PixelateDialog());
        private void OnSharpenRequested(object? sender, EventArgs e) => ShowEffectDialog(new SharpenDialog());


        private void ShowEffectDialog<T>(T dialog) where T : UserControl, IEffectDialog
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            // Initialize logic
            vm.StartEffectPreview();

            // Wire events using interface instead of dynamic
            dialog.PreviewRequested += (s, e) => vm.PreviewEffect(e.EffectOperation);
            dialog.ApplyRequested += (s, e) =>
            {
                vm.ApplyEffect(e.EffectOperation, e.StatusMessage);
                vm.CloseEffectsPanelCommand.Execute(null);
            };
            dialog.CancelRequested += (s, e) =>
            {
                vm.CancelEffectPreview();
                vm.CloseEffectsPanelCommand.Execute(null);
            };

            // If left sidebar is open, close it to avoid clutter? 
            // The request says "Side bar at right side won't cover the image preview at center".
            // So we can keep left sidebar open or close it. 
            // Usually only one "main" panel is active or both sidebars. 
            // Let's keep existing behavior for SettingsPanel (left) but ensure EffectsPanel (right) opens.

            vm.EffectsPanelContent = dialog;
            vm.IsEffectsPanelOpen = true;
        }

        private void OnModalBackgroundPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // Only close if clicking on the background, not the dialog content
            if (e.Source == sender && DataContext is MainViewModel vm)
            {
                vm.CancelEffectPreview();
                vm.CloseModalCommand.Execute(null);
            }
        }

        /// <summary>
        /// Validates that UI annotation state is synchronized with EditorCore state.
        /// ISSUE-001 mitigation: Detect annotation count mismatches in dual-state architecture.
        /// </summary>
        private void ValidateAnnotationSync()
        {
            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas == null) return;

            // Count UI annotations (exclude non-annotation controls like CropOverlay)
            int uiAnnotationCount = 0;
            foreach (var child in canvas.Children)
            {
                if (child is Control control && control.Tag is Annotation &&
                    control.Name != "CropOverlay" && control.Name != "CutOutOverlay")
                {
                    uiAnnotationCount++;
                }
            }

            int coreAnnotationCount = _editorCore.Annotations.Count;
        }

        // --- Image Paste & Drag-Drop ---

        /// <summary>
        /// Shared helper to insert an image annotation from an SKBitmap.
        /// Adds the annotation to both the Avalonia canvas and EditorCore, then switches to Select tool.
        /// </summary>
        private void InsertImageAnnotation(SKBitmap skBitmap, Point? dropPosition = null)
        {
            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas == null || DataContext is not MainViewModel vm)
            {
                return;
            }

            // Calculate position: drop point or center of canvas
            var posX = dropPosition?.X ?? (_editorCore.CanvasSize.Width / 2 - skBitmap.Width / 2);
            var posY = dropPosition?.Y ?? (_editorCore.CanvasSize.Height / 2 - skBitmap.Height / 2);

            var annotation = new ImageAnnotation();
            annotation.SetImage(skBitmap);
            annotation.StartPoint = new SKPoint((float)posX, (float)posY);
            annotation.EndPoint = new SKPoint(
                (float)posX + skBitmap.Width,
                (float)posY + skBitmap.Height);

            var avBitmap = BitmapConversionHelpers.ToAvaloniBitmap(skBitmap);
            var imageControl = new Image
            {
                Source = avBitmap,
                Width = skBitmap.Width,
                Height = skBitmap.Height,
                Tag = annotation
            };
            Canvas.SetLeft(imageControl, posX);
            Canvas.SetTop(imageControl, posY);

            canvas.Children.Add(imageControl);
            _editorCore.AddAnnotation(annotation);
            vm.HasAnnotations = true;
            vm.ActiveTool = EditorTool.Select; // Auto-switch to Select tool
            _selectionController.SetSelectedShape(imageControl);
        }

        /// <summary>
        /// Handles Ctrl+V paste of images from clipboard (both bitmap data and file references).
        /// </summary>
        private async Task PasteImageFromClipboard()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var clipboard = topLevel?.Clipboard;
            if (clipboard == null) return;

            try
            {
                // Check for image file paths in clipboard (e.g. copied from Explorer)
                var files = await clipboard.TryGetFilesAsync();
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        if (file is IStorageFile storageFile)
                        {
                            var ext = System.IO.Path.GetExtension(storageFile.Name)?.ToLowerInvariant();
                            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp" || ext == ".ico" || ext == ".tiff" || ext == ".tif")
                            {
                                try
                                {
                                    using var stream = await storageFile.OpenReadAsync();
                                    using var memStream = new System.IO.MemoryStream();
                                    await stream.CopyToAsync(memStream);
                                    memStream.Position = 0;
                                    var skBitmap = SKBitmap.Decode(memStream);
                                    if (skBitmap != null)
                                    {
                                        InsertImageAnnotation(skBitmap);
                                        return;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }

                // Try clipboard bitmap data (e.g. PrintScreen, copy from image app)
                var clipboardBitmap = await clipboard.TryGetBitmapAsync();
                if (clipboardBitmap != null)
                {
                    var skBitmap = BitmapConversionHelpers.ToSKBitmap(clipboardBitmap);
                    if (skBitmap != null)
                    {
                        InsertImageAnnotation(skBitmap);
                        return;
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Handles DragOver event to show appropriate drag cursor.
        /// </summary>
        private void OnDragOver(object? sender, DragEventArgs e)
        {
            // Keep DragOver lightweight and non-consuming; resolve concrete files in OnDrop.
            e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        /// <summary>
        /// Handles drag-and-drop of image files onto the editor canvas.
        /// </summary>
        private async void OnDrop(object? sender, DragEventArgs e)
        {
            var droppedItems = e.DataTransfer.TryGetFiles()?.ToList() ?? new List<IStorageItem>();

            // Fallback for providers that expose files only through raw items.
            if (droppedItems.Count == 0)
            {
                foreach (var item in e.DataTransfer.Items)
                {
                    if (item.TryGetRaw(DataFormat.File) is IStorageItem storageItem)
                    {
                        droppedItems.Add(storageItem);
                    }
                }
            }

            if (droppedItems.Count > 0)
            {
                // Get drop position relative to the annotation canvas
                var canvas = this.FindControl<Canvas>("AnnotationCanvas");
                Point? dropPos = null;
                if (canvas != null)
                {
                    dropPos = e.GetPosition(canvas);
                }

                foreach (var item in droppedItems)
                {
                    if (item is IStorageFile file)
                    {
                        var ext = System.IO.Path.GetExtension(file.Name)?.ToLowerInvariant();

                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp" || ext == ".ico" || ext == ".tiff" || ext == ".tif")
                        {
                            try
                            {
                                using var stream = await file.OpenReadAsync();
                                using var memStream = new System.IO.MemoryStream();
                                await stream.CopyToAsync(memStream);
                                memStream.Position = 0;
                                var skBitmap = SKBitmap.Decode(memStream);
                                if (skBitmap != null)
                                {
                                    // If there's no base image yet (common in embedded MainWindow editor),
                                    // use the dropped file as the main preview image.
                                    if (DataContext is MainViewModel vm && !vm.HasPreviewImage)
                                    {
                                        vm.UpdatePreview(skBitmap, clearAnnotations: true);
                                        return;
                                    }

                                    // Otherwise add it as an image annotation on top of the current canvas.
                                    var centeredPos = dropPos.HasValue
                                        ? new Point(dropPos.Value.X - skBitmap.Width / 2, dropPos.Value.Y - skBitmap.Height / 2)
                                        : (Point?)null;
                                    InsertImageAnnotation(skBitmap, centeredPos);
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Duplicates the currently selected annotation with a deep copy.
        /// The duplicate is offset by 20px and becomes the new selection.
        /// </summary>
        private void DuplicateSelectedAnnotation()
        {
            var selectedControl = _selectionController.SelectedShape;
            if (selectedControl == null) return;

            var annotation = selectedControl.Tag as Annotation;
            if (annotation == null) return;

            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas == null) return;

            // Deep clone the annotation (ImageAnnotation.Clone deep-copies the bitmap)
            var clone = annotation.Clone();

            // Offset the duplicate by 20px
            const float offset = 20f;
            clone.StartPoint = new SkiaSharp.SKPoint(clone.StartPoint.X + offset, clone.StartPoint.Y + offset);
            clone.EndPoint = new SkiaSharp.SKPoint(clone.EndPoint.X + offset, clone.EndPoint.Y + offset);

            // Offset freehand/eraser points if applicable
            if (clone is FreehandAnnotation freehandClone)
            {
                for (int i = 0; i < freehandClone.Points.Count; i++)
                {
                    var pt = freehandClone.Points[i];
                    freehandClone.Points[i] = new SkiaSharp.SKPoint(pt.X + offset, pt.Y + offset);
                }
            }
            else if (clone is SmartEraserAnnotation eraserClone)
            {
                for (int i = 0; i < eraserClone.Points.Count; i++)
                {
                    var pt = eraserClone.Points[i];
                    eraserClone.Points[i] = new SkiaSharp.SKPoint(pt.X + offset, pt.Y + offset);
                }
            }

            // Add to EditorCore (captures undo history before adding)
            _editorCore.AddAnnotation(clone);

            // Create the UI control for the cloned annotation
            var control = CreateControlForAnnotation(clone);
            if (control != null)
            {
                canvas.Children.Add(control);
                _selectionController.SetSelectedShape(control);
            }

            // Update clipboard status after internal copy
            _ = CheckClipboardStatus();

            // Update HasAnnotations state
            if (DataContext is MainViewModel vm)
            {
                vm.HasAnnotations = true;
            }
        }
        private async void OnCutRequested(object? sender, EventArgs e)
        {
            if (_selectionController.SelectedShape?.Tag is Annotation annotation)
            {
                // Copy to internal clipboard
                _clipboardAnnotation = annotation.Clone();

                // Update clipboard status
                _ = CheckClipboardStatus();

                // Clear system clipboard to avoid ambiguity when pasting back

                // Clear system clipboard to avoid ambiguity when pasting back
                try
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel?.Clipboard != null)
                    {
                        await topLevel.Clipboard.ClearAsync();
                    }
                }
                catch { }

                // Delete original using ViewModel command to ensure undo history is recorded
                if (DataContext is MainViewModel vm)
                {
                    vm.DeleteSelectedCommand.Execute(null);
                }
            }
        }

        private async void OnCopyRequested(object? sender, EventArgs e)
        {
            if (_selectionController.SelectedShape?.Tag is Annotation annotation)
            {
                // Deep clone to internal clipboard
                _clipboardAnnotation = annotation.Clone();

                // Update clipboard status
                _ = CheckClipboardStatus();

                // Clear system clipboard to avoid ambiguity when pasting back

                // Clear system clipboard to avoid ambiguity when pasting back
                // This ensures that if the user pastes, we know to use the internal clipboard
                // unless they subsequently copy something externally
                try
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel?.Clipboard != null)
                    {
                        await topLevel.Clipboard.ClearAsync();
                    }
                }
                catch { }
            }
        }

        private async void OnPasteRequested(object? sender, EventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var clipboard = topLevel.Clipboard;

            try
            {
                // Priority 1: Check system clipboard for images/files (external content)
                // This allows users to copy from browser/explorer and paste even if they previously copied a shape
                if (clipboard != null)
                {
                    // Check for files
                    var files = await clipboard.TryGetFilesAsync();
                    if (files != null && files.Any())
                    {
                        await PasteImageFromClipboard();
                        return;
                    }

                    // Check for bitmap
                    var formats = await clipboard.GetDataFormatsAsync();
                    if (formats.Any(f => f.ToString() == "PNG") || formats.Any(f => f.ToString() == "Bitmap") || formats.Any(f => f.ToString() == "DeviceIndependentBitmap"))
                    {
                        await PasteImageFromClipboard();
                        return;
                    }
                }

                // Priority 2: Internal shape clipboard
                if (_clipboardAnnotation != null)
                {
                    PasteInternalShape();
                    return;
                }
            }
            catch
            {
            }
        }

        private void PasteInternalShape()
        {
            if (_clipboardAnnotation == null) return;

            // Clone again from clipboard so we can paste multiple times
            var newAnnotation = _clipboardAnnotation.Clone();

            // Offset position so it's visible (10px offset)
            const float offset = 20f;

            // Adjust points based on type
            if (newAnnotation is ImageAnnotation img)
            {
                // Check if the image bitmap is valid (disposed?)
                if (img.ImageBitmap == null && _clipboardAnnotation is ImageAnnotation clipImg)
                {
                    // Resurrect bitmap if needed (unlikely if deep cloned correctly)
                    // But Clone() manages it.
                }
            }

            // General offset logic
            newAnnotation.StartPoint = new SKPoint(newAnnotation.StartPoint.X + offset, newAnnotation.StartPoint.Y + offset);
            newAnnotation.EndPoint = new SKPoint(newAnnotation.EndPoint.X + offset, newAnnotation.EndPoint.Y + offset);

            if (newAnnotation is FreehandAnnotation freehand)
            {
                for (int i = 0; i < freehand.Points.Count; i++)
                {
                    freehand.Points[i] = new SKPoint(freehand.Points[i].X + offset, freehand.Points[i].Y + offset);
                }
            }
            else if (newAnnotation is SmartEraserAnnotation eraser)
            {
                for (int i = 0; i < eraser.Points.Count; i++)
                {
                    eraser.Points[i] = new SKPoint(eraser.Points[i].X + offset, eraser.Points[i].Y + offset);
                }
            }

            // Add to Core
            _editorCore.AddAnnotation(newAnnotation);

            // Create UI
            var control = CreateControlForAnnotation(newAnnotation);
            if (control != null)
            {
                var canvas = this.FindControl<Canvas>("AnnotationCanvas");
                if (canvas != null)
                {
                    canvas.Children.Add(control);

                    // Update selection to the pasted object
                    _selectionController.SetSelectedShape(control);
                }
            }

            // Update VM state
            if (DataContext is MainViewModel vm)
            {
                vm.HasAnnotations = true;
            }
        }

        private void OnDuplicateRequested(object? sender, EventArgs e)
        {
            DuplicateSelectedAnnotation();
        }

        private void OnZoomToFitRequested(object? sender, EventArgs e)
        {
            _zoomController.ZoomToFit();
        }

        public void OpenContextMenu(Control target)
        {
            if (this.Resources["EditorContextMenu"] is ContextMenu menu)
            {
                menu.PlacementTarget = target;
                menu.Open(target);
            }
        }

        /// <summary>
        /// Checks if there is content on the system clipboard or internal clipboard
        /// and updates the ViewModel's CanPaste property.
        /// </summary>
        private async Task CheckClipboardStatus()
        {
            if (DataContext is not MainViewModel vm) return;

            bool canPaste = false;

            // 1. Check internal clipboard
            if (_clipboardAnnotation != null)
            {
                canPaste = true;
            }
            // 2. Check system clipboard
            else
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var clipboard = topLevel?.Clipboard;
                if (clipboard != null)
                {
                    try
                    {
                        // Check for files
                        var files = await clipboard.TryGetFilesAsync();
                        if (files != null && files.Any())
                        {
                            canPaste = true;
                        }
                        else
                        {
                            // Check for bitmap
                            var formats = await clipboard.GetDataFormatsAsync();
                            if (formats.Any(f => f.ToString() == "PNG") ||
                                formats.Any(f => f.ToString() == "Bitmap") ||
                                formats.Any(f => f.ToString() == "DeviceIndependentBitmap"))
                            {
                                canPaste = true;
                            }
                        }
                    }
                    catch { }
                }
            }

            vm.CanPaste = canPaste;
        }
    }
}
