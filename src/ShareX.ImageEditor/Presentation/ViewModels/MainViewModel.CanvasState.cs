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
using CommunityToolkit.Mvvm.Input;
using ShareX.ImageEditor.Core.ImageEffects.Helpers;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.Rendering;
using System.Collections.ObjectModel;

namespace ShareX.ImageEditor.Presentation.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
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
            catch (Exception ex)
            {
                EditorServices.ReportError(nameof(MainViewModel), "Failed to apply smart padding crop.", ex);
            }
            finally
            {
                _isApplyingSmartPadding = false;
            }
        }

        [RelayCommand]
        private void ApplyGradientPreset(GradientPreset preset)
        {
            bool wasGradientMode = SelectedBackgroundModeOption?.Mode == CanvasBackgroundMode.Gradient;
            SelectedGradientPreset = preset;

            if (!wasGradientMode)
            {
                SelectedBackgroundModeOption = FindBackgroundModeOption(CanvasBackgroundMode.Gradient);
            }
            else
            {
                ApplyGradientBackground(preset);
            }
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

            // Ordered so the most common day-to-day gradients stay near the top.
            return new ObservableCollection<GradientPreset>
            {
                new() { Name = "Sunset Glow", Brush = Make("#FF7E5F", "#FEB47B") },
                new() { Name = "Ocean Breeze", Brush = Make("#2193B0", "#6DD5ED") },
                new() { Name = "Skyline", Brush = Make("#00C6FF", "#0072FF") },
                new() { Name = "Peach Bloom", Brush = Make("#ED4264", "#FFEDBC") },
                new() { Name = "Mint Fresh", Brush = Make("#11998E", "#38EF7D") },
                new() { Name = "Royal Blue", Brush = Make("#396AFC", "#2948FF") },
                new() { Name = "Warm Flame", Brush = Make("#FF9A9E", "#FAD0C4") },
                new() { Name = "Coral Candy", Brush = Make("#FF9966", "#FF5E62") },
                new() { Name = "Mango", Brush = Make("#FFE259", "#FFA751") },
                new() { Name = "Violet Dream", Brush = Make("#4776E6", "#8E54E9") },
                new() { Name = "Ruby Fire", Brush = Make("#DD2476", "#FF512F") },
                new() { Name = "Aqua Marine", Brush = Make("#1A2980", "#26D0CE") },
                new() { Name = "Instagram", Brush = Make("#833AB4", "#FD1D1D") },
                new() { Name = "Aurora Green", Brush = Make("#00F260", "#0575E6") },
                new() { Name = "Citrus Pop", Brush = Make("#F7971E", "#FFD200") },
                new() { Name = "Arctic Sky", Brush = Make("#74EBD5", "#9FACE6") },
                new() { Name = "Cherry Rush", Brush = Make("#EB3349", "#F45C43") },
                new() { Name = "Lavender Sky", Brush = Make("#E0C3FC", "#8EC5FC") },
                new() { Name = "Midnight City", Brush = Make("#232526", "#414345") },
                new() { Name = "Deep Space", Brush = Make("#000428", "#004E92") },
                new() { Name = "Lagoon", Brush = Make("#43C6AC", "#191654") },
                new() { Name = "Rose Gold", Brush = Make("#EECDA3", "#EF629F") },
                new() { Name = "Emerald Water", Brush = Make("#348F50", "#56B4D3") },
                new() { Name = "Lemon Lime", Brush = Make("#56AB2F", "#A8E063") },
                new() { Name = "Pink Haze", Brush = Make("#EC008C", "#FC6767") },
                new() { Name = "Slate", Brush = Make("#BDC3C7", "#2C3E50") },
                new() { Name = "Carbon Steel", Brush = Make("#485563", "#29323C") },
                new() { Name = "Purple Bliss", Brush = Make("#8E2DE2", "#4A00E0") },
                new() { Name = "Ember", Brush = Make("#CB2D3E", "#EF473A") },
                new() { Name = "Soft Lilac", Brush = Make("#C471F5", "#FA71CD") },
                new() { Name = "Blue Hour", Brush = Make("#355C7D", "#6C5B7B") },
                new() { Name = "Golden Hour", Brush = Make("#F3904F", "#FFD194") },
                new() { Name = "Watermelon", Brush = Make("#FF5F6D", "#FFC371") },
                new() { Name = "Sea Foam", Brush = Make("#4CB8C4", "#3CD3AD") },
                new() { Name = "Twilight Plum", Brush = Make("#41295A", "#2F0743") },
                new() { Name = "Candy Grape", Brush = Make("#B24592", "#F15F79") },
                new() { Name = "Fresh Mint", Brush = Make("#76B852", "#8DC26F") },
                new() { Name = "Polar Night", Brush = Make("#283048", "#859398") },
                new() { Name = "Tropical Sun", Brush = Make("#F6D365", "#FDA085") },
                new() { Name = "Blue Raspberry", Brush = Make("#00B4DB", "#0083B0") },
                new() { Name = "Rosewater", Brush = Make("#E55D87", "#5FC3E4") },
                new() { Name = "Copper Glow", Brush = Make("#B79891", "#94716B") },
                new() { Name = "Aurora Violet", Brush = Make("#654EA3", "#EAAFC8") },
                new() { Name = "Apple Mint", Brush = Make("#5A3F37", "#2C7744") },
                new() { Name = "Sunrise Peach", Brush = Make("#F857A6", "#FF5858") },
                new() { Name = "Iceberg", Brush = Make("#C9FFBF", "#FFAFBD") },
                new() { Name = "Nightfall", Brush = Make("#141E30", "#243B55") },
                new() { Name = "Electric Cyan", Brush = Make("#0575E6", "#00F260") },
                new() { Name = "Velvet Rose", Brush = Make("#DA4453", "#89216B") },
                new() { Name = "Desert Sand", Brush = Make("#BA8B02", "#181818") }
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
    }
}
