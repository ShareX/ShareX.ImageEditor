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

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using ShareX.ImageEditor.ViewModels;
using SkiaSharp;

namespace ShareX.ImageEditor.Views
{
    public partial class EditorWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private string? _pendingFilePath;

        public EditorWindow() : this(null)
        {
        }

        public EditorWindow(EditorOptions? options)
        {
            InitializeComponent();

            _viewModel = new MainViewModel(options);
            DataContext = _viewModel;

            // Defer image loading until EditorView is loaded and subscribed
            this.Loaded += OnWindowLoaded;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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
                System.Diagnostics.Debug.WriteLine($"EditorWindow: File not found: {filePath}");
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
                _viewModel.PreviewImage = bitmap;
                _viewModel.LastSavedPath = filePath;
                _viewModel.ImageDimensions = $"{bitmap.Size.Width} x {bitmap.Size.Height}";
                _viewModel.WindowTitle = $"ShareX - Image Editor - {_viewModel.ImageDimensions}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EditorWindow: Failed to load image from path '{filePath}': {ex}");
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
                _viewModel.PreviewImage = bitmap;
                _viewModel.ImageDimensions = $"{bitmap.Size.Width} x {bitmap.Size.Height}";
                _viewModel.WindowTitle = $"ShareX - Image Editor - {_viewModel.ImageDimensions}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EditorWindow: Failed to load image from stream: {ex}");
            }
        }

        /// <summary>
        /// Gets the current edited image as a SkiaSharp SKBitmap.
        /// </summary>
        public SKBitmap? GetResultBitmap()
        {
            var editorView = this.FindControl<EditorView>("EditorViewControl");
            return editorView?.GetSnapshot();
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
    }
}