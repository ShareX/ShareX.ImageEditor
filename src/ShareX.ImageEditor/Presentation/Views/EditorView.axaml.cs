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
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Core.Editor;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.Controllers;
using ShareX.ImageEditor.Presentation.Controls;
using ShareX.ImageEditor.Presentation.Emoji;
using ShareX.ImageEditor.Presentation.Rendering;
using ShareX.ImageEditor.Presentation.Theming;
using ShareX.ImageEditor.Presentation.ViewModels;
using SkiaSharp;
using System.ComponentModel;

namespace ShareX.ImageEditor.Presentation.Views
{
    public partial class EditorView : UserControl
    {
        private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
        internal const double OverlayCanvasBleed = 24;

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
        private bool _pendingZoomToFitOnOpen;
        private int _pendingZoomToFitRetryCount;
        private int _pendingAutoCopyImageVersion;
        private Rect? _lastOverlayCanvasRect;
        private double _lastOverlayCanvasZoom = -1;
        private EffectBrowserPanel? _effectBrowserPanel;
        private ImageEditorOptions? _effectBrowserPanelOptions;
        private Cursor? _interactionCursorOverride;

        // Window-level key handler reference (so shortcuts work regardless of focus)
        private Window? _parentWindow;
        private readonly ThemeVariantScope? _editorThemeScope;
        private IPlatformSettings? _platformSettings;

        // SIP-CLIPBOARD: Internal clipboard for shape deep-cloning
        private static Annotation? _clipboardAnnotation;

