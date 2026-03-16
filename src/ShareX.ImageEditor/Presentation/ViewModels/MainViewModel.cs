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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.ImageEditor.Core.Abstractions;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Core.Editor;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.Theming;
using System.Collections.ObjectModel;

namespace ShareX.ImageEditor.Presentation.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        public sealed class GradientPreset
        {
            public required string Name { get; init; }
            public required IBrush Brush { get; init; }
        }

        public enum EditorTaskResult
        {
            None,
            Continue,
            ContinueNoSave,
            Cancel
        }

        private readonly ImageEditorOptions _options;
        public ImageEditorOptions Options => _options;
        public IAnnotationToolbarAdapter ToolbarAdapter { get; }

        private const string OutputRatioAuto = "Auto";

        [ObservableProperty]
        private bool _isDirty;

        private bool _isSyncingFromCore;
        private bool _zoomToFitOnNextImageLoad;

        [ObservableProperty]
        private string _windowTitle = "ShareX - Image Editor";

        [ObservableProperty]
        private bool _showTaskModeButtons = true;

        // Events to signal View to perform canvas operations
        public event EventHandler? UndoRequested;
        public event EventHandler? RedoRequested;
        public event EventHandler? DeleteRequested;
        public event EventHandler? ClearAnnotationsRequested;
        public event EventHandler? FlattenRequested;
        public event EventHandler? DeselectRequested;
        public event EventHandler? PasteRequested;
        public event EventHandler? DuplicateRequested;
        public event EventHandler? CutAnnotationRequested;
        public event EventHandler? CopyAnnotationRequested;
        public event EventHandler? ZoomToFitRequested;
        public event EventHandler? CloseRequested;

        [ObservableProperty]
        private bool _taskMode;

        public string ContinueButtonTooltip => TaskMode ? "Continue (Enter)" : "Run after capture tasks (Enter)";

        partial void OnTaskModeChanged(bool value)
        {
            OnPropertyChanged(nameof(ContinueButtonTooltip));
        }

        [ObservableProperty]
        private EditorTaskResult _taskResult = EditorTaskResult.None;

        [RelayCommand]
        private void Continue()
        {
            TaskResult = EditorTaskResult.Continue;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose();
        }

        public void RequestClose()
        {
            TaskResult = EditorTaskResult.Cancel;

            if (Options.ShowExitConfirmation && IsDirty)
            {
                ShowConfirmationDialog();
            }
            else
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ShowConfirmationDialog()
        {
            var dialog = new ConfirmationDialogViewModel(
                onYes: () =>
                {
                    Save();
                    IsModalOpen = false;
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                },
                onNo: () =>
                {
                    IsModalOpen = false;
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                },
                onCancel: () =>
                {
                    IsModalOpen = false;
                }
            );

            ModalContent = dialog;
            IsModalOpen = true;
        }

        // Export events
        private Action? _copyRequested;
        public event Action? CopyRequested
        {
            add { _copyRequested += value; CopyCommand.NotifyCanExecuteChanged(); }
            remove { _copyRequested -= value; CopyCommand.NotifyCanExecuteChanged(); }
        }
        public bool CanCopy() => _copyRequested != null && HasPreviewImage;

        private Action? _saveRequested;
        public event Action? SaveRequested
        {
            add { _saveRequested += value; SaveCommand.NotifyCanExecuteChanged(); }
            remove { _saveRequested -= value; SaveCommand.NotifyCanExecuteChanged(); }
        }
        public bool CanSave() => _saveRequested != null && HasPreviewImage;

        private Action? _saveAsRequested;
        public event Action? SaveAsRequested
        {
            add { _saveAsRequested += value; SaveAsCommand.NotifyCanExecuteChanged(); }
            remove { _saveAsRequested -= value; SaveAsCommand.NotifyCanExecuteChanged(); }
        }
        public bool CanSaveAs() => _saveAsRequested != null && HasPreviewImage;

        private Action? _pinRequested;
        public event Action? PinRequested
        {
            add { _pinRequested += value; PinToScreenCommand.NotifyCanExecuteChanged(); }
            remove { _pinRequested -= value; PinToScreenCommand.NotifyCanExecuteChanged(); }
        }
        public bool CanPinToScreen() => _pinRequested != null;

        private Action? _uploadRequested;
        public event Action? UploadRequested
        {
            add { _uploadRequested += value; UploadCommand.NotifyCanExecuteChanged(); }
            remove { _uploadRequested -= value; UploadCommand.NotifyCanExecuteChanged(); }
        }
        public bool CanUpload() => _uploadRequested != null;

        private Bitmap? _previewImage;
        public Bitmap? PreviewImage
        {
            get => _previewImage;
            set
            {
                if (SetProperty(ref _previewImage, value))
                {
                    OnPreviewImageChanged(value);
                }
            }
        }

        private bool _hasPreviewImage;
        public bool HasPreviewImage
        {
            get => _hasPreviewImage;
            set
            {
                if (SetProperty(ref _hasPreviewImage, value))
                {
                    ToggleEffectsPanelCommand.NotifyCanExecuteChanged();
                    CopyCommand.NotifyCanExecuteChanged();
                    SaveCommand.NotifyCanExecuteChanged();
                    SaveAsCommand.NotifyCanExecuteChanged();
                    ZoomInCommand.NotifyCanExecuteChanged();
                    ZoomOutCommand.NotifyCanExecuteChanged();
                    ResetZoomCommand.NotifyCanExecuteChanged();
                    ZoomToFitCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _hasSelectedAnnotation;
        /// <summary>
        /// Whether there is a currently selected annotation (shape). Used for Delete button CanExecute.
        /// </summary>
        public bool HasSelectedAnnotation
        {
            get => _hasSelectedAnnotation;
            set
            {
                if (SetProperty(ref _hasSelectedAnnotation, value))
                {
                    DeleteSelectedCommand.NotifyCanExecuteChanged();
                    DuplicateSelectedCommand.NotifyCanExecuteChanged();
                    BringToFrontCommand.NotifyCanExecuteChanged();
                    SendToBackCommand.NotifyCanExecuteChanged();
                    BringForwardCommand.NotifyCanExecuteChanged();
                    BringForwardCommand.NotifyCanExecuteChanged();
                    SendBackwardCommand.NotifyCanExecuteChanged();
                    CutAnnotationCommand.NotifyCanExecuteChanged();
                    CopyAnnotationCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _hasAnnotations;
        /// <summary>
        /// Whether there are any annotations on the canvas. Used for Clear All button CanExecute.
        /// </summary>
        public bool HasAnnotations
        {
            get => _hasAnnotations;
            set
            {
                if (SetProperty(ref _hasAnnotations, value))
                {
                    ClearAnnotationsCommand.NotifyCanExecuteChanged();
                    FlattenImageCommand.NotifyCanExecuteChanged();
                }
            }
        }

        [ObservableProperty]
        private double _imageWidth;

        [ObservableProperty]
        private double _imageHeight;

        private Thickness _smartPaddingCropInsets;

        public double SmartPaddingViewportWidth
        {
            get
            {
                double width = ImageWidth;

                if (BackgroundSmartPadding && AreBackgroundEffectsActive)
                {
                    width -= _smartPaddingCropInsets.Left + _smartPaddingCropInsets.Right;
                }

                return Math.Max(0, width);
            }
        }

        public double SmartPaddingViewportHeight
        {
            get
            {
                double height = ImageHeight;

                if (BackgroundSmartPadding && AreBackgroundEffectsActive)
                {
                    height -= _smartPaddingCropInsets.Top + _smartPaddingCropInsets.Bottom;
                }

                return Math.Max(0, height);
            }
        }

        public double SmartPaddingOffsetX
        {
            get
            {
                if (!BackgroundSmartPadding || !AreBackgroundEffectsActive)
                {
                    return 0;
                }

                return -_smartPaddingCropInsets.Left;
            }
        }

        public double SmartPaddingOffsetY
        {
            get
            {
                if (!BackgroundSmartPadding || !AreBackgroundEffectsActive)
                {
                    return 0;
                }

                return -_smartPaddingCropInsets.Top;
            }
        }

        private void NotifySmartPaddingLayoutChanged()
        {
            OnPropertyChanged(nameof(SmartPaddingColor));
            OnPropertyChanged(nameof(SmartPaddingThickness));
            OnPropertyChanged(nameof(SmartPaddingViewportWidth));
            OnPropertyChanged(nameof(SmartPaddingViewportHeight));
            OnPropertyChanged(nameof(SmartPaddingOffsetX));
            OnPropertyChanged(nameof(SmartPaddingOffsetY));
        }

        private void OnPreviewImageChanged(Bitmap? value)
        {
            if (value != null)
            {
                ImageWidth = value.Size.Width;
                ImageHeight = value.Size.Height;
                HasPreviewImage = true;

                if (!_isSyncingFromCore && !_isApplyingSmartPadding)
                {
                    IsDirty = true;
                }

                NotifySmartPaddingLayoutChanged();

                // Apply smart padding crop if enabled (but not if we're already applying it)
                // Only trigger if background effects are active to avoid overwriting live previews
                // Also skip if we are syncing from Core (to prevent infinite loops)
                if (BackgroundSmartPadding && !_isApplyingSmartPadding && AreBackgroundEffectsActive && !_isSyncingFromCore)
                {
                    ApplySmartPaddingCrop();
                }

                var fileName = GetFileNameFromPath(ImageFilePath);
                WindowTitle = BuildWindowTitle(ImageWidth, ImageHeight, fileName);
            }
            else
            {
                ImageWidth = 0;
                ImageHeight = 0;
                _smartPaddingCropInsets = new Thickness(0);
                NotifySmartPaddingLayoutChanged();
                HasPreviewImage = false;
            }
        }

        [ObservableProperty]
        private double _backgroundMargin = 30;

        [ObservableProperty]
        private double _backgroundPadding = 30;

        [ObservableProperty]
        private bool _backgroundSmartPadding = true;

        /// <summary>
        /// ISSUE-022 fix: Recursion guard flag for smart padding event chain.
        /// Prevents infinite loop: BackgroundSmartPadding property change → ApplySmartPaddingCrop →
        /// UpdatePreview → PreviewImage changed → ApplySmartPaddingCrop (again).
        /// Set to true during ApplySmartPaddingCrop execution to break the cycle.
        /// </summary>
        private bool _isApplyingSmartPadding = false;

        /// <summary>
        /// Public accessor for _isApplyingSmartPadding. Used by EditorView to skip
        /// LoadImageFromViewModel during smart padding operations, preventing history reset.
        /// </summary>
        public bool IsSmartPaddingInProgress => _isApplyingSmartPadding;

        public Thickness SmartPaddingThickness => AreBackgroundEffectsActive ? new Thickness(BackgroundPadding) : new Thickness(0);

        public IBrush SmartPaddingColor
        {
            get
            {
                if (!AreBackgroundEffectsActive || PreviewImage == null || BackgroundPadding <= 0)
                {
                    return Brushes.Transparent;
                }

                try
                {
                    int sampleX = 0;
                    int sampleY = 0;

                    if (BackgroundSmartPadding && AreBackgroundEffectsActive)
                    {
                        sampleX = (int)Math.Round(Math.Max(0, _smartPaddingCropInsets.Left));
                        sampleY = (int)Math.Round(Math.Max(0, _smartPaddingCropInsets.Top));
                    }

                    var topLeftColor = SamplePixelColor(PreviewImage, sampleX, sampleY);
                    return new SolidColorBrush(topLeftColor);
                }
                catch (Exception ex)
                {
                    EditorServices.ReportWarning(nameof(MainViewModel), "Failed to sample smart padding color. Falling back to transparent.", ex);
                    return Brushes.Transparent;
                }
            }
        }

        [ObservableProperty]
        private double _backgroundRoundedCorner = 15;

        [ObservableProperty]
        private double _backgroundShadowRadius = 30;

        private const double MinZoom = 0.25;
        private const double MaxZoom = 4.0;
        private const double ZoomStep = 0.1;

        [ObservableProperty]
        private double _zoom = 1.0;

        [ObservableProperty]
        private string _imageDimensions = "No image";

        [ObservableProperty]
        private bool _isPngFormat = true;

        [ObservableProperty]
        private string _appVersion;

        [ObservableProperty]
        private bool _isSettingsPanelOpen;

        [ObservableProperty]
        private int _numberCounter = 1;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
        private bool _canUndo;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
        private bool _canRedo;

        private bool _isCoreUndoAvailable;
        private bool _isCoreRedoAvailable;
        private EditorCore? _editorCore;

        private bool _isPreviewingEffect;
        public bool AreBackgroundEffectsActive => IsSettingsPanelOpen && !_isPreviewingEffect;
        public IBrush EffectiveCanvasBackground => AreBackgroundEffectsActive ? CanvasBackground : Brushes.Transparent;

        partial void OnIsSettingsPanelOpenChanged(bool value)
        {
            if (value)
            {
                StartWallpaperBackgroundPreload();
            }

            // Toggle background effects visibility
            OnPropertyChanged(nameof(AreBackgroundEffectsActive));
            OnPropertyChanged(nameof(EffectiveCanvasBackground));
            UpdateCanvasProperties();

            // Re-evaluate Smart Padding application
            // If we closed the panel, we might need to revert crop. If opened, apply crop.
            // But ApplySmartPaddingCrop depends on BackgroundSmartPadding too.
            if (_originalSourceImage != null)
            {
                ApplySmartPaddingCrop();
            }
        }

        public void UpdateCoreHistoryState(bool canUndo, bool canRedo)
        {
            _isCoreUndoAvailable = canUndo;
            _isCoreRedoAvailable = canRedo;
            UpdateUndoRedoProperties();
        }

        private void UpdateUndoRedoProperties()
        {
            CanUndo = _isCoreUndoAvailable;
            CanRedo = _isCoreRedoAvailable;
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PasteCommand))]
        private bool _canPaste;

        [RelayCommand(CanExecute = nameof(CanPaste))]
        private void Paste()
        {
            PasteRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand(CanExecute = nameof(HasSelectedAnnotation))]
        private void CutAnnotation()
        {
            CutAnnotationRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand(CanExecute = nameof(HasSelectedAnnotation))]
        private void CopyAnnotation()
        {
            CopyAnnotationRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand(CanExecute = nameof(HasSelectedAnnotation))]
        private void DuplicateSelected()
        {
            DuplicateRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand(CanExecute = nameof(HasSelectedAnnotation))]
        private void BringToFront()
        {
            _editorCore?.BringToFront();
        }

        [RelayCommand(CanExecute = nameof(HasSelectedAnnotation))]
        private void SendToBack()
        {
            _editorCore?.SendToBack();
        }

        [RelayCommand(CanExecute = nameof(HasSelectedAnnotation))]
        private void BringForward()
        {
            _editorCore?.BringForward();
        }

        [RelayCommand(CanExecute = nameof(HasSelectedAnnotation))]
        private void SendBackward()
        {
            _editorCore?.SendBackward();
        }

        [ObservableProperty]
        private string _selectedOutputRatio = OutputRatioAuto;

        [ObservableProperty]
        private double? _targetOutputAspectRatio;

        [RelayCommand]
        private void ResetNumberCounter()
        {
            NumberCounter = 1;
        }

        public void RecalculateNumberCounter(IEnumerable<Annotation> annotations)
        {
            int max = 0;
            if (annotations != null)
            {
                foreach (var ann in annotations)
                {
                    if (ann is NumberAnnotation num)
                    {
                        if (num.Number > max) max = num.Number;
                    }
                }
            }
            NumberCounter = max + 1;
        }

        [RelayCommand]
        private void SetOutputRatio(string ratioKey)
        {
            SelectedOutputRatio = string.IsNullOrWhiteSpace(ratioKey) ? OutputRatioAuto : ratioKey;
        }

        // Effects Panel Properties
        [ObservableProperty]
        private bool _isEffectsPanelOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEffectBrowserVisible))]
        [NotifyPropertyChangedFor(nameof(IsRightEffectsSidebarVisible))]
        private object? _effectsPanelContent;

        public bool IsEffectBrowserVisible => EffectsPanelContent == null && IsEffectsPanelOpen;

        public bool IsRightEffectsSidebarVisible => EffectsPanelContent != null && IsEffectsPanelOpen;

        private void CancelPendingEffectPreviewIfAny()
        {
            if (_preEffectImage != null)
            {
                // Ensure dialog preview is reverted when switching away from the dialog host.
                CancelEffectPreview();
            }
        }

        [RelayCommand]
        private void CloseEffectsPanel()
        {
            CancelPendingEffectPreviewIfAny();
            IsEffectsPanelOpen = false;
            EffectsPanelContent = null;
            OnPropertyChanged(nameof(IsEffectBrowserVisible));
            OnPropertyChanged(nameof(IsRightEffectsSidebarVisible));
        }

        [RelayCommand(CanExecute = nameof(HasPreviewImage))]
        private void ToggleEffectsPanel()
        {
            if (IsEffectsPanelOpen && EffectsPanelContent == null)
            {
                IsEffectsPanelOpen = false;
                OnPropertyChanged(nameof(IsEffectBrowserVisible));
                OnPropertyChanged(nameof(IsRightEffectsSidebarVisible));
            }
            else if (IsEffectsPanelOpen && EffectsPanelContent != null)
            {
                // Switching from dialog sidebar back to browser should cancel live preview.
                CancelPendingEffectPreviewIfAny();

                EffectsPanelContent = null;
                IsEffectsPanelOpen = true;
                OnPropertyChanged(nameof(IsEffectBrowserVisible));
                OnPropertyChanged(nameof(IsRightEffectsSidebarVisible));
            }
            else
            {
                EffectsPanelContent = null;
                IsEffectsPanelOpen = true;
                OnPropertyChanged(nameof(IsEffectBrowserVisible));
                OnPropertyChanged(nameof(IsRightEffectsSidebarVisible));
            }
        }

        // Modal Overlay Properties
        [ObservableProperty]
        private bool _isModalOpen;

        [ObservableProperty]
        private object? _modalContent;

        [RelayCommand]
        private void CloseModal()
        {
            IsModalOpen = false;
            ModalContent = null;
        }

        [RelayCommand]
        private void SetTheme(string themeName)
        {
            var theme = themeName switch
            {
                "ShareXDark" => ThemeManager.ShareXDark,
                "ShareXLight" => ThemeManager.ShareXLight,
                _ => ThemeVariant.Default
            };
            ThemeManager.SetTheme(theme);
        }

        [ObservableProperty]
        private IBrush _canvasBackground;

        public ObservableCollection<GradientPreset> GradientPresets { get; }

        [ObservableProperty]
        private double _canvasCornerRadius = 0;

        [ObservableProperty]
        private Thickness _canvasPadding;

        [ObservableProperty]
        private BoxShadows _canvasShadow;

        [ObservableProperty]
        private string? _lastSavedPath;

        [ObservableProperty]
        private string? _imageFilePath;

        [ObservableProperty]
        private string _applicationName = "ShareX";

        public string EditorTitle => $"{ApplicationName} Editor";

        public static MainViewModel Current { get; private set; } = null!;

        public MainViewModel(ImageEditorOptions? options = null)
        {
            _options = options ?? new ImageEditorOptions();
            ToolbarAdapter = new EditorToolbarAdapter(this);
            Current = this;
            GradientPresets = BuildGradientPresets();
            BackgroundModeOptions = BuildBackgroundModeOptions();
            InitializeBackgroundSettingsFromOptions();
            _canvasBackground = Brushes.Transparent;

            // Initialize values from options
            _textColor = $"#{_options.TextTextColor.A:X2}{_options.TextTextColor.R:X2}{_options.TextTextColor.G:X2}{_options.TextTextColor.B:X2}";
            _fillColor = $"#{_options.FillColor.A:X2}{_options.FillColor.R:X2}{_options.FillColor.G:X2}{_options.FillColor.B:X2}";
            _selectedColor = $"#{_options.BorderColor.A:X2}{_options.BorderColor.R:X2}{_options.BorderColor.G:X2}{_options.BorderColor.B:X2}";
            _strokeWidth = _options.Thickness;
            _cornerRadius = _options.CornerRadius;
            _fontSize = _options.TextFontSize;
            _shadowEnabled = _options.Shadow;
            _textBold = _options.TextBold;
            _textItalic = _options.TextItalic;
            _textUnderline = _options.TextUnderline;

            // Get version from assembly
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            _appVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";

            ApplySelectedBackgroundMode();
            UpdateCanvasProperties();
        }

        private static string BuildWindowTitle(double width, double height, string? fileName)
        {
            var sb = new System.Text.StringBuilder("ShareX - Image Editor");

            if (width > 0 && height > 0)
            {
                sb.Append($" - {width}x{height}");
            }

            if (!string.IsNullOrEmpty(fileName))
            {
                sb.Append($" - {fileName}");
            }

            return sb.ToString();
        }

        private static string? GetFileNameFromPath(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;
            try
            {
                return Path.GetFileName(filePath);
            }
            catch
            {
                return null;
            }
        }

        partial void OnImageFilePathChanged(string? value)
        {
            // Refresh window title when file path changes (after Save / Save As)
            var fileName = GetFileNameFromPath(value);
            WindowTitle = BuildWindowTitle(ImageWidth, ImageHeight, fileName);
        }

        public void AttachEditorCore(EditorCore editorCore)
        {
            _editorCore = editorCore;
            UpdateCoreHistoryState(editorCore.CanUndo, editorCore.CanRedo);
        }

        public void RequestZoomToFitOnNextImageLoad()
        {
            _zoomToFitOnNextImageLoad = Options.ZoomToFitOnOpen;
        }

        public bool ConsumeZoomToFitOnNextImageLoad()
        {
            if (!_zoomToFitOnNextImageLoad)
            {
                return false;
            }

            _zoomToFitOnNextImageLoad = false;
            return true;
        }

        partial void OnApplicationNameChanged(string value)
        {
            OnPropertyChanged(nameof(EditorTitle));
        }

        partial void OnSelectedOutputRatioChanged(string value)
        {
            TargetOutputAspectRatio = ParseAspectRatio(value);
            UpdateCanvasProperties();
        }

        partial void OnBackgroundMarginChanged(double value)
        {
            if (_isInitializingBackgroundSettings)
            {
                return;
            }

            Options.BackgroundMargin = value;
            UpdateCanvasProperties();
        }

        partial void OnBackgroundPaddingChanged(double value)
        {
            if (_isInitializingBackgroundSettings)
            {
                return;
            }

            Options.BackgroundPadding = value;
            NotifySmartPaddingLayoutChanged();
            UpdateCanvasProperties();
        }

        partial void OnBackgroundSmartPaddingChanged(bool value)
        {
            if (_isInitializingBackgroundSettings)
            {
                return;
            }

            Options.BackgroundSmartPadding = value;
            ApplySmartPaddingCrop();
        }

        partial void OnBackgroundRoundedCornerChanged(double value)
        {
            if (_isInitializingBackgroundSettings)
            {
                return;
            }

            Options.BackgroundRoundedCorner = value;
            UpdateCanvasProperties();
        }

        partial void OnBackgroundShadowRadiusChanged(double value)
        {
            if (_isInitializingBackgroundSettings)
            {
                return;
            }

            Options.BackgroundShadowRadius = value;
            UpdateCanvasProperties();
        }

        partial void OnZoomChanged(double value)
        {
            var clamped = Math.Clamp(value, MinZoom, MaxZoom);
            if (Math.Abs(clamped - value) > 0.0001)
            {
                Zoom = clamped;
                return;
            }
        }

        // Static color palette for annotation toolbar
        public static string[] ColorPalette => new[]
        {
            "#EF4444", "#F97316", "#EAB308", "#22C55E",
            "#0EA5E9", "#6366F1", "#A855F7", "#EC4899",
            "#FFFFFF", "#000000", "#64748B", "#1E293B"
        };

        // Static stroke widths
        public static int[] StrokeWidths => new[] { 2, 4, 6, 8, 10 };

        [RelayCommand]
        private void SelectTool(EditorTool tool)
        {
            DeselectRequested?.Invoke(this, EventArgs.Empty);

            if (tool is EditorTool.Crop or EditorTool.CutOut)
            {
                IsSettingsPanelOpen = false;
            }

            ActiveTool = tool;
        }

        [RelayCommand]
        private void SetColor(string color)
        {
            SelectedColor = color;
        }

        [RelayCommand]
        private void SetStrokeWidth(int width)
        {
            StrokeWidth = width;
        }

        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            UndoRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            RedoRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand(CanExecute = nameof(HasSelectedAnnotation))]
        private void DeleteSelected()
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand(CanExecute = nameof(HasAnnotations))]
        private void ClearAnnotations()
        {
            ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);
            ResetNumberCounter();
        }

        [RelayCommand(CanExecute = nameof(HasAnnotations))]
        private void FlattenImage()
        {
            FlattenRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void ToggleSettingsPanel()
        {
            IsSettingsPanelOpen = !IsSettingsPanelOpen;
        }

        private bool CanZoom() => HasPreviewImage;

        [RelayCommand(CanExecute = nameof(CanZoom))]
        private void ZoomIn()
        {
            Zoom = Math.Clamp(Math.Round((Zoom + ZoomStep) * 100) / 100, MinZoom, MaxZoom);
        }

        [RelayCommand(CanExecute = nameof(CanZoom))]
        private void ZoomOut()
        {
            Zoom = Math.Clamp(Math.Round((Zoom - ZoomStep) * 100) / 100, MinZoom, MaxZoom);
        }

        [RelayCommand(CanExecute = nameof(CanZoom))]
        private void ResetZoom()
        {
            Zoom = 1.0;
        }

        [RelayCommand(CanExecute = nameof(CanZoom))]
        private void ZoomToFit()
        {
            ZoomToFitRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Clear()
        {
            PreviewImage = null;

            // ISSUE-030 fix: Dispose bitmaps before clearing
            _currentSourceImage?.Dispose();
            _currentSourceImage = null;

            _originalSourceImage?.Dispose();
            _originalSourceImage = null;

            // HasPreviewImage = false; // Handled by OnPreviewImageChanged
            ImageDimensions = "No image";
            ResetNumberCounter();

            // Clear annotations as well
            ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand(CanExecute = nameof(CanCopy))]
        private void Copy()
        {
            _copyRequested?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            _saveRequested?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanSaveAs))]
        private void SaveAs()
        {
            _saveAsRequested?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanPinToScreen))]
        private void PinToScreen()
        {
            _pinRequested?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanUpload))]
        private async Task Upload()
        {
            _uploadRequested?.Invoke();
        }
    }
}
