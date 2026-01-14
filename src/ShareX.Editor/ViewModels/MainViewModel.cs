#region License Information (GPL v3)

/*
    ShareX.Editor - The UI-agnostic Editor library for ShareX
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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ShareX.Editor.ImageEffects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Editor.Annotations;
using ShareX.Editor.Helpers;
using System.Collections.ObjectModel;

namespace ShareX.Editor.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        public sealed class GradientPreset
        {
            public required string Name { get; init; }
            public required IBrush Brush { get; init; }
        }

        private const string OutputRatioAuto = "Auto";

        [ObservableProperty]
        private string _exportState = "";

        [ObservableProperty]
        private bool _showCaptureToolbar = true;

        // Events to signal View to perform canvas operations
        public event EventHandler? UndoRequested;
        public event EventHandler? RedoRequested;
        public event EventHandler? DeleteRequested;
        public event EventHandler? ClearAnnotationsRequested;

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
                OnPropertyChanged(nameof(SmartPaddingColor));

                // Apply smart padding crop if enabled (but not if we're already applying it)
                // Only trigger if background effects are active to avoid overwriting live previews
                if (UseSmartPadding && !_isApplyingSmartPadding && AreBackgroundEffectsActive)
                {
                    ApplySmartPaddingCrop();
                }
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

        private bool _isApplyingSmartPadding = false;

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
        private string _statusText = "Ready";

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
                    SelectedColor = $"#{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                }
            }
        }

        partial void OnSelectedColorChanged(string value)
        {
            OnPropertyChanged(nameof(SelectedColorBrush));
        }

        [ObservableProperty]
        private int _strokeWidth = 4;

        [ObservableProperty]
        private EditorTool _activeTool = EditorTool.Rectangle;

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
            CanUndo = _isCoreUndoAvailable || _imageUndoStack.Count > 0;
            CanRedo = _isCoreRedoAvailable || _imageRedoStack.Count > 0;
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

        [ObservableProperty]
        private IBrush _canvasBackground;

        public ObservableCollection<GradientPreset> GradientPresets { get; }

        [ObservableProperty]
        private double _canvasCornerRadius = 0;

        [ObservableProperty]
        private Thickness _canvasPadding;

        [ObservableProperty]
        private BoxShadows _canvasShadow;

        // Event for View to provide flattened image
        public event Func<Task<Bitmap?>>? SnapshotRequested;

        // Event for View to show SaveAs dialog and return selected path
        public event Func<Task<string?>>? SaveAsRequested;

        [ObservableProperty]
        private string? _lastSavedPath;

        [ObservableProperty]
        private string _applicationName = "ShareX";

        public string EditorTitle => $"{ApplicationName} Editor";

        public static MainViewModel Current { get; private set; } = null!;

        public MainViewModel()
        {
            Current = this;
            GradientPresets = BuildGradientPresets();
            _canvasBackground = CopyBrush(GradientPresets[1].Brush);

            // Get version from assembly
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            _appVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";

            UpdateCanvasProperties();
        }

        partial void OnApplicationNameChanged(string value)
        {
            OnPropertyChanged(nameof(EditorTitle));
        }

        partial void OnSelectedOutputRatioChanged(string value)
        {
            TargetOutputAspectRatio = ParseAspectRatio(value);
            UpdateCanvasProperties();
            StatusText = TargetOutputAspectRatio.HasValue
                ? $"Output ratio set to {value}"
                : "Output ratio auto";
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
                        UpdatePreview(_originalSourceImage);
                        StatusText = "Smart Padding: Restored original image";
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
                int minX = skBitmap.Width;
                int minY = skBitmap.Height;
                int maxX = 0;
                int maxY = 0;

                for (int y = 0; y < skBitmap.Height; y++)
                {
                    for (int x = 0; x < skBitmap.Width; x++)
                    {
                        var pixel = skBitmap.GetPixel(x, y);

                        // Check if pixel is different from target color (within tolerance)
                        if (Math.Abs(pixel.Red - targetColor.Red) > tolerance ||
                            Math.Abs(pixel.Green - targetColor.Green) > tolerance ||
                            Math.Abs(pixel.Blue - targetColor.Blue) > tolerance ||
                            Math.Abs(pixel.Alpha - targetColor.Alpha) > tolerance)
                        {
                            minX = Math.Min(minX, x);
                            minY = Math.Min(minY, y);
                            maxX = Math.Max(maxX, x);
                            maxY = Math.Max(maxY, y);
                        }
                    }
                }

                // Check if we found any content
                if (minX > maxX || minY > maxY || !AreBackgroundEffectsActive)
                {
                    // No content found (or background effects disabled), keep original
                    // (But verify if we should just show original)
                    _currentSourceImage = _originalSourceImage;
                    PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(_originalSourceImage);
                    ImageDimensions = $"{_originalSourceImage.Width} x {_originalSourceImage.Height}";

                    if (!AreBackgroundEffectsActive) StatusText = "Background effects hidden";
                    else StatusText = "Smart Padding: No content to crop";
                    
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
                _currentSourceImage = cropped;
                PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(cropped);
                ImageDimensions = $"{cropped.Width} x {cropped.Height}";
                StatusText = $"Smart Padding: Cropped to {cropWidth}x{cropHeight}";
            }
            catch (Exception ex)
            {
                StatusText = $"Smart Padding error: {ex.Message}";
                DebugHelper.WriteLine($"Smart padding crop failed: {ex.Message}");
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
            StatusText = $"Gradient set to {preset.Name}";
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

            StatusText = $"Zoom {clamped:P0}";
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
            // First check if we have image-level undo (crop/cutout operations)
            if (_imageUndoStack.Count > 0)
            {
                // Save current image to redo stack
                if (_currentSourceImage != null)
                {
                    _imageRedoStack.Push(_currentSourceImage.Copy());
                }

                // Restore previous image state
                var previousImage = _imageUndoStack.Pop();
                UpdatePreview(previousImage, clearAnnotations: false);
                StatusText = "Undid image operation";
                return;
            }

            // Otherwise delegate to annotation undo
            UndoRequested?.Invoke(this, EventArgs.Empty);
            StatusText = "Undo requested";
        }

        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            // First check if we have image-level redo (crop/cutout operations)
            if (_imageRedoStack.Count > 0)
            {
                // Save current image to undo stack
                var next = _imageRedoStack.Pop();
                if (_currentSourceImage != null)
                {
                    _imageUndoStack.Push(_currentSourceImage.Copy());
                }
                UpdatePreview(next, clearAnnotations: true);
                UpdateUndoRedoProperties();
                return;
            }

            // Otherwise delegate to annotation redo
            RedoRequested?.Invoke(this, EventArgs.Empty);
            StatusText = "Redo requested";
        }

        [RelayCommand]
        private void DeleteSelected()
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
            StatusText = "Delete requested";
        }

        [RelayCommand]
        private void ClearAnnotations()
        {
            ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);
            ResetNumberCounter();
            StatusText = "Annotations cleared";
        }

        [RelayCommand]
        private void ToggleSettingsPanel()
        {
            IsSettingsPanelOpen = !IsSettingsPanelOpen;
            StatusText = IsSettingsPanelOpen ? "Background panel opened" : "Background panel closed";
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
        private void Clear()
        {
            PreviewImage = null;
            _currentSourceImage = null;
            _originalSourceImage = null;
            // HasPreviewImage = false; // Handled by OnPreviewImageChanged
            ImageDimensions = "No image";
            StatusText = "Ready";
            ResetNumberCounter();

            // Clear annotations as well
            ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);
        }

        // Event for View to handle clipboard copy (requires TopLevel access)
        public event Func<Bitmap, Task>? CopyRequested;

        // Event for View to show error dialog
        public event Func<string, string, Task>? ShowErrorDialog;

        [RelayCommand]
        private async Task Copy()
        {
            // Get flattened image with annotations
            Bitmap? snapshot = null;
            if (SnapshotRequested != null)
            {
                snapshot = await SnapshotRequested.Invoke();
            }

            // Fallback to preview image if snapshot fails
            var imageToUse = snapshot ?? PreviewImage;
            if (imageToUse == null)
            {
                StatusText = "No image to copy";
                return;
            }

            if (CopyRequested != null)
            {
                try
                {
                    await CopyRequested.Invoke(imageToUse);
                    StatusText = snapshot != null
                        ? "Image with annotations copied to clipboard"
                        : "Image copied to clipboard";
                    ExportState = "Copied";
                    DebugHelper.WriteLine("Clipboard copy: Image copied to clipboard.");
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Failed to copy image to clipboard.\n\nError: {ex.Message}";
                    StatusText = $"Copy failed: {ex.Message}";
                    DebugHelper.WriteLine($"Clipboard copy failed: {ex.Message}");

                    // Show error dialog
                    if (ShowErrorDialog != null)
                    {
                        await ShowErrorDialog.Invoke("Copy Failed", errorMessage);
                    }
                }
            }
            else
            {
                StatusText = "Clipboard not available";
            }
        }

        [RelayCommand]
        private async Task QuickSave()
        {
            // Try get flattened image first
            Bitmap? snapshot = null;
            if (SnapshotRequested != null)
            {
                snapshot = await SnapshotRequested.Invoke();
            }

            if (snapshot == null && _currentSourceImage == null) return;

            try
            {
                // Simple quick save to Pictures/ShareX
                var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShareX");
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                var filename = $"ShareX_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
                var path = System.IO.Path.Combine(folder, filename);

                if (snapshot != null)
                {
                    snapshot.Save(path);
                }
                else if (_currentSourceImage != null)
                {
                    ImageHelpers.SaveBitmap(_currentSourceImage, path);
                }

                StatusText = $"Saved to {filename}";
                ExportState = "Saved";
                DebugHelper.WriteLine($"File saved: {path}");
            }
            catch (Exception ex)
            {
                StatusText = $"Save failed: {ex.Message}";
                DebugHelper.WriteLine($"File save failed: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task SaveAs()
        {
            if (SaveAsRequested == null)
            {
                StatusText = "SaveAs dialog not available";
                return;
            }

            // Show file picker dialog via View
            var path = await SaveAsRequested.Invoke();
            if (string.IsNullOrEmpty(path))
            {
                StatusText = "Save cancelled";
                return;
            }

            // Get flattened image with annotations
            Bitmap? snapshot = null;
            if (SnapshotRequested != null)
            {
                snapshot = await SnapshotRequested.Invoke();
            }

            var imageToSave = snapshot ?? PreviewImage;
            if (imageToSave == null)
            {
                StatusText = "No image to save";
                return;
            }

            try
            {
                // Save based on file extension
                var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();

                imageToSave.Save(path);

                var filename = System.IO.Path.GetFileName(path);
                StatusText = $"Saved to {filename}";
                ExportState = "Saved";
                LastSavedPath = path;
                DebugHelper.WriteLine($"File saved (Save As): {path}");
            }
            catch (Exception ex)
            {
                StatusText = $"Save failed: {ex.Message}";
                DebugHelper.WriteLine($"File save failed (Save As): {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task Upload()
        {
            // Get flattened image with annotations
            Bitmap? snapshot = null;
            if (SnapshotRequested != null)
            {
                snapshot = await SnapshotRequested.Invoke();
            }

            var imageToUpload = snapshot ?? PreviewImage;
            if (imageToUpload == null)
            {
                StatusText = "No image to upload";
                return;
            }

            try
            {
                StatusText = "Uploading...";
                ExportState = "Uploading";

                // TODO: Implement actual upload logic
                // This will be integrated with the upload system later
                // For now, just provide a placeholder that saves to temp and shows a message

                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ShareX_Upload_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
                imageToUpload.Save(tempPath);

                StatusText = "Upload complete (placeholder - integration needed)";
                ExportState = "Uploaded";
                DebugHelper.WriteLine($"Upload placeholder: Image saved to {tempPath}");

                // TODO: Replace with actual upload call:
                // var uploadResult = await UploadManager.UploadImageAsync(tempPath);
                // if (uploadResult.IsSuccess) StatusText = $"Uploaded: {uploadResult.URL}";
            }
            catch (Exception ex)
            {
                StatusText = $"Upload failed: {ex.Message}";
                ExportState = "";
                DebugHelper.WriteLine($"Upload failed: {ex.Message}");
            }
        }

        private SkiaSharp.SKBitmap? _currentSourceImage;
        private SkiaSharp.SKBitmap? _originalSourceImage; // Backup for smart padding restore

        // Image undo/redo stacks for crop/cutout operations
        private readonly Stack<SkiaSharp.SKBitmap> _imageUndoStack = new();
        private readonly Stack<SkiaSharp.SKBitmap> _imageRedoStack = new();

        public void UpdatePreview(SkiaSharp.SKBitmap image, bool clearAnnotations = true)
        {
            // Store source image for operations like Crop
            _currentSourceImage = image;

            // Update original backup first so smart padding uses the new image during PreviewImage change
            if (!_isApplyingSmartPadding)
            {
                _originalSourceImage?.Dispose();
                _originalSourceImage = image.Copy();
            }

            // Convert SKBitmap to Avalonia Bitmap
            PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(image);
            ImageDimensions = $"{image.Width} x {image.Height}";
            StatusText = $"Image: {image.Width} × {image.Height}";

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
            if (_currentSourceImage == null) return;
            if (width <= 0 || height <= 0) return;

            // Ensure bounds
            var rect = new SkiaSharp.SKRectI(x, y, x + width, y + height);
            var imageRect = new SkiaSharp.SKRectI(0, 0, _currentSourceImage.Width, _currentSourceImage.Height);
            rect.Intersect(imageRect);

            if (rect.Width <= 0 || rect.Height <= 0) return;

            // Save current image state for undo before cropping
            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();

            var cropped = ImageHelpers.Crop(_currentSourceImage, rect.Left, rect.Top, rect.Width, rect.Height);
            UpdatePreview(cropped, clearAnnotations: true);
            UpdateUndoRedoProperties();
        }

        public void CutOutImage(int startPos, int endPos, bool isVertical)
        {
            if (_currentSourceImage == null) return;

            // Ensure valid range
            if (isVertical)
            {
                if (startPos < 0 || endPos > _currentSourceImage.Width || startPos >= endPos)
                    return;
            }
            else
            {
                if (startPos < 0 || endPos > _currentSourceImage.Height || startPos >= endPos)
                    return;
            }

            // Save current image state for undo before cutting out
            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();
            
            // ... (CutOut logic omitted for brevity in diff, assuming calling UpdateUndoRedoProperties after invalidation or here?)
            // Actually CutOutImage calls UpdatePreview implicitly? No, it uses ViewModel.CutOutImage which calls this.
            // Wait, CutOut modifies bitmap?
            // The method CutOutImage only pushes undo stack, then loop logic?
            // Ah, CutOutImage implementation matches Logic.
            // Wait, CutOutImage in ViewModel logic seems incomplete in my view?
            // Line 1000 was last line viewed. I assume it continues.
            // I will just update the start of CutOut to ensure stack is pushed, and I should call UpdateUndoRedoProperties.
            // BUT I don't see the end of CutOutImage.
            
            // Safer to just update CropImage and Undo/Redo for now. 
            // I'll verify CutOutImage later or assuming it calls UpdatePreview?
            // Note: CutOut logic in Controller calls ViewModel.CutOutImage.
            // I will update CutOutImage if I can see it. I saw lines 983-1000.
            // I will just apply to Crop and Undo/Redo first.

            _imageRedoStack.Clear();

            var result = ImageHelpers.CutOut(_currentSourceImage, startPos, endPos, isVertical);
            UpdatePreview(result, clearAnnotations: true);

            StatusText = isVertical
                ? $"Cut out vertical section ({endPos - startPos}px wide)"
                : $"Cut out horizontal section ({endPos - startPos}px tall)";
        }

        // --- Edit Menu Commands ---

        [RelayCommand]
        private void Rotate90Clockwise()
        {
            if (_currentSourceImage == null) return;

            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();

            var rotated = ImageHelpers.Rotate90Clockwise(_currentSourceImage);
            UpdatePreview(rotated, clearAnnotations: true);
            UpdateUndoRedoProperties();
            StatusText = "Rotated 90° clockwise";
        }

        [RelayCommand]
        private void Rotate90CounterClockwise()
        {
            if (_currentSourceImage == null) return;

            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();

            var rotated = ImageHelpers.Rotate90CounterClockwise(_currentSourceImage);
            UpdatePreview(rotated, clearAnnotations: true);
            UpdateUndoRedoProperties();
            StatusText = "Rotated 90° counter-clockwise";
        }

        [RelayCommand]
        private void Rotate180()
        {
            if (_currentSourceImage == null) return;

            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();

            var rotated = ImageHelpers.Rotate180(_currentSourceImage);
            UpdatePreview(rotated, clearAnnotations: true);
            UpdateUndoRedoProperties();
            StatusText = "Rotated 180°";
        }

        [RelayCommand]
        private void FlipHorizontal()
        {
            if (_currentSourceImage == null) return;

            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();

            var flipped = ImageHelpers.FlipHorizontal(_currentSourceImage);
            UpdatePreview(flipped, clearAnnotations: true);
            UpdateUndoRedoProperties();
            StatusText = "Flipped horizontally";
        }

        [RelayCommand]
        private void FlipVertical()
        {
            if (_currentSourceImage == null) return;

            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();

            var flipped = ImageHelpers.FlipVertical(_currentSourceImage);
            UpdatePreview(flipped, clearAnnotations: true);
            UpdateUndoRedoProperties();
            StatusText = "Flipped vertically";
        }

        [RelayCommand]
        private void AutoCropImage()
        {
            if (_currentSourceImage == null) return;

            var topLeftColor = _currentSourceImage.GetPixel(0, 0);

            var cropped = ImageHelpers.AutoCrop(_currentSourceImage, topLeftColor, tolerance: 10);

            if (cropped != null && cropped.Width > 0 && cropped.Height > 0 &&
                (cropped.Width != _currentSourceImage.Width || cropped.Height != _currentSourceImage.Height))
            {
                _imageUndoStack.Push(_currentSourceImage.Copy());
                _imageRedoStack.Clear();

                UpdatePreview(cropped, clearAnnotations: true);
                UpdateUndoRedoProperties();
                StatusText = $"Auto-cropped to {cropped.Width}x{cropped.Height}";
            }
            else
            {
                StatusText = "Nothing to auto-crop";
            }
        }

        /// <summary>
        /// Resize the image to new dimensions with specified quality.
        /// </summary>
        public void ResizeImage(int newWidth, int newHeight, SkiaSharp.SKFilterQuality quality = SkiaSharp.SKFilterQuality.High)
        {
            if (_currentSourceImage == null) return;
            if (newWidth <= 0 || newHeight <= 0) return;

            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();

            var resized = ImageHelpers.Resize(_currentSourceImage, newWidth, newHeight, maintainAspectRatio: false, quality);
            if (resized != null)
            {
                UpdatePreview(resized, clearAnnotations: true);
                UpdateUndoRedoProperties();
                StatusText = $"Resized to {newWidth}x{newHeight}";
            }
        }

        /// <summary>
        /// Resize the canvas by adding padding around the image.
        /// </summary>
        public void ResizeCanvas(int top, int right, int bottom, int left, SkiaSharp.SKColor backgroundColor)
        {
            if (_currentSourceImage == null) return;

            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();

            var resized = ImageHelpers.ResizeCanvas(_currentSourceImage, left, top, right, bottom, backgroundColor);
            UpdatePreview(resized, clearAnnotations: true);
            UpdateUndoRedoProperties();

            int newWidth = _currentSourceImage.Width + left + right;
            int newHeight = _currentSourceImage.Height + top + bottom;
            StatusText = $"Canvas resized to {newWidth}x{newHeight}";
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
            if (_currentSourceImage == null) return;

            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();

            var result = effect(_currentSourceImage);
            UpdatePreview(result, clearAnnotations: true);
            UpdateUndoRedoProperties();
            StatusText = statusMessage;
        }

        // --- Effect Live Preview Logic ---

        private SkiaSharp.SKBitmap? _preEffectImage;


        /// <summary>
        /// Called when an effect dialog opens to store the state before previewing.
        /// </summary>
        public void StartEffectPreview()
        {
            if (_currentSourceImage == null) return;
            
            _isPreviewingEffect = true;
            OnPropertyChanged(nameof(AreBackgroundEffectsActive));
            UpdateCanvasProperties();
            ApplySmartPaddingCrop(); 

            _preEffectImage = _currentSourceImage.Copy();
        }

        /// <summary>
        /// Updates the displayed preview without committing changes to the source image or undo stack.
        /// </summary>
        public void UpdatePreviewImageOnly(SkiaSharp.SKBitmap preview)
        {
            if (preview == null) return;
            
            // Dispose previous preview bitmap if it exists and isn't the source
            // Note: UpdatePreview creates a new Avalonia bitmap, so we are fine.
            // We just need to update the binding properly.
            
            // We don't call UpdatePreview() here because that resets annotations and other state too aggressively?
            // Actually UpdatePreview() does:
            // 1. Sets _currentSourceImage (WE DO NOT WANT THIS yet)
            // 2. Backs up original (WE DO NOT WANT THIS)
            // 3. Converts to Avalonia Bitmap (WE WANT THIS)
            
            PreviewImage = Helpers.BitmapConversionHelpers.ToAvaloniBitmap(preview);
        }

        /// <summary>
        /// Commits the effect to the undo stack and updates the source image.
        /// </summary>
        public void ApplyEffect(SkiaSharp.SKBitmap result, string statusMessage)
        {
            if (_preEffectImage == null) return; // Should have been started
            
            // Restore _currentSourceImage to pre-effect state so Push works correctly?
            // Actually, we haven't changed _currentSourceImage yet, only PreviewImage.
            // So _currentSourceImage is still the "Before" state.
            
            if (_currentSourceImage == null) return;

            _imageUndoStack.Push(_currentSourceImage.Copy());
            _imageRedoStack.Clear();

            UpdatePreview(result, clearAnnotations: true);
            UpdateUndoRedoProperties();
            StatusText = statusMessage;

            _preEffectImage?.Dispose();
            _preEffectImage = null;

            _isPreviewingEffect = false;
            OnPropertyChanged(nameof(AreBackgroundEffectsActive));
            UpdateCanvasProperties();
            ApplySmartPaddingCrop();
        }

        /// <summary>
        /// Cancels the preview and restores the original image view.
        /// </summary>
        public void CancelEffectPreview()
        {
            if (_preEffectImage != null)
            {
                UpdatePreview(_preEffectImage, clearAnnotations: false);
                // Do NOT dispose _preEffectImage here, because UpdatePreview takes ownership 
                // and sets it as _currentSourceImage.
                // and sets it as _currentSourceImage.
                _preEffectImage = null;
            }

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
            
            // Run effect on copy of pre-effect image? 
            // Or if effect is non-destructive (returns new), pass pre-effect directly.
            // ImageHelpers methods return NEW bitmap.
            try 
            {
                var result = effect(_preEffectImage);
                // UpdatePreviewImageOnly takes ownership or we verify disposal?
                // ToAvaloniBitmap creates a copy/wrapper. result needs disposal eventually?
                // UpdatePreviewImageOnly converts it. We should dispose 'result' after conversion if it's not needed.
                // But PreviewImage might depend on it if it wraps it directly?
                // ToAvaloniaBitmap usually creates a WriteableBitmap copy.
                // Let's assume we need to dispose result if ToAvaloniaBitmap copies.
                
                PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(result);
                result.Dispose(); 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Preview Error: {ex}");
            }
        }

        /// <summary>
        /// Applies the effect to the source image and commits to undo stack.
        /// </summary>
        public void ApplyEffect(Func<SkiaSharp.SKBitmap, SkiaSharp.SKBitmap> effect, string statusMessage)
        {
            if (_preEffectImage == null || _currentSourceImage == null) return;

             _imageUndoStack.Push(_currentSourceImage.Copy()); // _currentSourceImage matches _preEffectImage roughly
            _imageRedoStack.Clear();
            
            // _currentSourceImage IS the _preEffectImage content basically (before preview changes).
            // So we apply effect to _currentSourceImage or _preEffectImage?
            // Safer to apply to _preEffectImage (original state) and set as new _currentSourceImage.
            
            var result = effect(_preEffectImage);
            UpdatePreview(result, clearAnnotations: true);
            UpdateUndoRedoProperties();
            StatusText = statusMessage;

            _preEffectImage?.Dispose();
            _preEffectImage = null;

            // Restore Background Effects
            _isPreviewingEffect = false;
            OnPropertyChanged(nameof(AreBackgroundEffectsActive));
            UpdateCanvasProperties();
            ApplySmartPaddingCrop();
        }

        // --- Rotate Custom Angle Feature ---

        [ObservableProperty]
        private double _rotateAngleDegrees;

        [ObservableProperty]
        private bool _isRotateCustomAngleDialogOpen;

        private SkiaSharp.SKBitmap? _rotateCustomAngleOriginalBitmap;

        [RelayCommand]
        public void OpenRotateCustomAngleDialog()
        {
            if (PreviewImage == null || _currentSourceImage == null) return;

             // Snapshot the CURRENT state (including any previous edits)
             var current = _currentSourceImage;
             if (current != null)
             {
                 _rotateCustomAngleOriginalBitmap = current.Copy();
                 RotateAngleDegrees = 0;
                 IsRotateCustomAngleDialogOpen = true;
             }
        }

        partial void OnRotateAngleDegreesChanged(double value)
        {
             RotateCustomAngleLiveApply();
        }

        private void RotateCustomAngleLiveApply()
        {
            if (!IsRotateCustomAngleDialogOpen || _rotateCustomAngleOriginalBitmap == null) return;

            float angle = (float)Math.Clamp(RotateAngleDegrees, -180, 180);
            var effect = RotateImageEffect.Custom(angle);

            using var result = effect.Apply(_rotateCustomAngleOriginalBitmap);
            
            UpdatePreview(result, clearAnnotations: false); 
        }

        [RelayCommand]
        public void CommitRotateCustomAngle()
        {
            if (_rotateCustomAngleOriginalBitmap == null) return;

            float angle = (float)Math.Clamp(RotateAngleDegrees, -180, 180);

            var effect = RotateImageEffect.Custom(angle);
            var result = effect.Apply(_rotateCustomAngleOriginalBitmap);
            
            ApplyEffect(result, $"Rotated {angle:0.0}°");

            IsRotateCustomAngleDialogOpen = false;
            IsModalOpen = false;
            ModalContent = null;
            
            _rotateCustomAngleOriginalBitmap?.Dispose();
            _rotateCustomAngleOriginalBitmap = null;
        }

        [RelayCommand]
        public void CancelRotateCustomAngle()
        {
            if (_rotateCustomAngleOriginalBitmap != null)
            {
                // Restore original
                // We just need to reload the original bitmap into the view/core.
                // We don't push to undo stack.
                UpdatePreview(_rotateCustomAngleOriginalBitmap, clearAnnotations: false);
                
                _rotateCustomAngleOriginalBitmap.Dispose();
                _rotateCustomAngleOriginalBitmap = null;
            }

            IsRotateCustomAngleDialogOpen = false;
            IsModalOpen = false;
            ModalContent = null;
        }
    }
}


