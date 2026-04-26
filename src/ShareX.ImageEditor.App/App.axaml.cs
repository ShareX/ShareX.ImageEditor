#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
using SkiaSharp;
using System;
using System.IO;

namespace ShareX.ImageEditor.App
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                EditorServices.EnsureDefaultDesktopWallpaperService();

                ImageEditorOptions options = new ImageEditorOptions();

#if DEBUG
                options.ShowExitConfirmation = false;
#endif

                EditorWindow window = new EditorWindow(options);
                desktop.MainWindow = window;

                if (window.DataContext is MainViewModel vm)
                {
                    vm.ShowFileMenu = true;
                    vm.ShowTaskButtons = true;
                    vm.UseContinueWorkflow = false;
                    vm.ShowBottomToolbar = true;
                    vm.ShowStartScreen = true;

                    string? imagePath = GetImagePathFromArgs(desktop.Args);

                    if (imagePath != null)
                    {
                        window.LoadImage(imagePath);
                    }
                    else
                    {
#if DEBUG
                        LoadExampleImage(vm);
#endif
                    }

                    vm.CopyRequested += async () =>
                    {
                        IClipboard? clipboard = desktop.MainWindow?.Clipboard;

                        if (clipboard != null)
                        {
                            byte[]? bytes = window.GetResultBytes();

                            if (bytes != null)
                            {
                                using (MemoryStream stream = new MemoryStream(bytes))
                                {
                                    Bitmap bitmap = new Bitmap(stream);
                                    DataTransfer data = new DataTransfer();
                                    DataTransferItem item = new DataTransferItem();
                                    item.SetBitmap(bitmap);
                                    data.Add(item);

                                    await clipboard.SetDataAsync(data);
                                }
                            }
                        }
                    };
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".ico"];

        private string? GetImagePathFromArgs(string[]? args)
        {
            if (args == null || args.Length == 0)
            {
                return null;
            }

            string filePath = args[0];

            if (File.Exists(filePath))
            {
                string extension = Path.GetExtension(filePath);

                if (Array.Exists(ImageExtensions, ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                {
                    return filePath;
                }
            }

            return null;
        }

        private void LoadExampleImage(MainViewModel vm)
        {
            string location = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(location, "Assets", "Sample.png");

            if (File.Exists(path))
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    SKBitmap skBitmap = SKBitmap.Decode(stream);

                    vm.UpdatePreview(skBitmap);
                }
            }
        }
    }
}
