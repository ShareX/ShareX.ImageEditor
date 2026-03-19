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
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.Theming;
using ShareX.ImageEditor.Presentation.ViewModels;
using SkiaSharp;
using System.Reflection;

namespace ShareX.ImageEditor.Presentation.Views
{
    public partial class EditorWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private string? _pendingFilePath;
        private bool _allowClose;
        private IPlatformSettings? _platformSettings;

        public EditorWindow() : this(null)
        {
        }

        public EditorWindow(ImageEditorOptions? options)
        {
            InitializeComponent();

            _viewModel = new MainViewModel(options);
            DataContext = _viewModel;
            _viewModel.WindowTitle = GetWindowTitle(null);

            // Defer image loading until EditorView is loaded and subscribed
            this.Loaded += OnWindowLoaded;

            // Set initial theme and subscribe to changes
            RequestedThemeVariant = ThemeManager.ShareXDark;
            ThemeManager.ThemeChanged += OnThemeChanged;

            Opened += OnWindowOpened;
            Closed += OnWindowClosed;

            if (_viewModel.Options.UseSystemAccentColor)
            {
                SetPlatformSettings(Application.Current?.PlatformSettings);
                UpdateAccentColor();
            }

            _viewModel.CloseRequested += (s, e) =>
            {
                _allowClose = true;
                Close();
            };
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (_allowClose)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;
            _viewModel.RequestClose();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnThemeChanged(object? sender, Avalonia.Styling.ThemeVariant theme)
        {
            RequestedThemeVariant = theme;

            if (_viewModel.Options.UseSystemAccentColor)
            {
                UpdateAccentColor();
            }
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            if (!_viewModel.Options.UseSystemAccentColor)
            {
                return;
            }

            SetPlatformSettings(PlatformSettings ?? Application.Current?.PlatformSettings);
            UpdateAccentColor();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            SetPlatformSettings(null);
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
            UpdateAccentColor(colorValues);
        }

        private void UpdateAccentColor(PlatformColorValues? colorValues = null)
        {
            if (!_viewModel.Options.UseSystemAccentColor)
            {
                return;
            }

            colorValues ??= _platformSettings?.GetColorValues() ?? Application.Current?.PlatformSettings?.GetColorValues();

            if (colorValues == null || colorValues.AccentColor1.A == 0)
            {
                return;
            }

            Color startColor = colorValues.AccentColor1;
            Color endColor = DarkenColor(startColor, 0.10);

            if (this.FindControl<EditorView>("EditorViewControl") is not { } editorView)
            {
                return;
            }

            editorView.Resources["ShareX.Color.Accent.Start"] = startColor;
            editorView.Resources["ShareX.Color.Accent.End"] = endColor;
            UpdateAccentBrush(editorView, ThemeManager.ShareXDark, startColor, endColor);
            UpdateAccentBrush(editorView, ThemeManager.ShareXLight, startColor, endColor);
        }

        private static void UpdateAccentBrush(EditorView editorView, Avalonia.Styling.ThemeVariant theme, Color startColor, Color endColor)
        {
            if (!editorView.Resources.TryGetResource("ShareX.Brush.Accent", theme, out object? accentBrushValue) ||
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

        private static Color DarkenColor(Color color, double amount)
        {
            double factor = Math.Clamp(1 - amount, 0, 1);

            return Color.FromArgb(
                color.A,
                (byte)Math.Clamp((int)Math.Round(color.R * factor), 0, byte.MaxValue),
                (byte)Math.Clamp((int)Math.Round(color.G * factor), 0, byte.MaxValue),
                (byte)Math.Clamp((int)Math.Round(color.B * factor), 0, byte.MaxValue));
        }

        private void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Now that EditorView is loaded and subscribed to ViewModel, load the image
            if (!string.IsNullOrEmpty(_pendingFilePath))
            {
                LoadImageInternal(_pendingFilePath);
                _pendingFilePath = null;
            }
        }

        /// <summary>
        /// Loads an image from the specified file path.
        /// If called before window is loaded, defers loading until EditorView is ready.
        /// </summary>
        /// <param name="filePath">Absolute path to the image file.</param>
        public void LoadImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            if (!File.Exists(filePath))
            {
                return;
            }

            // If window not loaded yet, defer image loading
            if (!IsLoaded)
            {
                _pendingFilePath = filePath;
                return;
            }

            LoadImageInternal(filePath);
        }

        private void LoadImageInternal(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var bitmap = new Bitmap(stream);
                _viewModel.RequestZoomToFitOnNextImageLoad();
                _viewModel.PreviewImage = bitmap;
                _viewModel.LastSavedPath = filePath;
                _viewModel.ImageFilePath = filePath;
                _viewModel.ImageDimensions = $"{bitmap.Size.Width} x {bitmap.Size.Height}";
                _viewModel.WindowTitle = GetWindowTitle(_viewModel.ImageDimensions);
                _viewModel.IsDirty = false;
            }
            catch (Exception ex)
            {
                EditorServices.ReportError(nameof(EditorWindow), $"Failed to load image file '{filePath}'.", ex);
            }
        }

        /// <summary>
        /// Loads an image from a stream.
        /// </summary>
        /// <param name="stream">Stream containing image data.</param>
        public void LoadImage(Stream stream)
        {
            if (stream == null) return;
            try
            {
                // Ensure stream position is at beginning if possible
                if (stream.CanSeek && stream.Position != 0)
                    stream.Position = 0;

                var bitmap = new Bitmap(stream);
                _viewModel.RequestZoomToFitOnNextImageLoad();
                _viewModel.PreviewImage = bitmap;
                _viewModel.ImageDimensions = $"{bitmap.Size.Width} x {bitmap.Size.Height}";
                _viewModel.WindowTitle = GetWindowTitle(_viewModel.ImageDimensions);
                _viewModel.IsDirty = false;
            }
            catch (Exception ex)
            {
                EditorServices.ReportError(nameof(EditorWindow), "Failed to load image from stream.", ex);
            }
        }

        private static string GetWindowTitle(string? dimensions)
        {
            return string.IsNullOrEmpty(dimensions)
                ? "ShareX - Image Editor"
                : $"ShareX - Image Editor - {dimensions}";
        }

        private static string GetVersionString()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (!string.IsNullOrEmpty(info?.InformationalVersion))
            {
                return info.InformationalVersion;
            }

            var version = asm.GetName().Version;
            return version?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Gets the current edited image as a SkiaSharp SKBitmap.
        /// </summary>
        public SKBitmap? GetResultBitmap()
        {
            var editorView = this.FindControl<EditorView>("EditorViewControl");
            return editorView?.GetSnapshot();
        }

        public SKBitmap? GetSourceBitmap()
        {
            var editorView = this.FindControl<EditorView>("EditorViewControl");
            return editorView?.GetSource();
        }

        /// <summary>
        /// Gets the encoded image data in the specified format. 
        /// Useful for interoperability with other frameworks (e.g. WinForms).
        /// </summary>
        public byte[]? GetResultBytes(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
        {
            using var bitmap = GetResultBitmap();
            if (bitmap == null) return null;

            using var data = bitmap.Encode(format, quality);
            return data.ToArray();
        }

        /// <summary>
        /// Gets the encoded image data in the specified format. 
        /// Useful for interoperability with other frameworks (e.g. WinForms).
        /// </summary>
        public byte[]? GetSourceBytes(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
        {
            using var bitmap = GetSourceBitmap();
            if (bitmap == null) return null;

            using var data = bitmap.Encode(format, quality);
            return data.ToArray();
        }
    }
}
