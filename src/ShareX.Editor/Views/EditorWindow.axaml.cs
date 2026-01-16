#pragma warning disable CS0618 // FileDialogFilter is obsolete
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using ShareX.Editor.ViewModels;
using SkiaSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ShareX.Editor.Views
{
    public partial class EditorWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private string? _pendingFilePath;

        public EditorWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // Hook up ViewModel events
            _viewModel.SaveAsRequested += OnSaveAsRequested;
            
            // If the window is closed, we might want to clean up
            this.Closed += OnClosed;
            
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

        private void OnClosed(object? sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.SaveAsRequested -= OnSaveAsRequested;
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

        private async Task<string?> OnSaveAsRequested()
        {
            var storageProvider = StorageProvider;
            if (storageProvider == null) return null;

            var file = await storageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save Image",
                DefaultExtension = "png",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("PNG Image")
                    {
                        Patterns = new[] { "*.png" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("JPEG Image")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            return file?.Path.LocalPath;
        }
    }
}