        public EditorView()
        {
            InitializeComponent();
            _editorThemeScope = this.FindControl<ThemeVariantScope>("EditorThemeScope");

            _editorCore = new EditorCore();

            _zoomController = new EditorZoomController(this);
            _selectionController = new EditorSelectionController(this);
            _inputController = new EditorInputController(this, _selectionController, _zoomController);

            // Subscribe to selection controller events
            _selectionController.RequestUpdateEffect += OnRequestUpdateEffect;
            _selectionController.SelectionChanged += OnSelectionChanged;
            LayoutUpdated += OnLayoutUpdated;

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
                    QueueAutoCopyImageToClipboard(vm);
                }
            });

            // Capture wheel events in tunneling phase so ScrollViewer doesn't scroll when using Ctrl+wheel zoom.
            AddHandler(PointerWheelChangedEvent, OnPreviewPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);

            // Enable drag-and-drop for image files
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DropEvent, OnDrop);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);

            DataContextChanged += OnEditorDataContextChanged;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            UpdateOverlayCanvasLayout();
        }

        private void OnCanvasScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            UpdateOverlayCanvasLayout();
        }

        private void UpdateOverlayCanvasLayout()
        {
            var overlayCanvas = this.FindControl<Canvas>("OverlayCanvas");
            var overlayHost = this.FindControl<Canvas>("OverlayHost");
            var canvasContainer = this.FindControl<Grid>("CanvasContainer");
            var vm = DataContext as MainViewModel;

            if (overlayCanvas == null || overlayHost == null || canvasContainer == null)
            {
                return;
            }

            double contentWidth = canvasContainer.Bounds.Width;
            double contentHeight = canvasContainer.Bounds.Height;

            if (contentWidth <= 0 || contentHeight <= 0)
            {
                return;
            }

            var contentOrigin = canvasContainer.TranslatePoint(default, overlayHost);
            if (!contentOrigin.HasValue)
            {
                return;
            }

            double zoom = vm?.Zoom ?? 1;
            var overlayRect = new Rect(
                contentOrigin.Value.X - (OverlayCanvasBleed * zoom),
                contentOrigin.Value.Y - (OverlayCanvasBleed * zoom),
                contentWidth + (OverlayCanvasBleed * 2),
                contentHeight + (OverlayCanvasBleed * 2));

            if (_lastOverlayCanvasRect == overlayRect && Math.Abs(_lastOverlayCanvasZoom - zoom) < 0.0001)
            {
                return;
            }

            _lastOverlayCanvasRect = overlayRect;
            _lastOverlayCanvasZoom = zoom;

            overlayCanvas.Width = overlayRect.Width;
            overlayCanvas.Height = overlayRect.Height;
            Canvas.SetLeft(overlayCanvas, overlayRect.Left);
            Canvas.SetTop(overlayCanvas, overlayRect.Top);
            overlayCanvas.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            overlayCanvas.RenderTransform = new ScaleTransform(zoom, zoom);
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

                    // Don't sync stroke properties from ImageAnnotation, effect annotations,
                    // or SmartEraser. They use nonstandard option semantics and would
                    // clobber the shared defaults for other tools.
                    if (vm.SelectedAnnotation is not ImageAnnotation
                        && vm.SelectedAnnotation is not BaseEffectAnnotation
                        && vm.SelectedAnnotation is not SmartEraserAnnotation)
                    {
                        vm.SelectedColor = vm.SelectedAnnotation.StrokeColor;
                        vm.StrokeWidth = (int)vm.SelectedAnnotation.StrokeWidth;
                        vm.ShadowEnabled = vm.SelectedAnnotation.ShadowEnabled;
                    }

                    if (vm.SelectedAnnotation is NumberAnnotation num)
                    {
                        vm.FontSize = num.FontSize;
                        vm.FillColor = num.FillColor;
                        if (!string.IsNullOrEmpty(num.TextColor))
                            vm.TextColorValue = Avalonia.Media.Color.Parse(num.TextColor);
                    }
                    else if (vm.SelectedAnnotation is TextAnnotation text)
                    {
                        vm.FontSize = text.FontSize;
                        vm.TextBold = text.IsBold;
                        vm.TextItalic = text.IsItalic;
                        vm.TextUnderline = text.IsUnderline;
                        if (!string.IsNullOrEmpty(text.TextColor))
                            vm.TextColorValue = Avalonia.Media.Color.Parse(text.TextColor);
                    }
                    else if (vm.SelectedAnnotation is SpeechBalloonAnnotation balloon)
                    {
                        vm.FontSize = balloon.FontSize;
                        vm.FillColor = balloon.FillColor;
                        vm.CornerRadius = balloon.CornerRadius;
                        if (!string.IsNullOrEmpty(balloon.TextColor))
                            vm.TextColorValue = Avalonia.Media.Color.Parse(balloon.TextColor);
                    }
                    else if (vm.SelectedAnnotation is RectangleAnnotation rect && vm.SelectedAnnotation is not SmartEraserAnnotation)
                    {
                        vm.FillColor = rect.FillColor;
                        vm.CornerRadius = rect.CornerRadius;
                    }
                    else if (vm.SelectedAnnotation is EllipseAnnotation ellipse)
                    {
                        vm.FillColor = ellipse.FillColor;
                    }
                    else if (vm.SelectedAnnotation is BaseEffectAnnotation effect)
                    {
                        vm.EffectStrength = (int)effect.Amount;
                        if (effect is HighlightAnnotation highlight)
                        {
                            vm.FillColor = highlight.FillColor;
                        }
                    }
                }
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            ThemeManager.ThemeChanged += OnThemeChanged;

            // Check clipboard initially
            _ = CheckClipboardStatus();

            // Attach key handlers to the parent Window so shortcuts work
            // regardless of which child control has focus (buttons, dropdowns, etc.).
            _parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (_parentWindow != null)
            {
                _parentWindow.KeyDown += OnKeyDown;
                _parentWindow.KeyUp += OnKeyUp;
                _parentWindow.Activated += OnWindowActivated;
            }

            // Give the editor initial focus
            this.Focus();

            if (DataContext is MainViewModel vm)
            {
                vm.AttachEditorCore(_editorCore);
                _editorCore.ActiveTool = vm.ActiveTool;
                HookAnnotationToolbarEvents();

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
                vm.FlattenRequested += OnFlattenRequested;
                vm.EmojiInsertionRequested += OnEmojiInsertionRequested;

                // File menu event handlers (Image Editor Mode)
                vm.NewImageRequested += OnNewImageRequested;
                vm.OpenImageRequested += OnOpenImageRequested;
                vm.StartScreenRequested += OnStartScreenRequested;
                vm.LoadFromClipboardRequested += OnLoadFromClipboardRequested;
                vm.LoadFromUrlRequested += OnLoadFromUrlRequested;
                vm.LoadRecentFileRequested += OnLoadRecentFileRequested;
                vm.CopyRequested += OnCopyImageRequested;
                vm.SaveRequested += OnSaveRequested;
                vm.SaveAsRequested += OnSaveAsRequested;

                // Original code subscribed to vm.PropertyChanged
                vm.PropertyChanged += OnViewModelPropertyChanged;

                // Initialize zoom
                _zoomController.InitLastZoom(vm.Zoom);
                UpdateCursorForTool();

                // Wire up View interactions
                vm.DeselectRequested += OnDeselectRequested;
                vm.CanvasFocusRequested += OnCanvasFocusRequested;

                // Initial load
                if (vm.PreviewImage != null)
                {
                    bool isInitialImageLoad = _editorCore.SourceImage == null;
                    LoadImageFromViewModel(vm);
                    if (isInitialImageLoad)
                    {
                        QueueAutoCopyImageToClipboard(vm);
                    }
                }
                else if (vm.ShowStartScreen)
                {
                    // No image loaded � show the start screen dialog
                    vm.RequestStartScreen();
                }

                // Reset dirty flag after initial load � loading the image fires HistoryChanged
                // and OnPreviewImageChanged which both set IsDirty=true as a side-effect.
                vm.IsDirty = false;
            }

            RefreshPlatformColorTracking();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            ThemeManager.ThemeChanged -= OnThemeChanged;

            if (_parentWindow != null)
            {
                _parentWindow.KeyDown -= OnKeyDown;
                _parentWindow.KeyUp -= OnKeyUp;
                _parentWindow.Activated -= OnWindowActivated;
            }

            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                vm.DeselectRequested -= OnDeselectRequested;
                vm.ZoomToFitRequested -= OnZoomToFitRequested;
                vm.NewImageRequested -= OnNewImageRequested;
                vm.OpenImageRequested -= OnOpenImageRequested;
                vm.StartScreenRequested -= OnStartScreenRequested;
                vm.LoadFromClipboardRequested -= OnLoadFromClipboardRequested;
                vm.LoadFromUrlRequested -= OnLoadFromUrlRequested;
                vm.LoadRecentFileRequested -= OnLoadRecentFileRequested;
                vm.SaveRequested -= OnSaveRequested;
                vm.SaveAsRequested -= OnSaveAsRequested;
                vm.EmojiInsertionRequested -= OnEmojiInsertionRequested;
            }

            UnhookAnnotationToolbarEvents();
            _selectionController.RequestUpdateEffect -= OnRequestUpdateEffect;
            ClearEffectPreviewCache();
            SetPlatformSettings(null);
        }

        private void OnEditorDataContextChanged(object? sender, EventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            RefreshPlatformColorTracking();
        }

        private void OnThemeChanged(object? sender, ThemeVariant theme)
        {
            Dispatcher.UIThread.Post(() => ApplyTheme(theme));
        }

        private void RefreshPlatformColorTracking()
        {
            if (!ShouldListenToPlatformColorChanges())
            {
                SetPlatformSettings(null);
            }
            else
            {
                SetPlatformSettings(this.GetPlatformSettings() ?? Application.Current?.PlatformSettings);
            }

            PlatformColorValues? colorValues = _platformSettings?.GetColorValues()
                ?? this.GetPlatformSettings()?.GetColorValues()
                ?? Application.Current?.PlatformSettings?.GetColorValues();

            UpdateTheme(colorValues);
            UpdateAccentColor(colorValues);
        }

        private bool ShouldUseSystemTheme()
        {
            return DataContext is MainViewModel { Options.UseSystemTheme: true };
        }

        private bool ShouldUseSystemAccentColor()
        {
            return DataContext is MainViewModel { Options.UseSystemAccentColor: true };
        }

        private bool ShouldListenToPlatformColorChanges()
        {
            return ShouldUseSystemTheme() || ShouldUseSystemAccentColor();
        }

        private void SetPlatformSettings(IPlatformSettings? platformSettings)
        {
            if (ReferenceEquals(_platformSettings, platformSettings))
            {
                return;
            }

            if (_platformSettings != null)
            {
                _platformSettings.ColorValuesChanged -= OnPlatformColorValuesChanged;
            }

            _platformSettings = platformSettings;

            if (_platformSettings != null)
            {
                _platformSettings.ColorValuesChanged += OnPlatformColorValuesChanged;
            }
        }

        private void OnPlatformColorValuesChanged(object? sender, PlatformColorValues colorValues)
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateTheme(colorValues);
                UpdateAccentColor(colorValues);
            });
        }

        private void UpdateTheme(PlatformColorValues? colorValues = null)
        {
            if (ShouldUseSystemTheme())
            {
                ApplyTheme(MapSystemTheme(colorValues));
                return;
            }

            ApplyTheme(ThemeManager.GetCurrentTheme());
        }

        private void ApplyTheme(ThemeVariant theme)
        {
            if (_editorThemeScope != null)
            {
                _editorThemeScope.RequestedThemeVariant = theme;
            }
        }

        private ThemeVariant MapSystemTheme(PlatformColorValues? colorValues)
        {
            if (colorValues != null)
            {
                return IsLightTheme(colorValues.ThemeVariant.ToString())
                    ? ThemeManager.ShareXLight
                    : ThemeManager.ShareXDark;
            }

            ThemeVariant hostTheme = TopLevel.GetTopLevel(this)?.ActualThemeVariant
                ?? Application.Current?.ActualThemeVariant
                ?? ThemeVariant.Default;

            return IsLightTheme(hostTheme.ToString())
                ? ThemeManager.ShareXLight
                : ThemeManager.ShareXDark;
        }

        private static bool IsLightTheme(string? themeName)
        {
            return !string.IsNullOrWhiteSpace(themeName) &&
                themeName.Contains("Light", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateAccentColor(PlatformColorValues? colorValues = null)
        {
            if (!ShouldUseSystemAccentColor())
            {
                return;
            }

            colorValues ??= _platformSettings?.GetColorValues()
                ?? this.GetPlatformSettings()?.GetColorValues()
                ?? Application.Current?.PlatformSettings?.GetColorValues();

            if (colorValues == null || colorValues.AccentColor1.A == 0)
            {
                return;
            }

            Color startColor = colorValues.AccentColor1;
            Color endColor = DarkenColor(startColor, 0.10);
            Color foregroundColor = GetAccentForegroundColor(startColor, endColor);

            Resources["ShareX.Color.Accent.Start"] = startColor;
            Resources["ShareX.Color.Accent.End"] = endColor;
            Resources["ShareX.Color.Accent.Foreground"] = foregroundColor;

            UpdateAccentBrush(ThemeManager.ShareXDark, startColor, endColor);
            UpdateAccentBrush(ThemeManager.ShareXLight, startColor, endColor);
            UpdateAccentForegroundBrush(ThemeManager.ShareXDark, foregroundColor);
            UpdateAccentForegroundBrush(ThemeManager.ShareXLight, foregroundColor);
        }

        private void UpdateAccentBrush(Avalonia.Styling.ThemeVariant theme, Color startColor, Color endColor)
        {
            if (!Resources.TryGetResource("ShareX.Brush.Accent", theme, out object? accentBrushValue) ||
                accentBrushValue is not LinearGradientBrush accentBrush)
            {
                return;
            }

            accentBrush.StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative);
            accentBrush.EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative);

            GradientStops gradientStops = accentBrush.GradientStops;

            while (gradientStops.Count < 2)
            {
                gradientStops.Add(new GradientStop());
            }

            while (gradientStops.Count > 2)
            {
                gradientStops.RemoveAt(gradientStops.Count - 1);
            }

            gradientStops[0].Color = startColor;
            gradientStops[0].Offset = 0;
            gradientStops[1].Color = endColor;
            gradientStops[1].Offset = 1;
        }

        private void UpdateAccentForegroundBrush(Avalonia.Styling.ThemeVariant theme, Color foregroundColor)
        {
            if (!Resources.TryGetResource("ShareX.Brush.Accent.Foreground", theme, out object? accentForegroundBrushValue) ||
                accentForegroundBrushValue is not SolidColorBrush accentForegroundBrush)
            {
                return;
            }

            accentForegroundBrush.Color = foregroundColor;
        }

        private Color GetAccentForegroundColor(Color startColor, Color endColor)
        {
            Color lightForeground = GetThemeColor(
                ThemeManager.ShareXDark,
                "ShareX.Color.Text",
                Color.Parse("#D8DADB"));

            Color darkForeground = GetThemeColor(
                ThemeManager.ShareXLight,
                "ShareX.Color.Text",
                Color.Parse("#4E4E4E"));

            double darkSwitchRatio = GetResourceDouble(
                "ShareX.Value.Accent.Foreground.DarkSwitchRatio",
                1.75);

            double lightContrast = Math.Min(
                GetContrastRatio(lightForeground, startColor),
                GetContrastRatio(lightForeground, endColor));

            double darkContrast = Math.Min(
                GetContrastRatio(darkForeground, startColor),
                GetContrastRatio(darkForeground, endColor));

            return darkContrast >= lightContrast * darkSwitchRatio
                ? darkForeground
                : lightForeground;
        }

        private Color GetThemeColor(Avalonia.Styling.ThemeVariant theme, string resourceKey, Color fallback)
        {
            if (!Resources.TryGetResource(resourceKey, theme, out object? resourceValue))
            {
                return fallback;
            }

            return resourceValue switch
            {
                Color color => color,
                SolidColorBrush brush => brush.Color,
                _ => fallback
            };
        }

        private double GetResourceDouble(string resourceKey, double fallback)
        {
            if (!Resources.TryGetResource(resourceKey, ActualThemeVariant, out object? resourceValue))
            {
                return fallback;
            }

            return resourceValue switch
            {
                double value => value,
                float value => value,
                decimal value => (double)value,
                int value => value,
                long value => value,
                _ => fallback
            };
        }

        private static double GetContrastRatio(Color firstColor, Color secondColor)
        {
            double firstLuminance = GetRelativeLuminance(firstColor);
            double secondLuminance = GetRelativeLuminance(secondColor);

            double lighter = Math.Max(firstLuminance, secondLuminance);
            double darker = Math.Min(firstLuminance, secondLuminance);

            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double GetRelativeLuminance(Color color)
        {
            double red = LinearizeColorChannel(color.R);
            double green = LinearizeColorChannel(color.G);
            double blue = LinearizeColorChannel(color.B);

            return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
        }

        private static double LinearizeColorChannel(byte channel)
        {
            double normalized = channel / 255.0;

            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        private static Color DarkenColor(Color color, double amount)
        {
            double factor = Math.Clamp(1 - amount, 0, 1);

            return Color.FromArgb(
                color.A,
                (byte)Math.Clamp((int)Math.Round(color.R * factor), 0, byte.MaxValue),
                (byte)Math.Clamp((int)Math.Round(color.G * factor), 0, byte.MaxValue),
                (byte)Math.Clamp((int)Math.Round(color.B * factor), 0, byte.MaxValue));
        }

        private void OnWindowActivated(object? sender, EventArgs e)
        {
            _ = CheckClipboardStatus();
        }

        private void OnEffectBrowserOverlayPointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.IsEffectBrowserVisible)
            {
                if (vm.CloseEffectsPanelCommand.CanExecute(null))
                {
                    vm.CloseEffectsPanelCommand.Execute(null);
                }
            }
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
                else if (e.PropertyName == nameof(MainViewModel.FillColorValue))
                {
                    ApplySelectedFillColor(vm.FillColor);
                }
                else if (e.PropertyName == nameof(MainViewModel.TextColorValue))
                {
                    ApplySelectedTextColor(vm.TextColor);
                }
                else if (e.PropertyName == nameof(MainViewModel.CornerRadius))
                {
                    ApplySelectedCornerRadius(vm.CornerRadius);
                }
                else if (e.PropertyName == nameof(MainViewModel.PreviewImage))
                {
                    bool isInitialImageLoad = vm.PreviewImage != null && _editorCore.SourceImage == null;
                    ClearEffectPreviewCache();
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

                    if (isInitialImageLoad)
                    {
                        QueueAutoCopyImageToClipboard(vm);
                    }
                }
                else if (e.PropertyName == nameof(MainViewModel.Zoom))
                {
                    _zoomController.HandleZoomPropertyChanged(vm);
                }
                else if (e.PropertyName == nameof(MainViewModel.ActiveTool))
                {
                    _editorCore.ActiveTool = vm.ActiveTool;

                    if (vm.ActiveTool == EditorTool.Crop)
                    {
                        _inputController.ActivateCropToFullImage();
                        this.Focus();
                    }
                    else
                    {
                        _inputController.CancelCrop();
                    }
                    _selectionController.ClearSelection();
                    UpdateCursorForTool(); // ISSUE-018 fix: Update cursor feedback for active tool
                }
                else if (e.PropertyName == nameof(MainViewModel.IsEffectBrowserVisible))
                {
                    if (vm.IsEffectBrowserVisible)
                    {
                        EnsureEffectBrowserPanel(vm).FocusSearchBox();
                    }
                }
            }
        }

        private EffectBrowserPanel EnsureEffectBrowserPanel(MainViewModel vm)
        {
            if (_effectBrowserPanel == null)
            {
                _effectBrowserPanel = new EffectBrowserPanel();
                _effectBrowserPanel.EffectDialogRequested += OnEffectDialogRequested;

                var effectBrowserHost = this.FindControl<ContentControl>("EffectBrowserHost");
                if (effectBrowserHost != null)
                {
                    effectBrowserHost.Content = _effectBrowserPanel;
                }
            }

            if (!ReferenceEquals(_effectBrowserPanelOptions, vm.Options))
            {
                _effectBrowserPanel.SetOptions(vm.Options);
                _effectBrowserPanelOptions = vm.Options;
            }

            return _effectBrowserPanel;
        }

        /// <summary>
        /// ISSUE-018 fix: Updates the editor canvas cursor based on the active tool.
        /// The overlay canvas sits on top of the annotation canvas, so both must stay in sync.
        /// </summary>
        private void UpdateCursorForTool()
        {
            if (DataContext is not MainViewModel vm) return;

            if (_interactionCursorOverride != null)
            {
                if (_selectionController.IsInteractionActive || _zoomController.IsPanning || _inputController.IsCropInteractionActive)
                {
                    ApplyInteractionCursor(_interactionCursorOverride);
                }
                else
                {
                    _interactionCursorOverride = null;
                    HideInteractionCaptureLayer();
                }
            }

            var annotationCanvas = this.FindControl<Canvas>("AnnotationCanvas");
            var overlayCanvas = this.FindControl<Canvas>("OverlayCanvas");
            if (annotationCanvas == null && overlayCanvas == null) return;

            Cursor cursor = vm.ActiveTool switch
            {
                EditorTool.Select => ArrowCursor,
                EditorTool.Crop or EditorTool.CutOut => CursorAssetLoader.GetCrosshairCursor(),
                _ => CursorAssetLoader.GetCrosshairCursor() // Drawing tools (Rectangle, Ellipse, Pen, etc.)
            };

            if (annotationCanvas != null)
            {
                annotationCanvas.Cursor = cursor;
            }

            if (overlayCanvas != null)
            {
                overlayCanvas.Cursor = cursor;
            }
        }

        internal void ApplyInteractionCursor(Cursor cursor)
        {
            _interactionCursorOverride = cursor;
            var interactionLayer = this.FindControl<Border>("InteractionCaptureLayer");
            if (interactionLayer != null)
            {
                interactionLayer.Cursor = cursor;
                interactionLayer.IsHitTestVisible = true;
                interactionLayer.IsVisible = true;
            }
        }

        internal void BeginInteractionCursorCapture(IPointer pointer, Cursor cursor)
        {
            ApplyInteractionCursor(cursor);

            var interactionLayer = this.FindControl<Border>("InteractionCaptureLayer");
            if (interactionLayer != null)
            {
                pointer.Capture(interactionLayer);
            }
        }

        internal void RestoreEditorSurfaceCursorForActiveTool()
        {
            if (_selectionController.IsInteractionActive || _zoomController.IsPanning || _inputController.IsCropInteractionActive)
            {
                return;
            }

            _interactionCursorOverride = null;
            HideInteractionCaptureLayer();
            UpdateCursorForTool();
        }

        private void HideInteractionCaptureLayer()
        {
            var interactionLayer = this.FindControl<Border>("InteractionCaptureLayer");
            if (interactionLayer != null)
            {
                interactionLayer.IsHitTestVisible = false;
                interactionLayer.IsVisible = false;
                interactionLayer.Cursor = ArrowCursor;
            }
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

                using var skBitmap = !vm.IsEffectPreviewActive
                    ? vm.CreateSourceImageCopyForCore() ?? BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage)
                    : BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
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
                    QueueZoomToFitOnOpenIfNeeded(vm);
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

            using var skBitmap = !vm.IsEffectPreviewActive
                ? vm.CreateSourceImageCopyForCore() ?? BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage)
                : BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
            if (skBitmap != null)
            {
                _skipNextCoreImageChanged = true;
                _editorCore.UpdateSourceImage(skBitmap.Copy());
                _canvasControl.Initialize(skBitmap.Width, skBitmap.Height);
                RenderCore();
            }
        }

        private void QueueZoomToFitOnOpenIfNeeded(MainViewModel vm)
        {
            if (!vm.ConsumeZoomToFitOnNextImageLoad())
            {
                return;
            }

            _pendingZoomToFitOnOpen = true;
            _pendingZoomToFitRetryCount = 4;
            TryApplyPendingZoomToFitOnOpen();
        }

        private void TryApplyPendingZoomToFitOnOpen()
        {
            if (!_pendingZoomToFitOnOpen)
            {
                return;
            }

            if (_zoomController.ZoomToFit())
            {
                _pendingZoomToFitOnOpen = false;
                return;
            }

            if (_pendingZoomToFitRetryCount-- <= 0)
            {
                _pendingZoomToFitOnOpen = false;
                return;
            }

            Dispatcher.UIThread.Post(TryApplyPendingZoomToFitOnOpen, DispatcherPriority.Render);
        }

        private void QueueAutoCopyImageToClipboard(MainViewModel vm)
        {
            if (!vm.Options.AutoCopyImageToClipboard || !vm.HasPreviewImage)
            {
                return;
            }

            int version = ++_pendingAutoCopyImageVersion;

            Dispatcher.UIThread.Post(async () =>
            {
                if (version != _pendingAutoCopyImageVersion)
                {
                    return;
                }

                AutoCopyImageToClipboard(vm);
            }, DispatcherPriority.Background);
        }

        private void AutoCopyImageToClipboard(MainViewModel vm)
        {
            if (!vm.Options.AutoCopyImageToClipboard || !vm.HasPreviewImage)
            {
                return;
            }

            try
            {
                vm.RequestCopyToClipboard();
            }
            catch (Exception ex)
            {
                EditorServices.ReportWarning(nameof(EditorView), "Failed to raise auto-copy image request.", ex);
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

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // Skip shortcuts when the user is typing in a text field
            if (_parentWindow?.FocusManager?.GetFocusedElement() is TextBox) return;

            // Skip shortcuts when a modal dialog is open (e.g. emoji picker search box)
            if (DataContext is MainViewModel { IsModalOpen: true }) return;

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
                        case Key.F: vm.FlattenImageCommand.Execute(null); e.Handled = true; break;
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
                        case Key.C:
                            if (vm.CopyAnnotationCommand.CanExecute(null))
                                vm.CopyAnnotationCommand.Execute(null);
                            else
                                vm.CopyCommand.Execute(null);
                            e.Handled = true;
                            break;
                        case Key.V: vm.PasteCommand.Execute(null); e.Handled = true; break;
                        case Key.D: DuplicateSelectedAnnotation(); e.Handled = true; break;
                        case Key.S: vm.SaveCommand.Execute(null); e.Handled = true; break;
                        case Key.N: vm.NewImageCommand.Execute(null); e.Handled = true; break;
                        case Key.O: vm.OpenImageCommand.Execute(null); e.Handled = true; break;
                        case Key.P:
                            if (vm.PinToScreenCommand.CanExecute(null))
                            {
                                vm.PinToScreenCommand.Execute(null);
                                e.Handled = true;
                            }
                            break;
                        case Key.U:
                            if (vm.UploadCommand.CanExecute(null))
                            {
                                vm.UploadCommand.Execute(null);
                                e.Handled = true;
                            }
                            break;
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
                            case Key.J: vm.SelectToolCommand.Execute(EditorTool.Emoji); e.Handled = true; break;
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
                                if (_inputController.TryConfirmCrop())
                                {
                                    e.Handled = true;
                                }
                                else if (vm.UseContinueWorkflow)
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

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (DataContext is MainViewModel vm && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
            {
                // Close emoji modal dialog on Escape (before TextBox short-circuit)
                if (vm.IsModalOpen)
                {
                    vm.CloseModalCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Close image effects panel on Escape (before TextBox short-circuit)
                // Covers both the effects browser (EffectsPanelContent == null) and specific
                // effect dialogs (EffectsPanelContent != null) to prevent Esc from falling
                // through to the editor-close path when any effects panel state is active.
                if (vm.IsEffectsPanelOpen)
                {
                    vm.CloseEffectsPanelCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            // Skip shortcuts when the user is typing in a text field
            if (_parentWindow?.FocusManager?.GetFocusedElement() is TextBox) return;

            if (DataContext is MainViewModel vm2 && e.KeyModifiers == KeyModifiers.None)
            {
                switch (e.Key)
                {
                    case Key.Escape:
                        if (_inputController.CancelCrop())
                        {
                            e.Handled = true;
                        }
                        else if (_selectionController.SelectedShape != null)
                        {
                            _selectionController.ClearSelection();
                            e.Handled = true;
                        }
                        else if (vm2.UseContinueWorkflow)
                        {
                            vm2.CancelCommand.Execute(null);
                            e.Handled = true;
                        }
                        else
                        {
                            vm2.ExitEditorCommand.Execute(null);
                            e.Handled = true;
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
            _inputController.CancelCrop();
            _selectionController.ClearSelection();
        }

        private void OnCanvasFocusRequested(object? sender, EventArgs e)
        {
            this.Focus();
        }

        private Color SKColorToAvalonia(SKColor color)
        {
            return Color.FromUInt32((uint)color);
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
                    RefreshSpotlightOverlay();

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
                RefreshSpotlightOverlay();
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

        // --- Crop and Image Insertion ---

        public void PerformCrop()
        {
            var cropOverlay = this.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
            if (cropOverlay != null && cropOverlay.IsVisible && DataContext is MainViewModel vm)
            {
                var rect = new SkiaSharp.SKRect(
                    (float)(Canvas.GetLeft(cropOverlay) - OverlayCanvasBleed),
                    (float)(Canvas.GetTop(cropOverlay) - OverlayCanvasBleed),
                    (float)(Canvas.GetLeft(cropOverlay) - OverlayCanvasBleed + cropOverlay.Width),
                    (float)(Canvas.GetTop(cropOverlay) - OverlayCanvasBleed + cropOverlay.Height));

                if (rect.Width > 0 && rect.Height > 0)
                {
                    // Canvas coordinates are already in image-pixel space (AnnotationCanvas
                    // is sized to CanvasSize = bitmap.Width/Height). No DPI scaling needed.
                    var cropX = (int)Math.Round(rect.Left);
                    var cropY = (int)Math.Round(rect.Top);
                    var cropW = (int)Math.Round(rect.Width);
                    var cropH = (int)Math.Round(rect.Height);

                    _editorCore.Crop(new SKRect(cropX, cropY, cropX + cropW, cropY + cropH));
                }
                cropOverlay.IsVisible = false;
            }
        }

        // --- Image Paste & Drag-Drop ---

        /// <summary>
        /// Inserts an image annotation from an SKBitmap at an optional drop position.
        /// Adds the annotation to both the Avalonia canvas and EditorCore, then switches to Select tool.
        /// </summary>
        /// <remarks>
        /// XIP0039 Guardrail 6: This method is public so host applications can insert image annotations
        /// directly without resorting to reflection. The previous private access required callers such as
        /// <c>MainWindow.axaml.cs</c> to use <c>BindingFlags.NonPublic</c> reflection.
        /// </remarks>
        public void InsertImageAnnotation(SKBitmap skBitmap, Point? dropPosition = null)
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

        private void InsertEmojiAnnotation(string unicodeSequence, string displayName, Point? dropPosition = null)
        {
            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas == null || DataContext is not MainViewModel vm)
            {
                return;
            }

            const int defaultSize = 160;

            var posX = dropPosition?.X ?? (_editorCore.CanvasSize.Width / 2 - defaultSize / 2.0);
            var posY = dropPosition?.Y ?? (_editorCore.CanvasSize.Height / 2 - defaultSize / 2.0);

            var annotation = new EmojiAnnotation
            {
                UnicodeSequence = unicodeSequence,
                DisplayName = displayName,
                StartPoint = new SKPoint((float)posX, (float)posY),
                EndPoint = new SKPoint((float)(posX + defaultSize), (float)(posY + defaultSize))
            };

            var control = CreateControlForAnnotation(annotation);
            if (control == null)
            {
                return;
            }

            canvas.Children.Add(control);
            _editorCore.AddAnnotation(annotation);
            vm.HasAnnotations = true;
            vm.ActiveTool = EditorTool.Select;
            _selectionController.SetSelectedShape(control);
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
                            catch (Exception ex)
                            {
                                EditorServices.ReportWarning(nameof(EditorView), $"Failed to decode dropped image '{file.Name}'.", ex);
                            }
                        }
                    }
                }
            }
        }

        private async void OnBrowseBackgroundImageClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null)
            {
                return;
            }

            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select background image",
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            });

            if (files.Count > 0)
            {
                vm.SetBackgroundImagePath(files[0].Path.LocalPath);
            }
        }

        private void OnNewImageRequested(object? sender, EventArgs e)
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            var dialog = new NewImageDialogViewModel(
                onOk: (result) =>
                {
                    vm.IsModalOpen = false;

                    var color = result.Transparent ? SKColors.Transparent :
                        new SKColor(result.BackgroundColor.R, result.BackgroundColor.G, result.BackgroundColor.B, result.BackgroundColor.A);

                    var skBitmap = new SKBitmap(new SKImageInfo(result.Width, result.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                    using var canvas = new SKCanvas(skBitmap);
                    canvas.Clear(color);

                    // Clear annotation visuals
                    var annotationCanvas = this.FindControl<Canvas>("AnnotationCanvas");
                    annotationCanvas?.Children.Clear();
                    RefreshSpotlightOverlay();
                    _selectionController.ClearSelection();

                    // Load fresh image into core (clears history and annotations)
                    _skipNextCoreImageChanged = true;
                    _editorCore.LoadImage(skBitmap);

                    // Initialize canvas control
                    _canvasControl?.Initialize(skBitmap.Width, skBitmap.Height);
                    RenderCore();

                    // Sync to VM
                    try
                    {
                        _isSyncingToVM = true;
                        vm.ImageFilePath = null;
                        vm.IsDirty = false;
                        vm.HasAnnotations = false;
                        vm.UpdateCoreHistoryState(_editorCore.CanUndo, _editorCore.CanRedo);

                        using var image = SKImage.FromBitmap(skBitmap);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using var stream = new MemoryStream(data.ToArray());
                        vm.PreviewImage = new Avalonia.Media.Imaging.Bitmap(stream);
                    }
                    finally
                    {
                        _isSyncingToVM = false;
                    }
                },
                onCancel: () =>
                {
                    vm.IsModalOpen = false;
                }
            );

            vm.ModalContent = dialog;
            vm.IsModalOpen = true;
        }

        private async void OnOpenImageRequested(object? sender, EventArgs e)
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null)
            {
                return;
            }

            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open image",
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            });

            if (files.Count > 0)
            {
                using var stream = await files[0].OpenReadAsync();
                using var memStream = new MemoryStream();
                await stream.CopyToAsync(memStream);
                memStream.Position = 0;

                var skBitmap = SKBitmap.Decode(memStream);
                if (skBitmap == null) return;

                LoadBitmapIntoEditor(vm, skBitmap, files[0].Path.LocalPath);
            }
        }

        private void OnStartScreenRequested(object? sender, EventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            EnsureStartScreenDialog(vm);
        }

        private StartScreenDialogViewModel EnsureStartScreenDialog(MainViewModel vm)
        {
            if (vm.ModalContent is StartScreenDialogViewModel existingDialog)
            {
                vm.IsModalOpen = true;
                return existingDialog;
            }

            StartScreenDialogViewModel? dialog = null;

            dialog = new StartScreenDialogViewModel(
                recentFiles: vm.RecentImageFiles,
                onNewImage: () =>
                {
                    vm.CloseModalCommand.Execute(null);
                    vm.NewImageCommand.Execute(null);
                },
                onOpenFile: () =>
                {
                    vm.CloseModalCommand.Execute(null);
                    vm.OpenImageCommand.Execute(null);
                },
                onLoadFromClipboard: () =>
                {
                    vm.RequestLoadFromClipboard();
                },
                onShowUrlInput: () =>
                {
                    if (dialog != null)
                    {
                        _ = PrepareStartScreenUrlInputAsync(dialog);
                    }
                },
                onSubmitUrl: url =>
                {
                    vm.RequestLoadFromUrl(url);
                },
                onClose: () =>
                {
                    vm.CloseModalCommand.Execute(null);
                },
                onExit: () =>
                {
                    vm.CloseModalCommand.Execute(null);
                    vm.ExitEditorCommand.Execute(null);
                },
                onOpenRecentFile: path =>
                {
                    vm.RequestLoadRecentFile(path);
                });

            vm.ModalContent = dialog;
            vm.IsModalOpen = true;

            return dialog;
        }

        private async Task PrepareStartScreenUrlInputAsync(StartScreenDialogViewModel dialog)
        {
            string? clipboardUrl = null;

            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    var text = await topLevel.Clipboard.TryGetTextAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        text = text.Trim();
                        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                        {
                            clipboardUrl = text;
                        }
                    }
                }
            }
            catch
            {
                // Ignore clipboard read errors while preparing the inline URL entry.
            }

            dialog.ShowUrlInput(clipboardUrl);
        }

        private void ShowStartScreenStatus(MainViewModel vm, string message)
        {
            var dialog = EnsureStartScreenDialog(vm);
            dialog.ShowStatus(message);
        }

        private async void OnLoadFromClipboardRequested(object? sender, EventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard == null)
            {
                ShowStartScreenStatus(vm, "Failed to load image from clipboard.");
                return;
            }

            try
            {
                var clipboard = topLevel.Clipboard;

                // Try to get bitmap from clipboard
                var clipboardBitmap = await clipboard.TryGetBitmapAsync();
                if (clipboardBitmap != null)
                {
                    using var ms = new MemoryStream();
                    clipboardBitmap.Save(ms);
                    (clipboardBitmap as IDisposable)?.Dispose();
                    ms.Position = 0;

                    var skBitmap = SKBitmap.Decode(ms);
                    if (skBitmap != null)
                    {
                        vm.CloseModalCommand.Execute(null);
                        LoadBitmapIntoEditor(vm, skBitmap, null);
                        return;
                    }
                }

                // Try files
                var files = await clipboard.TryGetFilesAsync();
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        if (file is not IStorageFile storageFile) continue;

                        try
                        {
                            using var stream = await storageFile.OpenReadAsync();
                            using var memStream = new MemoryStream();
                            await stream.CopyToAsync(memStream);
                            memStream.Position = 0;

                            var skBitmap = SKBitmap.Decode(memStream);
                            if (skBitmap != null)
                            {
                                vm.CloseModalCommand.Execute(null);
                                LoadBitmapIntoEditor(vm, skBitmap, storageFile.Path.LocalPath);
                                return;
                            }
                        }
                        catch
                        {
                            // Try next file
                        }
                    }
                }

                ShowStartScreenStatus(vm, "Failed to load image from clipboard.\nClipboard does not contain an image.");
            }
            catch (Exception ex)
            {
                EditorServices.ReportError(nameof(EditorView), "Failed to load image from clipboard.", ex);
                ShowStartScreenStatus(vm, "Failed to load image from clipboard.");
            }
        }

        private async void OnLoadFromUrlRequested(object? sender, string url)
        {
            if (DataContext is not MainViewModel vm) return;

            StartScreenDialogViewModel? startScreenDialog = vm.ModalContent as StartScreenDialogViewModel;
            startScreenDialog?.ClearStatus();
            startScreenDialog?.SetUrlLoading(true);

            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "ShareX");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var memStream = new MemoryStream();
                await stream.CopyToAsync(memStream);
                memStream.Position = 0;

                var skBitmap = SKBitmap.Decode(memStream);
                if (skBitmap == null)
                {
                    startScreenDialog?.SetUrlLoading(false);
                    startScreenDialog?.ShowStatus("The URL does not point to a valid image.");
                    return;
                }

                vm.CloseModalCommand.Execute(null);
                LoadBitmapIntoEditor(vm, skBitmap, null);
            }
            catch (Exception ex)
            {
                startScreenDialog?.SetUrlLoading(false);
                startScreenDialog?.ShowStatus($"Failed to download image: {ex.Message}");
            }
        }

        private void OnLoadRecentFileRequested(object? sender, string filePath)
        {
            if (DataContext is not MainViewModel vm) return;

            if (!File.Exists(filePath))
            {
                vm.RemoveRecentImageFile(filePath);
                if (vm.ModalContent is StartScreenDialogViewModel startScreenDialog)
                {
                    startScreenDialog.RecentFiles.Remove(filePath);
                }
                ShowStartScreenStatus(vm, $"The file no longer exists.\n{filePath}");
                return;
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                var skBitmap = SKBitmap.Decode(stream);
                if (skBitmap == null)
                {
                    EditorServices.ReportError(nameof(EditorView), $"Failed to decode image file '{filePath}'.");
                    ShowStartScreenStatus(vm, $"Failed to load image file.\n{filePath}");
                    return;
                }

                vm.CloseModalCommand.Execute(null);
                LoadBitmapIntoEditor(vm, skBitmap, filePath);
            }
            catch (Exception ex)
            {
                EditorServices.ReportError(nameof(EditorView), $"Failed to load image file '{filePath}'.", ex);
                ShowStartScreenStatus(vm, $"Failed to load image file.\n{filePath}");
            }
        }

        private void LoadBitmapIntoEditor(MainViewModel vm, SKBitmap skBitmap, string? filePath)
        {
            // Clear annotation visuals
            var annotationCanvas = this.FindControl<Canvas>("AnnotationCanvas");
            annotationCanvas?.Children.Clear();
            RefreshSpotlightOverlay();
            _selectionController.ClearSelection();

            // Load fresh image into core (clears history and annotations)
            _skipNextCoreImageChanged = true;
            _editorCore.LoadImage(skBitmap);

            // Initialize canvas control
            _canvasControl?.Initialize(skBitmap.Width, skBitmap.Height);
            RenderCore();

            // Sync to VM
            try
            {
                _isSyncingToVM = true;
                vm.ImageFilePath = filePath;
                vm.IsDirty = false;
                vm.HasAnnotations = false;
                vm.UpdateCoreHistoryState(_editorCore.CanUndo, _editorCore.CanRedo);

                using var image = SKImage.FromBitmap(skBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var pngStream = new MemoryStream(data.ToArray());
                vm.PreviewImage = new Avalonia.Media.Imaging.Bitmap(pngStream);
            }
            finally
            {
                _isSyncingToVM = false;
            }

            // Track in recent files
            if (!string.IsNullOrEmpty(filePath))
            {
                vm.AddRecentImageFile(filePath);
            }
        }

        private void OnEmojiInsertionRequested(object? sender, EmojiSelectionRequest e)
        {
            try
            {
                InsertEmojiAnnotation(e.UnicodeSequence, e.DisplayName);
            }
            catch (Exception ex)
            {
                EditorServices.ReportWarning(nameof(EditorView), $"Failed to render emoji '{e.DisplayName}'.", ex);
            }
        }

        private async void OnCopyImageRequested()
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.HasHostCopyHandler) return;

            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            IClipboard? clipboard = topLevel?.Clipboard;
            if (clipboard == null) return;

            using var skBitmap = GetSnapshot();
            if (skBitmap == null) return;

            using var image = SKImage.FromBitmap(skBitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            using var memStream = new System.IO.MemoryStream(encoded.ToArray());
            var bitmap = new Avalonia.Media.Imaging.Bitmap(memStream);

            DataTransfer data = new DataTransfer();
            DataTransferItem item = new DataTransferItem();
            item.SetBitmap(bitmap);
            data.Add(item);

            await clipboard.SetDataAsync(data);
        }

        private void OnSaveRequested()
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.HasHostSaveHandler) return;

            if (!string.IsNullOrEmpty(vm.ImageFilePath))
            {
                SaveSnapshotToFile(vm.ImageFilePath!);
                vm.IsDirty = false;
            }
        }

        private async void OnSaveAsRequested()
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.HasHostSaveAsHandler) return;

            await SaveAsAsync();
        }

        private async Task SaveAsAsync()
        {
            if (DataContext is not MainViewModel vm) return;

            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null) return;

            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save image as",
                SuggestedFileName = !string.IsNullOrEmpty(vm.ImageFilePath)
                    ? System.IO.Path.GetFileName(vm.ImageFilePath)
                    : "image.png",
                FileTypeChoices =
                [
                    new FilePickerFileType("PNG") { Patterns = ["*.png"] },
                    new FilePickerFileType("JPEG") { Patterns = ["*.jpg", "*.jpeg"] },
                    new FilePickerFileType("WebP") { Patterns = ["*.webp"] }
                ]
            });

            if (file != null)
            {
                var path = file.Path.LocalPath;
                SaveSnapshotToFile(path);
                vm.ImageFilePath = path;
                vm.IsDirty = false;
            }
        }

        private void SaveSnapshotToFile(string path)
        {
            using var bitmap = GetSnapshot();
            if (bitmap == null) return;

            var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
            var format = ext switch
            {
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png
            };

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, format == SKEncodedImageFormat.Jpeg ? 95 : 100);
            using var stream = System.IO.File.OpenWrite(path);
            data.SaveTo(stream);
        }

        private void OnZoomToFitRequested(object? sender, EventArgs e)
        {
            _zoomController.ZoomToFit();
        }

        private void OnFlattenRequested(object? sender, EventArgs e)
        {
            var snapshot = GetSnapshot();
            if (snapshot == null) return;

            if (_editorCore.FlattenImage(snapshot))
            {
                // Clear annotation visuals from the UI canvas
                var canvas = this.FindControl<Canvas>("AnnotationCanvas");
                if (canvas != null)
                {
                    canvas.Children.Clear();
                    _selectionController.ClearSelection();
                }

                if (DataContext is MainViewModel vm)
                {
                    vm.HasAnnotations = false;
                }
            }
        }

        public void OpenContextMenu(Control target)
        {
            if (this.Resources["EditorContextFlyout"] is MenuFlyout flyout)
            {
                target.ContextFlyout = flyout;
                flyout.ShowAt(target);
            }
        }

    }
}
