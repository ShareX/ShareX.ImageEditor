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
using ShareX.ImageEditor.Abstractions;
using ShareX.ImageEditor.Adapters;
using ShareX.ImageEditor.Annotations;
using ShareX.ImageEditor.Helpers;
using ShareX.ImageEditor.ImageEffects.Adjustments;
using ShareX.ImageEditor.ImageEffects.Manipulations;
using System.Collections.ObjectModel;

namespace ShareX.ImageEditor.ViewModels
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

        private readonly EditorOptions _options;
        public EditorOptions Options => _options;
        public IAnnotationToolbarAdapter ToolbarAdapter { get; }

        private const string OutputRatioAuto = "Auto";

        [ObservableProperty]
        private bool _isDirty;

        private bool _isSyncingFromCore;

        [ObservableProperty]
        private string _windowTitle = "ShareX - Image Editor";

        [ObservableProperty]
        private bool _showCaptureToolbar = true;

        // Events to signal View to perform canvas operations
        public event EventHandler? UndoRequested;
        public event EventHandler? RedoRequested;
        public event EventHandler? DeleteRequested;
        public event EventHandler? ClearAnnotationsRequested;
        public event EventHandler? DeselectRequested;
        public event EventHandler? PasteRequested;
        public event EventHandler? DuplicateRequested;
        public event EventHandler? CutAnnotationRequested;
        public event EventHandler? CopyAnnotationRequested;
        public event EventHandler? ZoomToFitRequested;
        public event EventHandler? CloseRequested;

        [ObservableProperty]
        private bool _taskMode;

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
            TaskResult = EditorTaskResult.Cancel;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RequestClose()
        {
            if (IsDirty)
            {
                ShowConfirmationDialog();
            }
            else
            {
                TaskResult = EditorTaskResult.Continue;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ShowConfirmationDialog()
        {
            var dialog = new ConfirmationDialogViewModel(
                onYes: () =>
                {
                    Save();
                    TaskResult = EditorTaskResult.Continue;
                    IsModalOpen = false;
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                },
                onNo: () =>
                {
                    TaskResult = EditorTaskResult.ContinueNoSave;
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
        public bool CanCopy() => _copyRequested != null;

        private Action? _saveRequested;
        public event Action? SaveRequested
        {
            add { _saveRequested += value; SaveCommand.NotifyCanExecuteChanged(); }
            remove { _saveRequested -= value; SaveCommand.NotifyCanExecuteChanged(); }
        }
        public bool CanSave() => _saveRequested != null;

        private Action? _saveAsRequested;
        public event Action? SaveAsRequested
        {
            add { _saveAsRequested += value; SaveAsCommand.NotifyCanExecuteChanged(); }
            remove { _saveAsRequested -= value; SaveAsCommand.NotifyCanExecuteChanged(); }
        }
        public bool CanSaveAs() => _saveAsRequested != null;

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
            set => SetProperty(ref _hasPreviewImage, value);
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
                }
            }
        }

        [ObservableProperty]
        private double _imageWidth;

        [ObservableProperty]
        private double _imageHeight;

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

                OnPropertyChanged(nameof(SmartPaddingColor));

                // Apply smart padding crop if enabled (but not if we're already applying it)
                // Only trigger if background effects are active to avoid overwriting live previews
                // Also skip if we are syncing from Core (to prevent infinite loops)
                if (UseSmartPadding && !_isApplyingSmartPadding && AreBackgroundEffectsActive && !_isSyncingFromCore)
                {
                    ApplySmartPaddingCrop();
                }

                WindowTitle = $"ShareX - Image Editor - {ImageWidth}x{ImageHeight}";
            }
            else
            {
                ImageWidth = 0;
                ImageHeight = 0;
                HasPreviewImage = false;
            }
        }

        [ObservableProperty]
        private double _previewPadding = 30;

        [ObservableProperty]
        private double _smartPadding = 30;

        [ObservableProperty]
        private bool _useSmartPadding = true;

        /// <summary>
        /// ISSUE-022 fix: Recursion guard flag for smart padding event chain.
        /// Prevents infinite loop: UseSmartPadding property change → ApplySmartPaddingCrop →
        /// UpdatePreview → PreviewImage changed → ApplySmartPaddingCrop (again).
        /// Set to true during ApplySmartPaddingCrop execution to break the cycle.
        /// </summary>
        private bool _isApplyingSmartPadding = false;

        /// <summary>
        /// Public accessor for _isApplyingSmartPadding. Used by EditorView to skip
        /// LoadImageFromViewModel during smart padding operations, preventing history reset.
        /// </summary>
        public bool IsSmartPaddingInProgress => _isApplyingSmartPadding;

        public Thickness SmartPaddingThickness => AreBackgroundEffectsActive ? new Thickness(SmartPadding) : new Thickness(0);

        public IBrush SmartPaddingColor
        {
            get
            {
                if (!AreBackgroundEffectsActive || PreviewImage == null || SmartPadding <= 0)
                {
                    return Brushes.Transparent;
                }

                try
                {
                    var topLeftColor = SamplePixelColor(PreviewImage, 0, 0);
                    return new SolidColorBrush(topLeftColor);
                }
                catch
                {
                    return Brushes.Transparent;
                }
            }
        }

        [ObservableProperty]
        private double _previewCornerRadius = 15;

        [ObservableProperty]
        private double _shadowBlur = 30;

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
        private string _selectedColor = "#EF4444";

        // Add a brush version for the dropdown control
        public IBrush SelectedColorBrush
        {
            get => new SolidColorBrush(Color.Parse(SelectedColor));
            set
            {
                if (value is SolidColorBrush solidBrush)
                {
                    SelectedColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                }
            }
        }

        // Color value for Avalonia ColorPicker binding
        public Color SelectedColorValue
        {
            get => Color.Parse(SelectedColor);
            set => SelectedColor = $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
        }

        partial void OnSelectedColorChanged(string value)
        {
            OnPropertyChanged(nameof(SelectedColorBrush));
            OnPropertyChanged(nameof(SelectedColorValue));
            UpdateOptionsFromSelectedColor();
        }

        private void UpdateOptionsFromSelectedColor()
        {
            var color = SelectedColorValue;
            switch (ActiveTool)
            {
                case EditorTool.Select:
                    if (SelectedAnnotation != null)
                    {
                        // TODO: Update SelectedAnnotation color if needed
                    }
                    else
                    {
                        // Fallback to update generic options if no annotation is selected but tool is active?
                        // Or just update default options.
                        UpdateDefaultOptionsColor(color);
                    }
                    break;
                default:
                    UpdateDefaultOptionsColor(color);
                    break;
            }
        }

        private void UpdateDefaultOptionsColor(Color color)
        {
            switch (ActiveTool)
            {
                case EditorTool.Step:
                    Options.StepBorderColor = color;
                    break;
                case EditorTool.Highlight:
                    Options.HighlighterColor = color;
                    break;
                default:
                    Options.BorderColor = color;
                    break;
            }
        }

        [ObservableProperty]
        private int _strokeWidth = 4;

        partial void OnStrokeWidthChanged(int value)
        {
            Options.Thickness = value;
        }

        // Tool-specific options
        [ObservableProperty]
        private string _fillColor = "#00000000"; // Transparent by default

        // Add a brush version for the fill color dropdown control
        public IBrush FillColorBrush
        {
            get => new SolidColorBrush(Color.Parse(FillColor));
            set
            {
                if (value is SolidColorBrush solidBrush)
                {
                    FillColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                }
            }
        }

        // Color value for Avalonia ColorPicker binding
        public Color FillColorValue
        {
            get => Color.Parse(FillColor);
            set => FillColor = $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
        }

        partial void OnFillColorChanged(string value)
        {
            OnPropertyChanged(nameof(FillColorBrush));
            OnPropertyChanged(nameof(FillColorValue));
            UpdateOptionsFromFillColor();
        }

        private void UpdateOptionsFromFillColor()
        {
            var color = FillColorValue;
            switch (ActiveTool)
            {
                case EditorTool.Step:
                    Options.StepFillColor = color;
                    break;
                default:
                    Options.FillColor = color;
                    break;
            }
        }

        [ObservableProperty]
        private float _fontSize = 30;

        partial void OnFontSizeChanged(float value)
        {
            bool isStep = ActiveTool == EditorTool.Step;

            if (ActiveTool == EditorTool.Select && SelectedAnnotation is NumberAnnotation)
            {
                isStep = true;
            }

            if (isStep)
            {
                Options.StepFontSize = value;
            }
            else
            {
                Options.FontSize = value;
            }
        }

        [ObservableProperty]
        private float _effectStrength = 10;

        partial void OnEffectStrengthChanged(float value)
        {
            switch (ActiveTool)
            {
                case EditorTool.Blur:
                    Options.BlurStrength = value;
                    break;
                case EditorTool.Pixelate:
                    Options.PixelateStrength = value;
                    break;
                case EditorTool.Magnify:
                    Options.MagnifierStrength = value;
                    break;
                case EditorTool.Spotlight:
                    Options.SpotlightStrength = value;
                    break;
            }
        }

        [ObservableProperty]
        private bool _shadowEnabled = true;

        partial void OnShadowEnabledChanged(bool value)
        {
            Options.Shadow = value;
        }

        // Visibility computed properties based on ActiveTool
        public bool ShowBorderColor => ActiveTool switch
        {
            EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
                or EditorTool.Freehand or EditorTool.Highlight or EditorTool.Text
                or EditorTool.SpeechBalloon or EditorTool.Step => true,
            EditorTool.Select => _selectedAnnotation != null && _selectedAnnotation.ToolType switch
            {
                EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
                    or EditorTool.Freehand or EditorTool.Highlight or EditorTool.Text
                    or EditorTool.SpeechBalloon or EditorTool.Step => true,
                _ => false
            },
            _ => false
        };

        public bool ShowFillColor => ActiveTool switch
        {
            EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.SpeechBalloon or EditorTool.Step => true,
            EditorTool.Select => _selectedAnnotation != null && _selectedAnnotation.ToolType switch
            {
                EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.SpeechBalloon or EditorTool.Step => true,
                _ => false
            },
            _ => false
        };

        public bool ShowThickness => ActiveTool switch
        {
            EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
                or EditorTool.Freehand or EditorTool.SpeechBalloon or EditorTool.Step or EditorTool.SmartEraser => true,
            EditorTool.Select => _selectedAnnotation != null && _selectedAnnotation.ToolType switch
            {
                EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
                    or EditorTool.Freehand or EditorTool.SpeechBalloon or EditorTool.Step or EditorTool.SmartEraser => true,
                _ => false
            },
            _ => false
        };

        public bool ShowFontSize => ActiveTool switch
        {
            EditorTool.Text or EditorTool.Step or EditorTool.SpeechBalloon => true,
            EditorTool.Select => _selectedAnnotation != null && _selectedAnnotation.ToolType switch
            {
                EditorTool.Text or EditorTool.Step or EditorTool.SpeechBalloon => true,
                _ => false
            },
            _ => false
        };

        public bool ShowStrength => ActiveTool switch
        {
            EditorTool.Blur or EditorTool.Pixelate or EditorTool.Magnify or EditorTool.Spotlight => true,
            EditorTool.Select => _selectedAnnotation != null && _selectedAnnotation.ToolType switch
            {
                EditorTool.Blur or EditorTool.Pixelate or EditorTool.Magnify or EditorTool.Spotlight => true,
                _ => false
            },
            _ => false
        };

        public bool ShowShadow => ActiveTool switch
        {
            EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
                or EditorTool.Freehand or EditorTool.Text or EditorTool.SpeechBalloon or EditorTool.Step => true,
            EditorTool.Select => _selectedAnnotation != null && _selectedAnnotation.ToolType switch
            {
                EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
                    or EditorTool.Freehand or EditorTool.Text or EditorTool.SpeechBalloon or EditorTool.Step => true,
                _ => false
            },
            _ => false
        };

        // Track selected annotation for Select tool visibility logic
        private Annotation? _selectedAnnotation;
        public Annotation? SelectedAnnotation
        {
            get => _selectedAnnotation;
            set
            {
                if (SetProperty(ref _selectedAnnotation, value))
                {
                    UpdateToolOptionsVisibility();
                }
            }
        }

        private void UpdateToolOptionsVisibility()
        {
            OnPropertyChanged(nameof(ShowBorderColor));
            OnPropertyChanged(nameof(ShowFillColor));
            OnPropertyChanged(nameof(ShowThickness));
            OnPropertyChanged(nameof(ShowFontSize));
            OnPropertyChanged(nameof(ShowStrength));
            OnPropertyChanged(nameof(ShowShadow));
            OnPropertyChanged(nameof(ShowToolOptionsSeparator));
        }

        public bool ShowToolOptionsSeparator => ShowBorderColor || ShowFillColor || ShowThickness || ShowFontSize || ShowStrength || ShowShadow;

        [ObservableProperty]
        private EditorTool _activeTool = EditorTool.Rectangle;

        partial void OnActiveToolChanged(EditorTool value)
        {
            UpdateToolOptionsVisibility();
            LoadOptionsForTool(value);
        }

        private void LoadOptionsForTool(EditorTool tool)
        {
            // Prevent property change callbacks from overwriting options while loading
            // We can just set fields directly or use a flag, but setting properties is safer for UI updates.
            // However, setting properties triggers On...Changed which calls UpdateOptionsFrom...
            // Use a flag to suppress updates back to Options? 
            // Actually, if we set the property to the value from Options, updating Options back to the same value is harmless.

            switch (tool)
            {
                case EditorTool.Rectangle:
                case EditorTool.Ellipse:
                case EditorTool.Line:
                case EditorTool.Arrow:
                case EditorTool.Freehand:
                case EditorTool.Text:
                case EditorTool.SpeechBalloon:
                    SelectedColorValue = Options.BorderColor;
                    FillColorValue = Options.FillColor;
                    StrokeWidth = Options.Thickness;
                    ShadowEnabled = Options.Shadow;
                    FontSize = Options.FontSize;
                    break;
                case EditorTool.Step:
                    SelectedColorValue = Options.StepBorderColor;
                    FillColorValue = Options.StepFillColor;
                    StrokeWidth = Options.Thickness; // Or specific step thickness? EditorOptions uses generic Thickness.
                    ShadowEnabled = Options.Shadow;
                    FontSize = Options.StepFontSize;
                    break;
                case EditorTool.Highlight:
                    SelectedColorValue = Options.HighlighterColor;
                    StrokeWidth = Options.Thickness;
                    break;
                case EditorTool.Blur:
                    EffectStrength = Options.BlurStrength;
                    break;
                case EditorTool.Pixelate:
                    EffectStrength = Options.PixelateStrength;
                    break;
                case EditorTool.Magnify:
                    EffectStrength = Options.MagnifierStrength;
                    break;
                case EditorTool.Spotlight:
                    EffectStrength = Options.SpotlightStrength;
                    break;
            }
        }

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

        partial void OnIsSettingsPanelOpenChanged(bool value)
        {
            // Toggle background effects visibility
            OnPropertyChanged(nameof(AreBackgroundEffectsActive));
            UpdateCanvasProperties();

            // Re-evaluate Smart Padding application
            // If we closed the panel, we might need to revert crop. If opened, apply crop.
            // But ApplySmartPaddingCrop depends on UseSmartPadding too.
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
        private object? _effectsPanelContent;

        [RelayCommand]
        private void CloseEffectsPanel()
        {
            IsEffectsPanelOpen = false;
            EffectsPanelContent = null;
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
                "Dark" => ThemeVariant.Dark,
                "Light" => ThemeVariant.Light,
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
        private string _applicationName = "ShareX";

        public string EditorTitle => $"{ApplicationName} Editor";

        public static MainViewModel Current { get; private set; } = null!;

        public MainViewModel(EditorOptions? options = null)
        {
            _options = options ?? new EditorOptions();
            ToolbarAdapter = new EditorToolbarAdapter(this);
            Current = this;
            GradientPresets = BuildGradientPresets();
            _canvasBackground = CopyBrush(GradientPresets[0].Brush);

            // Get version from assembly
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            _appVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";

            UpdateCanvasProperties();
        }

        public void AttachEditorCore(EditorCore editorCore)
        {
            _editorCore = editorCore;
            UpdateCoreHistoryState(editorCore.CanUndo, editorCore.CanRedo);
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

        partial void OnPreviewPaddingChanged(double value)
        {
            UpdateCanvasProperties();
        }

        partial void OnSmartPaddingChanged(double value)
        {
            OnPropertyChanged(nameof(SmartPaddingColor));
            OnPropertyChanged(nameof(SmartPaddingThickness));
        }

        partial void OnUseSmartPaddingChanged(bool value)
        {
            ApplySmartPaddingCrop();
        }

        partial void OnPreviewCornerRadiusChanged(double value)
        {
            UpdateCanvasProperties();
        }

        partial void OnShadowBlurChanged(double value)
        {
            UpdateCanvasProperties();
        }

        private Color SamplePixelColor(Bitmap bitmap, int x, int y)
        {
            // Optimization: If we have the source SKBitmap (which we usually do for the main image),
            // use it directly instead of round-tripping.
            if (_currentSourceImage != null &&
                _currentSourceImage.Width == bitmap.Size.Width &&
                _currentSourceImage.Height == bitmap.Size.Height)
            {
                if (x < 0 || y < 0 || x >= _currentSourceImage.Width || y >= _currentSourceImage.Height)
                    return Colors.Transparent;

                var skColor = _currentSourceImage.GetPixel(x, y);
                return Color.FromArgb(skColor.Alpha, skColor.Red, skColor.Green, skColor.Blue);
            }

            // Fallback to fast conversion if we don't have the source match
            using var skBitmap = BitmapConversionHelpers.ToSKBitmap(bitmap);
            if (skBitmap == null || x < 0 || y < 0 || x >= skBitmap.Width || y >= skBitmap.Height)
            {
                return Colors.Transparent;
            }

            var pixel = skBitmap.GetPixel(x, y);
            return Color.FromArgb(pixel.Alpha, pixel.Red, pixel.Green, pixel.Blue);
        }

        /// <summary>
        /// Applies smart padding crop to remove uniform-colored borders from the image.
        /// <para><strong>ISSUE-022 fix: Event Chain Documentation</strong></para>
        /// <para>
        /// This method is part of a complex event chain that requires recursion prevention:
        /// </para>
        /// <list type="number">
        /// <item>User toggles UseSmartPadding property → OnPropertyChanged fires</item>
        /// <item>Property change triggers this method via partial method hook</item>
        /// <item>Method modifies PreviewImage (via UpdatePreview or direct assignment)</item>
        /// <item>PreviewImage change would trigger this method again → infinite loop</item>
        /// </list>
        /// <para>
        /// Solution: <c>_isApplyingSmartPadding</c> flag prevents re-entry during execution.
        /// </para>
        /// <para>
        /// Additionally, this method is called automatically when background effects are applied,
        /// ensuring the smart padding is re-applied to maintain correct image bounds.
        /// </para>
        /// </summary>
        private void ApplySmartPaddingCrop()
        {
            if (_originalSourceImage == null || PreviewImage == null)
            {
                return;
            }

            if (_isApplyingSmartPadding)
            {
                return; // Prevent recursive calls
            }

            if (!UseSmartPadding)
            {
                // Restore original image from backup
                if (!_isApplyingSmartPadding && _originalSourceImage != null)
                {
                    _isApplyingSmartPadding = true;
                    try
                    {
                        // SIP-FIX: Must copy _originalSourceImage because _currentSourceImage is owned/disposable
                        if (_currentSourceImage != null && _currentSourceImage != _originalSourceImage)
                        {
                            _currentSourceImage.Dispose();
                        }
                        _currentSourceImage = SafeCopyBitmap(_originalSourceImage, "ApplySmartPaddingCrop.Restore");
                        if (_currentSourceImage != null)
                        {
                            PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(_currentSourceImage);
                            ImageDimensions = $"{_currentSourceImage.Width} x {_currentSourceImage.Height}";
                        }
                    }
                    finally
                    {
                        _isApplyingSmartPadding = false;
                    }
                }
                return;
            }

            _isApplyingSmartPadding = true;
            try
            {
                // Start from the original image backup
                var skBitmap = _originalSourceImage;

                if (skBitmap == null) return;

                // Get top-left pixel color as reference
                var targetColor = skBitmap.GetPixel(0, 0);
                const int tolerance = 30; // Color tolerance for matching

                // Find bounds of content (non-matching pixels)
                // SIP-FIX: Use precise scanning (every pixel) to find true edges
                int minX = skBitmap.Width;
                int minY = skBitmap.Height;
                int maxX = 0;
                int maxY = 0;

                // SIP-FIX: Use unsafe pointer access for performance to allow checking every pixel
                unsafe
                {
                    byte* ptr = (byte*)skBitmap.GetPixels().ToPointer();
                    int width = skBitmap.Width;
                    int height = skBitmap.Height;
                    int rowBytes = skBitmap.RowBytes;
                    int bpp = skBitmap.BytesPerPixel;

                    byte tR = targetColor.Red;
                    byte tG = targetColor.Green;
                    byte tB = targetColor.Blue;
                    byte tA = targetColor.Alpha;

                    // Only optimize for 4 bytes per pixel (standard Bgra8888/Rgba8888)
                    if (bpp == 4)
                    {
                        bool isBgra = skBitmap.ColorType == SkiaSharp.SKColorType.Bgra8888;

                        for (int y = 0; y < height; y++)
                        {
                            byte* row = ptr + (y * rowBytes);
                            bool rowHasContent = false;

                            // Scan from left
                            for (int x = 0; x < width; x++)
                            {
                                byte r, g, b, a;
                                // Simple mapping for standard 4-byte formats
                                if (isBgra)
                                {
                                    b = row[x * 4 + 0];
                                    g = row[x * 4 + 1];
                                    r = row[x * 4 + 2];
                                    a = row[x * 4 + 3];
                                }
                                else // Rgba8888
                                {
                                    r = row[x * 4 + 0];
                                    g = row[x * 4 + 1];
                                    b = row[x * 4 + 2];
                                    a = row[x * 4 + 3];
                                }

                                if (Math.Abs(r - tR) > tolerance ||
                                    Math.Abs(g - tG) > tolerance ||
                                    Math.Abs(b - tB) > tolerance ||
                                    Math.Abs(a - tA) > tolerance)
                                {
                                    // Found content start
                                    if (x < minX) minX = x;
                                    if (x > maxX) maxX = x; // Initial set for this row
                                    if (y < minY) minY = y;
                                    if (y > maxY) maxY = y;
                                    rowHasContent = true;
                                    break; // Stop left scan
                                }
                            }

                            if (rowHasContent)
                            {
                                // Scan from right
                                for (int x = width - 1; x >= 0; x--)
                                {
                                    byte r, g, b, a;
                                    if (isBgra)
                                    {
                                        b = row[x * 4 + 0];
                                        g = row[x * 4 + 1];
                                        r = row[x * 4 + 2];
                                        a = row[x * 4 + 3];
                                    }
                                    else
                                    {
                                        r = row[x * 4 + 0];
                                        g = row[x * 4 + 1];
                                        b = row[x * 4 + 2];
                                        a = row[x * 4 + 3];
                                    }

                                    if (Math.Abs(r - tR) > tolerance ||
                                        Math.Abs(g - tG) > tolerance ||
                                        Math.Abs(b - tB) > tolerance ||
                                        Math.Abs(a - tA) > tolerance)
                                    {
                                        if (x > maxX) maxX = x;
                                        break; // Stop right scan, found the end of content
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Fallback: Use GetPixel but with edge-scan optimization for non-standard formats
                        for (int y = 0; y < height; y++)
                        {
                            bool rowHasContent = false;
                            // Scan left
                            for (int x = 0; x < width; x++)
                            {
                                var pixel = skBitmap.GetPixel(x, y);
                                if (Math.Abs(pixel.Red - tR) > tolerance ||
                                    Math.Abs(pixel.Green - tG) > tolerance ||
                                    Math.Abs(pixel.Blue - tB) > tolerance ||
                                    Math.Abs(pixel.Alpha - tA) > tolerance)
                                {
                                    if (x < minX) minX = x;
                                    if (x > maxX) maxX = x;
                                    if (y < minY) minY = y;
                                    if (y > maxY) maxY = y;
                                    rowHasContent = true;
                                    break;
                                }
                            }
                            if (rowHasContent)
                            {
                                // Scan right
                                for (int x = width - 1; x >= 0; x--)
                                {
                                    var pixel = skBitmap.GetPixel(x, y);
                                    if (Math.Abs(pixel.Red - tR) > tolerance ||
                                        Math.Abs(pixel.Green - tG) > tolerance ||
                                        Math.Abs(pixel.Blue - tB) > tolerance ||
                                        Math.Abs(pixel.Alpha - tA) > tolerance)
                                    {
                                        if (x > maxX) maxX = x;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                // Check if we found any content
                if (minX > maxX || minY > maxY || !AreBackgroundEffectsActive)
                {
                    // No content found (or background effects disabled), keep original
                    // SIP-FIX: Must copy _originalSourceImage because _currentSourceImage is owned/disposable
                    if (_currentSourceImage != null && _currentSourceImage != _originalSourceImage)
                    {
                        _currentSourceImage.Dispose();
                    }
                    _currentSourceImage = SafeCopyBitmap(_originalSourceImage, "ApplySmartPaddingCrop.NoCrop");
                    if (_currentSourceImage != null)
                    {
                        PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(_currentSourceImage);
                        ImageDimensions = $"{_currentSourceImage.Width} x {_currentSourceImage.Height}";
                    }

                    return;
                }

                // Calculate crop rectangle
                int cropX = minX;
                int cropY = minY;
                int cropWidth = maxX - minX + 1;
                int cropHeight = maxY - minY + 1;

                // Ensure valid dimensions
                if (cropWidth <= 0 || cropHeight <= 0)
                {
                    return;
                }

                // Perform the crop on the original image using internal ImageHelpers
                var cropped = ImageHelpers.Crop(_originalSourceImage, cropX, cropY, cropWidth, cropHeight);

                // Update preview with cropped image
                // ISSUE-023 fix: Dispose old currentSourceImage before reassignment (if different)
                if (_currentSourceImage != null && _currentSourceImage != _originalSourceImage)
                {
                    _currentSourceImage.Dispose();
                }
                _currentSourceImage = cropped;
                PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(cropped);
                ImageDimensions = $"{cropped.Width} x {cropped.Height}";
            }
            catch
            {
            }
            finally
            {
                _isApplyingSmartPadding = false;
            }
        }

        [RelayCommand]
        private void ApplyGradientPreset(GradientPreset preset)
        {
            // Clone to avoid accidental brush sharing between controls
            CanvasBackground = CopyBrush(preset.Brush);
        }

        private void UpdateCanvasProperties()
        {
            if (AreBackgroundEffectsActive)
            {
                CanvasPadding = CalculateOutputPadding(PreviewPadding, TargetOutputAspectRatio);
                CanvasShadow = new BoxShadows(new BoxShadow
                {
                    Blur = ShadowBlur,
                    Color = Color.FromArgb(80, 0, 0, 0),
                    OffsetX = 0,
                    OffsetY = 10
                });
                CanvasCornerRadius = Math.Max(0, PreviewCornerRadius);
            }
            else
            {
                CanvasPadding = new Thickness(0);
                CanvasShadow = new BoxShadows(); // No shadow
                CanvasCornerRadius = 0;
            }
            OnPropertyChanged(nameof(SmartPaddingColor));
            OnPropertyChanged(nameof(SmartPaddingThickness));
        }

        private Thickness CalculateOutputPadding(double basePadding, double? targetAspectRatio)
        {
            if (PreviewImage == null || PreviewImage.Size.Width <= 0 || PreviewImage.Size.Height <= 0 || !targetAspectRatio.HasValue)
            {
                return new Thickness(basePadding);
            }

            double imageWidth = PreviewImage.Size.Width;
            double imageHeight = PreviewImage.Size.Height;

            double totalWidth = imageWidth + (basePadding * 2);
            double totalHeight = imageHeight + (basePadding * 2);
            double currentAspect = totalWidth / totalHeight;
            double target = targetAspectRatio.Value;

            double extraX = 0;
            double extraY = 0;

            const double epsilon = 0.0001;
            if (currentAspect > target + epsilon)
            {
                // Too wide, add vertical padding
                double requiredHeight = totalWidth / target;
                double addHeight = Math.Max(0, requiredHeight - totalHeight);
                extraY = addHeight / 2;
            }
            else if (currentAspect + epsilon < target)
            {
                // Too tall, add horizontal padding
                double requiredWidth = totalHeight * target;
                double addWidth = Math.Max(0, requiredWidth - totalWidth);
                extraX = addWidth / 2;
            }

            return new Thickness(basePadding + extraX, basePadding + extraY, basePadding + extraX, basePadding + extraY);
        }

        private static double? ParseAspectRatio(string ratio)
        {
            if (string.IsNullOrWhiteSpace(ratio) || string.Equals(ratio, OutputRatioAuto, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var parts = ratio.Split(':');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out var w) &&
                double.TryParse(parts[1], out var h) &&
                w > 0 && h > 0)
            {
                return w / h;
            }

            return null;
        }

        private static ObservableCollection<GradientPreset> BuildGradientPresets()
        {
            static LinearGradientBrush Make(string start, string end) => new()
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new Avalonia.Media.GradientStop(Color.Parse(start), 0),
                    new Avalonia.Media.GradientStop(Color.Parse(end), 1)
                }
            };

            return new ObservableCollection<GradientPreset>
            {
                new() { Name = "None", Brush = Brushes.Transparent },
                new() { Name = "Sunset", Brush = Make("#F093FB", "#F5576C") },
                new() { Name = "Ocean", Brush = Make("#667EEA", "#764BA2") },
                new() { Name = "Forest", Brush = Make("#11998E", "#38EF7D") },
                new() { Name = "Fire", Brush = Make("#F12711", "#F5AF19") },
                new() { Name = "Cool Blue", Brush = Make("#2193B0", "#6DD5ED") },
                new() { Name = "Lavender", Brush = Make("#B8B8FF", "#D6A4FF") },
                new() { Name = "Aqua", Brush = Make("#13547A", "#80D0C7") },
                new() { Name = "Grape", Brush = Make("#7F00FF", "#E100FF") },
                new() { Name = "Peach", Brush = Make("#FFB88C", "#DE6262") },
                new() { Name = "Sky", Brush = Make("#56CCF2", "#2F80ED") },
                new() { Name = "Warm", Brush = Make("#F2994A", "#F2C94C") },
                new() { Name = "Mint", Brush = Make("#00B09B", "#96C93D") },
                new() { Name = "Midnight", Brush = Make("#232526", "#414345") },
                new() { Name = "Carbon", Brush = Make("#373B44", "#4286F4") },
                new() { Name = "Deep Space", Brush = Make("#000428", "#004E92") },
                new() { Name = "Noir", Brush = Make("#0F2027", "#2C5364") },
                new() { Name = "Royal", Brush = Make("#141E30", "#243B55") },
                new() { Name = "Rose Gold", Brush = Make("#E8CBC0", "#636FA4") },
                new() { Name = "Emerald", Brush = Make("#076585", "#FFFFFF") },
                new() { Name = "Amethyst", Brush = Make("#9D50BB", "#6E48AA") },
                new() { Name = "Neon", Brush = Make("#FF0844", "#FFB199") },
                new() { Name = "Aurora", Brush = Make("#00C9FF", "#92FE9D") },
                new() { Name = "Candy", Brush = Make("#D53369", "#DAAE51") },
                new() { Name = "Clean", Brush = new SolidColorBrush(Color.Parse("#FFFFFF")) }
            };
        }

        private static IBrush CopyBrush(IBrush brush)
        {
            switch (brush)
            {
                case SolidColorBrush solid:
                    return new SolidColorBrush(solid.Color)
                    {
                        Opacity = solid.Opacity
                    };
                case LinearGradientBrush linear:
                    var stops = new GradientStops();
                    foreach (var stop in linear.GradientStops)
                    {
                        stops.Add(new Avalonia.Media.GradientStop(stop.Color, stop.Offset));
                    }

                    return new LinearGradientBrush
                    {
                        StartPoint = linear.StartPoint,
                        EndPoint = linear.EndPoint,
                        GradientStops = stops,
                        SpreadMethod = linear.SpreadMethod,
                        Opacity = linear.Opacity
                    };
                default:
                    // Fall back to the original reference if an unsupported brush type is supplied.
                    return brush;
            }
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

        [RelayCommand]
        private void ToggleSettingsPanel()
        {
            IsSettingsPanelOpen = !IsSettingsPanelOpen;
        }

        [RelayCommand]
        private void ZoomIn()
        {
            Zoom = Math.Clamp(Math.Round((Zoom + ZoomStep) * 100) / 100, MinZoom, MaxZoom);
        }

        [RelayCommand]
        private void ZoomOut()
        {
            Zoom = Math.Clamp(Math.Round((Zoom - ZoomStep) * 100) / 100, MinZoom, MaxZoom);
        }

        [RelayCommand]
        private void ResetZoom()
        {
            Zoom = 1.0;
        }

        [RelayCommand]
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

        private SkiaSharp.SKBitmap? _currentSourceImage;
        private SkiaSharp.SKBitmap? _originalSourceImage; // Backup for smart padding restore

        private static bool IsBitmapAlive(SkiaSharp.SKBitmap? bitmap)
        {
            return bitmap != null && bitmap.Handle != IntPtr.Zero;
        }

        private static SkiaSharp.SKBitmap? SafeCopyBitmap(SkiaSharp.SKBitmap? source, string context)
        {
            if (!IsBitmapAlive(source))
            {
                return null;
            }

            SkiaSharp.SKBitmap safeSource = source!;
            SkiaSharp.SKBitmap? copy = safeSource.Copy();
            if (copy == null || copy.Handle == IntPtr.Zero)
            {
                copy?.Dispose();
                return null;
            }

            return copy;
        }

        private SkiaSharp.SKBitmap? GetBestAvailableSourceBitmap()
        {
            SkiaSharp.SKBitmap? coreSource = _editorCore?.SourceImage;
            if (IsBitmapAlive(coreSource))
            {
                return coreSource;
            }

            if (IsBitmapAlive(_currentSourceImage))
            {
                return _currentSourceImage;
            }

            return null;
        }

        /// <summary>
        /// Updates the preview image. **TAKES OWNERSHIP** of the bitmap parameter.
        /// </summary>
        /// <remarks>
        /// ISSUE-027: Ownership contract documentation
        /// - The bitmap parameter is stored directly in _currentSourceImage
        /// - The caller MUST NOT dispose the bitmap after calling this method
        /// - A backup copy is created for _originalSourceImage (for smart padding)
        /// - If the bitmap was created by the caller, ownership is fully transferred
        /// </remarks>
        /// <param name="image">Image bitmap (ownership transferred to ViewModel)</param>
        /// <param name="clearAnnotations">Whether to clear all annotations</param>
        public void UpdatePreview(SkiaSharp.SKBitmap image, bool clearAnnotations = true)
        {
            if (!IsBitmapAlive(image))
            {
                return;
            }

            // ISSUE-031 fix: Dispose old currentSourceImage before replacing (if different object)
            if (_currentSourceImage != null && _currentSourceImage != image)
            {
                _currentSourceImage.Dispose();
            }
            _currentSourceImage = image;

            // Update original backup first so smart padding uses the new image during PreviewImage change
            if (!_isApplyingSmartPadding)
            {
                _originalSourceImage?.Dispose();
                var copy = SafeCopyBitmap(image, "UpdatePreview");
                _originalSourceImage = copy;
            }

            // Store dimensions BEFORE conversion (ToAvaloniBitmap triggers property change that may dispose the bitmap)
            int width = image.Width;
            int height = image.Height;

            // Convert SKBitmap to Avalonia Bitmap
            PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(image);
            ImageDimensions = $"{width} x {height}";

            // Reset view state for the new image
            Zoom = 1.0;
            if (clearAnnotations)
            {
                ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);
            }
            ResetNumberCounter();
        }

        public void CropImage(int x, int y, int width, int height)
        {
            if (_editorCore == null || width <= 0 || height <= 0)
            {
                return;
            }

            _editorCore.Crop(new SkiaSharp.SKRect(x, y, x + width, y + height));
        }

        public void CutOutImage(int startPos, int endPos, bool isVertical)
        {
            _editorCore?.CutOut(startPos, endPos, isVertical);
        }

        // --- Edit Menu Commands ---

        [RelayCommand]
        private void Rotate90Clockwise()
        {
            _editorCore?.Rotate90Clockwise();
        }

        [RelayCommand]
        private void Rotate90CounterClockwise()
        {
            _editorCore?.Rotate90CounterClockwise();
        }

        [RelayCommand]
        private void Rotate180()
        {
            _editorCore?.Rotate180();
        }

        [RelayCommand]
        private void FlipHorizontal()
        {
            _editorCore?.FlipHorizontal();
        }

        [RelayCommand]
        private void FlipVertical()
        {
            _editorCore?.FlipVertical();
        }

        [RelayCommand]
        private void AutoCropImage()
        {
            _editorCore?.AutoCrop(10);
        }

        /// <summary>
        /// Resize the image to new dimensions with specified quality.
        /// </summary>
        public void ResizeImage(int newWidth, int newHeight, SkiaSharp.SKFilterQuality quality = SkiaSharp.SKFilterQuality.High)
        {
            if (newWidth <= 0 || newHeight <= 0)
            {
                return;
            }

            _editorCore?.ResizeImage(newWidth, newHeight, quality);
        }

        /// <summary>
        /// Resize the canvas by adding padding around the image.
        /// </summary>
        public void ResizeCanvas(int top, int right, int bottom, int left, SkiaSharp.SKColor backgroundColor)
        {
            _editorCore?.ResizeCanvas(top, right, bottom, left, backgroundColor);
        }

        // --- Effects Menu Commands ---

        [RelayCommand]
        private void InvertColors()
        {
            ApplyOneShotEffect(img => new InvertImageEffect().Apply(img), "Inverted colors");
        }

        [RelayCommand]
        private void BlackAndWhite()
        {
            ApplyOneShotEffect(img => new BlackAndWhiteImageEffect().Apply(img), "Applied Black & White filter");
        }

        [RelayCommand]
        private void Sepia()
        {
            ApplyOneShotEffect(img => new SepiaImageEffect().Apply(img), "Applied Sepia filter");
        }

        [RelayCommand]
        private void Polaroid()
        {
            ApplyOneShotEffect(img => new PolaroidImageEffect().Apply(img), "Applied Polaroid filter");
        }

        private void ApplyOneShotEffect(Func<SkiaSharp.SKBitmap, SkiaSharp.SKBitmap> effect, string statusMessage)
        {
            if (_editorCore == null)
            {
                return;
            }

            _editorCore.ApplyImageEffect(effect);
        }

        // --- Effect Live Preview Logic ---

        private SkiaSharp.SKBitmap? _preEffectImage;

        /// <summary>
        /// Called when an effect dialog opens to store the state before previewing.
        /// </summary>
        public void StartEffectPreview()
        {
            SkiaSharp.SKBitmap? source = GetBestAvailableSourceBitmap();
            if (source == null)
            {
                return;
            }

            SkiaSharp.SKBitmap? copy = SafeCopyBitmap(source, "StartEffectPreview");
            if (copy == null)
            {
                return;
            }

            // ISSUE-024 fix: Dispose previous bitmap before reassignment
            _preEffectImage?.Dispose();
            _preEffectImage = copy;

            _isPreviewingEffect = true;
            OnPropertyChanged(nameof(AreBackgroundEffectsActive));
            UpdateCanvasProperties();
            ApplySmartPaddingCrop();
        }

        /// <summary>
        /// Updates the displayed preview without committing changes to the source image or undo stack.
        /// </summary>
        public void UpdatePreviewImageOnly(SkiaSharp.SKBitmap preview, bool syncSourceState = false)
        {
            if (!IsBitmapAlive(preview))
            {
                return;
            }

            try
            {
                _isSyncingFromCore = true;

                // SIP-FIX: Calculate dimensions string BEFORE setting PreviewImage.
                // Setting PreviewImage can trigger bindings that might dispose the source 
                // via EditorCore updates if not handled carefully.
                string dimStr = $"{preview.Width} x {preview.Height}";

                PreviewImage = Helpers.BitmapConversionHelpers.ToAvaloniBitmap(preview);
                ImageDimensions = dimStr;

                if (syncSourceState)
                {
                    SkiaSharp.SKBitmap? sourceCopy = SafeCopyBitmap(preview, "UpdatePreviewImageOnly.SyncCurrent");
                    if (sourceCopy != null)
                    {
                        _currentSourceImage?.Dispose();
                        _currentSourceImage = sourceCopy;

                        if (!_isApplyingSmartPadding)
                        {
                            _originalSourceImage?.Dispose();
                            _originalSourceImage = SafeCopyBitmap(sourceCopy, "UpdatePreviewImageOnly.SyncOriginal");
                        }
                    }
                }
            }
            finally
            {
                _isSyncingFromCore = false;
            }
        }

        /// <summary>
        /// ISSUE-028 fix: Common logic for committing effects and cleaning up preview state.
        /// </summary>
        private void CommitEffectAndCleanup(SkiaSharp.SKBitmap result, string statusMessage)
        {
            SkiaSharp.SKBitmap? preEffectImage = _preEffectImage;
            bool applied = false;
            bool resultTransferred = false;

            if (_editorCore != null)
            {
                // SIP-FIX: Ensure EditorCore has the original clean image before applying the effect.
                // This ensures the memento captures the pre-effect state, not the preview/intermediate state.
                if (preEffectImage != null)
                {
                    var cleanState = preEffectImage.Copy();
                    if (cleanState != null)
                    {
                        _editorCore.UpdateSourceImage(cleanState);
                    }
                }

                applied = _editorCore.ApplyImageOperation(_ => result, clearAnnotations: false);
                resultTransferred = applied;

                // SIP-FIX: Ensure ViewModel state (_currentSourceImage) matches Core state after apply.
                if (applied)
                {
                    var syncCopy = SafeCopyBitmap(result, "CommitEffect.Sync");
                    if (syncCopy != null)
                    {
                        _currentSourceImage?.Dispose();
                        _currentSourceImage = syncCopy;

                        if (!_isApplyingSmartPadding)
                        {
                            _originalSourceImage?.Dispose();
                            _originalSourceImage = SafeCopyBitmap(syncCopy, "CommitEffect.SyncOriginal");
                        }
                    }
                }

                if (!applied)
                {
                    if (!ReferenceEquals(result, preEffectImage))
                    {
                        result.Dispose();
                    }
                }
            }
            else
            {
                UpdatePreview(result, clearAnnotations: false);
                applied = true;
                resultTransferred = true;
            }

            if (!(resultTransferred && ReferenceEquals(preEffectImage, result)))
            {
                preEffectImage?.Dispose();
            }
            _preEffectImage = null;

            _isPreviewingEffect = false;
            OnPropertyChanged(nameof(AreBackgroundEffectsActive));
            UpdateCanvasProperties();
            ApplySmartPaddingCrop();
        }

        /// <summary>
        /// Commits the effect to the undo stack and updates the source image.
        /// </summary>
        public void ApplyEffect(SkiaSharp.SKBitmap result, string statusMessage)
        {
            if (_preEffectImage == null) return; // Should have been started
            CommitEffectAndCleanup(result, statusMessage);
        }

        /// <summary>
        /// Cancels the preview and restores the original image view.
        /// </summary>
        public void CancelEffectPreview()
        {
            // SIP-FIX: Prioritize _preEffectImage (clean state) for cancellation.
            // GetBestAvailableSourceBitmap() might return the dirty/preview state from EditorCore.
            SkiaSharp.SKBitmap? source = _preEffectImage ?? GetBestAvailableSourceBitmap();

            if (source != null)
            {
                UpdatePreviewImageOnly(source, syncSourceState: true);
            }

            _preEffectImage?.Dispose();
            _preEffectImage = null;

            // Restore Background Effects
            _isPreviewingEffect = false;
            OnPropertyChanged(nameof(AreBackgroundEffectsActive));
            UpdateCanvasProperties();
            ApplySmartPaddingCrop();
        }

        /// <summary>
        /// Applies the effect function to the pre-effect image and updates the preview.
        /// </summary>
        public void PreviewEffect(Func<SkiaSharp.SKBitmap, SkiaSharp.SKBitmap> effect)
        {
            if (_preEffectImage == null || effect == null) return;

            try
            {
                var result = effect(_preEffectImage);
                UpdatePreviewImageOnly(result, syncSourceState: false);
                result.Dispose();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Applies the effect to the source image and commits to undo stack.
        /// </summary>
        public void ApplyEffect(Func<SkiaSharp.SKBitmap, SkiaSharp.SKBitmap> effect, string statusMessage)
        {
            if (_preEffectImage == null) return;

            var result = effect(_preEffectImage);
            CommitEffectAndCleanup(result, statusMessage);
        }

        // --- Rotate Custom Angle Feature ---

        [ObservableProperty]
        private double _rotateAngleDegrees;

        [ObservableProperty]
        private bool _isRotateCustomAngleDialogOpen;

        [ObservableProperty]
        private bool _rotateAutoResize = true;

        private SkiaSharp.SKBitmap? _rotateCustomAngleOriginalBitmap;

        [RelayCommand]
        public void OpenRotateCustomAngleDialog()
        {
            SkiaSharp.SKBitmap? source = GetBestAvailableSourceBitmap();
            if (PreviewImage == null || source == null)
            {
                return;
            }

            // Snapshot the CURRENT state (including any previous edits)
            var current = source;
            if (current != null)
            {
                // ISSUE-024 fix: Dispose previous bitmap before reassignment
                _rotateCustomAngleOriginalBitmap?.Dispose();
                var copy = SafeCopyBitmap(current, "OpenRotateCustomAngleDialog");
                if (copy == null)
                {
                    return;
                }
                _rotateCustomAngleOriginalBitmap = copy;
                RotateAngleDegrees = 0;
                IsRotateCustomAngleDialogOpen = true;
            }
        }

        partial void OnRotateAngleDegreesChanged(double value)
        {
            RotateCustomAngleLiveApply();
        }

        partial void OnRotateAutoResizeChanged(bool value)
        {
            RotateCustomAngleLiveApply();
        }

        private void RotateCustomAngleLiveApply()
        {
            if (!IsRotateCustomAngleDialogOpen || _rotateCustomAngleOriginalBitmap == null) return;

            float angle = (float)Math.Clamp(RotateAngleDegrees, -180, 180);
            var effect = RotateImageEffect.Custom(angle, RotateAutoResize);

            var result = effect.Apply(_rotateCustomAngleOriginalBitmap);

            UpdatePreviewImageOnly(result, syncSourceState: false);
            result.Dispose();
        }

        [RelayCommand]
        public void CommitRotateCustomAngle()
        {
            if (_editorCore == null || _rotateCustomAngleOriginalBitmap == null)
            {
                return;
            }

            float angle = (float)Math.Clamp(RotateAngleDegrees, -180, 180);
            _editorCore.RotateCustomAngle(angle, RotateAutoResize);

            IsRotateCustomAngleDialogOpen = false;
            IsModalOpen = false;
            ModalContent = null;

            _rotateCustomAngleOriginalBitmap?.Dispose();
            _rotateCustomAngleOriginalBitmap = null;
        }

        [RelayCommand]
        public void CancelRotateCustomAngle()
        {
            SkiaSharp.SKBitmap? source = GetBestAvailableSourceBitmap();
            if (source != null)
            {
                UpdatePreviewImageOnly(source, syncSourceState: true);
            }
            else if (_rotateCustomAngleOriginalBitmap != null)
            {
                UpdatePreview(_rotateCustomAngleOriginalBitmap, clearAnnotations: false);
                _rotateCustomAngleOriginalBitmap = null;
            }
            else
            {
                _rotateCustomAngleOriginalBitmap?.Dispose();
                _rotateCustomAngleOriginalBitmap = null;
            }

            IsRotateCustomAngleDialogOpen = false;
            IsModalOpen = false;
            ModalContent = null;
        }
    }
}



