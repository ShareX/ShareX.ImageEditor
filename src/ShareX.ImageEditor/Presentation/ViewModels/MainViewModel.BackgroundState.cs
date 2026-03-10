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

using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.ImageEditor.Hosting;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ShareX.ImageEditor.Presentation.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        public enum CanvasBackgroundMode
        {
            Gradient,
            Color,
            Transparent,
            Image,
            Wallpaper
        }

        public sealed class BackgroundModeOption
        {
            public required CanvasBackgroundMode Mode { get; init; }
            public required string DisplayName { get; init; }

            public override string ToString() => DisplayName;
        }

        private const int SpiGetDesktopWallpaper = 0x0073;
        private const int MaxWallpaperPath = 260;

        private Bitmap? _backgroundBitmap;

        public ObservableCollection<BackgroundModeOption> BackgroundModeOptions { get; }

        [ObservableProperty]
        private BackgroundModeOption? _selectedBackgroundModeOption;

        [ObservableProperty]
        private GradientPreset? _selectedGradientPreset;

        [ObservableProperty]
        private string _backgroundColor = "#FFFFFFFF";

        [ObservableProperty]
        private string? _backgroundImagePath;

        public bool IsGradientBackgroundModeSelected => SelectedBackgroundMode == CanvasBackgroundMode.Gradient;
        public bool IsColorBackgroundModeSelected => SelectedBackgroundMode == CanvasBackgroundMode.Color;
        public bool IsImageBackgroundModeSelected => SelectedBackgroundMode == CanvasBackgroundMode.Image;
        public bool HasBackgroundImagePath => !string.IsNullOrWhiteSpace(BackgroundImagePath);

        public IBrush BackgroundColorBrush
        {
            get => new SolidColorBrush(Color.Parse(BackgroundColor));
            set
            {
                if (value is SolidColorBrush solidBrush)
                {
                    BackgroundColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                }
            }
        }

        public Color BackgroundColorValue
        {
            get => Color.Parse(BackgroundColor);
            set => BackgroundColor = $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
        }

        partial void OnSelectedBackgroundModeOptionChanged(BackgroundModeOption? value)
        {
            OnPropertyChanged(nameof(IsGradientBackgroundModeSelected));
            OnPropertyChanged(nameof(IsColorBackgroundModeSelected));
            OnPropertyChanged(nameof(IsImageBackgroundModeSelected));
            ApplySelectedBackgroundMode();
        }

        partial void OnSelectedGradientPresetChanged(GradientPreset? value)
        {
            if (value != null && SelectedBackgroundMode == CanvasBackgroundMode.Gradient)
            {
                ApplyGradientBackground(value);
            }
        }

        partial void OnBackgroundColorChanged(string value)
        {
            OnPropertyChanged(nameof(BackgroundColorBrush));
            OnPropertyChanged(nameof(BackgroundColorValue));

            if (SelectedBackgroundMode == CanvasBackgroundMode.Color)
            {
                ApplyColorBackground(BackgroundColorValue);
            }
        }

        partial void OnBackgroundImagePathChanged(string? value)
        {
            OnPropertyChanged(nameof(HasBackgroundImagePath));

            if (SelectedBackgroundMode == CanvasBackgroundMode.Image)
            {
                ApplyImageBackground(value);
            }
        }

        public void SetBackgroundImagePath(string? filePath)
        {
            bool isSamePath = string.Equals(BackgroundImagePath, filePath, StringComparison.Ordinal);
            BackgroundImagePath = filePath;

            if (isSamePath && SelectedBackgroundMode == CanvasBackgroundMode.Image)
            {
                ApplyImageBackground(filePath);
            }
        }

        private CanvasBackgroundMode SelectedBackgroundMode =>
            SelectedBackgroundModeOption?.Mode ?? CanvasBackgroundMode.Transparent;

        private void ApplySelectedBackgroundMode()
        {
            switch (SelectedBackgroundMode)
            {
                case CanvasBackgroundMode.Gradient:
                    if (SelectedGradientPreset != null)
                    {
                        ApplyGradientBackground(SelectedGradientPreset);
                    }
                    break;
                case CanvasBackgroundMode.Color:
                    ApplyColorBackground(BackgroundColorValue);
                    break;
                case CanvasBackgroundMode.Transparent:
                    ApplyTransparentBackground();
                    break;
                case CanvasBackgroundMode.Image:
                    ApplyImageBackground(BackgroundImagePath);
                    break;
                case CanvasBackgroundMode.Wallpaper:
                    ApplyWallpaperBackground();
                    break;
            }
        }

        private void ApplyGradientBackground(GradientPreset preset)
        {
            SetCanvasBackground(CopyBrush(preset.Brush));
        }

        private void ApplyColorBackground(Color color)
        {
            SetCanvasBackground(new SolidColorBrush(color));
        }

        private void ApplyTransparentBackground()
        {
            SetCanvasBackground(Brushes.Transparent);
        }

        private void ApplyImageBackground(string? filePath)
        {
            if (!TryCreateImageBrushFromPath(filePath, out ImageBrush? brush, out Bitmap? bitmap))
            {
                SetCanvasBackground(Brushes.Transparent);
                return;
            }

            SetCanvasBackground(brush!, bitmap);
        }

        private void ApplyWallpaperBackground()
        {
            if (!TryGetWindowsWallpaperPath(out string? wallpaperPath))
            {
                EditorServices.ReportWarning(nameof(MainViewModel), "Failed to locate the current Windows wallpaper.");
                SetCanvasBackground(Brushes.Transparent);
                return;
            }

            if (!TryCreateImageBrushFromPath(wallpaperPath, out ImageBrush? brush, out Bitmap? bitmap))
            {
                SetCanvasBackground(Brushes.Transparent);
                return;
            }

            SetCanvasBackground(brush!, bitmap);
        }

        private void SetCanvasBackground(IBrush brush, Bitmap? ownedBitmap = null)
        {
            Bitmap? previousBitmap = _backgroundBitmap;
            _backgroundBitmap = ownedBitmap;
            CanvasBackground = brush;

            if (previousBitmap != null && !ReferenceEquals(previousBitmap, ownedBitmap))
            {
                previousBitmap.Dispose();
            }
        }

        private BackgroundModeOption FindBackgroundModeOption(CanvasBackgroundMode mode)
        {
            return BackgroundModeOptions.FirstOrDefault(option => option.Mode == mode) ?? BackgroundModeOptions[0];
        }

        private static ObservableCollection<BackgroundModeOption> BuildBackgroundModeOptions()
        {
            ObservableCollection<BackgroundModeOption> options =
            [
                new() { Mode = CanvasBackgroundMode.Gradient, DisplayName = "Gradient" },
                new() { Mode = CanvasBackgroundMode.Color, DisplayName = "Color" },
                new() { Mode = CanvasBackgroundMode.Transparent, DisplayName = "Transparent" },
                new() { Mode = CanvasBackgroundMode.Image, DisplayName = "Image" }
            ];

            if (OperatingSystem.IsWindows())
            {
                options.Add(new BackgroundModeOption { Mode = CanvasBackgroundMode.Wallpaper, DisplayName = "Wallpaper" });
            }

            return options;
        }

        private static bool TryCreateImageBrushFromPath(string? filePath, out ImageBrush? brush, out Bitmap? bitmap)
        {
            brush = null;
            bitmap = null;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using FileStream stream = File.OpenRead(filePath);
                bitmap = new Bitmap(stream);
                brush = new ImageBrush(bitmap)
                {
                    Stretch = Stretch.UniformToFill
                };

                return true;
            }
            catch (Exception ex)
            {
                bitmap?.Dispose();
                bitmap = null;
                EditorServices.ReportWarning(nameof(MainViewModel), $"Failed to load background image '{filePath}'.", ex);
                return false;
            }
        }

        private static bool TryGetWindowsWallpaperPath(out string? path)
        {
            path = null;

            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            StringBuilder buffer = new StringBuilder(MaxWallpaperPath);
            if (!SystemParametersInfo(SpiGetDesktopWallpaper, buffer.Capacity, buffer, 0))
            {
                return false;
            }

            string wallpaperPath = buffer.ToString().TrimEnd('\0');
            if (string.IsNullOrWhiteSpace(wallpaperPath) || !File.Exists(wallpaperPath))
            {
                return false;
            }

            path = wallpaperPath;
            return true;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfo(int uiAction, int uiParam, StringBuilder pvParam, int fWinIni);
    }
}