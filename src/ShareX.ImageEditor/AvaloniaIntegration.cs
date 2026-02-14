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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using ShareX.ImageEditor.ViewModels;
using ShareX.ImageEditor.Views;

namespace ShareX.ImageEditor
{
    public class AvaloniaApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // No main window here, we manage windows manually
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }

    public class EditorEvents
    {
        public Func<byte[], Task>? CopyImageRequested { get; set; }
        public Func<byte[], Task>? SaveImageRequested { get; set; }
        public Func<byte[], Task>? SaveImageAsRequested { get; set; }
        public Action<byte[]>? PinImageRequested { get; set; }
        public Func<byte[], Task>? UploadImageRequested { get; set; }
    }

    public static class AvaloniaIntegration
    {
        private static bool initialized = false;

        private static void Initialize()
        {
            if (!initialized)
            {
                if (Application.Current == null)
                {
                    AppBuilder.Configure<AvaloniaApp>()
                        .UsePlatformDetect()
                        .WithInterFont()
                        .LogToTrace()
                        .SetupWithoutStarting();
                }

                initialized = true;
            }
        }

        public static void ShowEditor(string filePath)
        {
            Initialize();
            EditorWindow window = new EditorWindow();
            window.Show();

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                window.LoadImage(filePath);
            }
        }

        public static void ShowEditor(Stream imageStream)
        {
            Initialize();
            EditorWindow window = new EditorWindow();
            window.Show();

            if (imageStream != null)
            {
                window.LoadImage(imageStream);
            }
        }

        public static byte[]? ShowEditorDialog(Stream imageStream, EditorEvents? events = null)
        {
            Initialize();
            byte[]? result = null;

            EditorWindow window = new EditorWindow();
            if (imageStream != null) window.LoadImage(imageStream);

            SetupEvents(window, events, () =>
            {
                result = window.GetResultBytes();
            });

            window.Show();
            DispatcherFrame frame = new DispatcherFrame();
            window.Closed += (s, e) => frame.Continue = false;
            Dispatcher.UIThread.PushFrame(frame);

            return result;
        }

        private static void SetupEvents(EditorWindow window, EditorEvents? events, Action onResult)
        {
            if (events == null) return;

            MainViewModel? vm = window.DataContext as MainViewModel;
            if (vm == null) return;

            if (events.SaveImageRequested != null)
            {
                vm.SaveRequested += async () =>
                {
                    byte[]? bytes = window.GetResultBytes();
                    if (bytes != null) await events.SaveImageRequested(bytes);
                };
            }

            if (events.CopyImageRequested != null)
            {
                vm.CopyRequested += async (img) =>
                {
                    byte[]? bytes = window.GetResultBytes();
                    if (bytes != null) await events.CopyImageRequested(bytes);
                };
            }

            if (events.UploadImageRequested != null)
            {
                vm.UploadRequested += async (img) =>
                {
                    byte[]? bytes = window.GetResultBytes();
                    if (bytes != null) await events.UploadImageRequested(bytes);
                };
            }

            if (events.PinImageRequested != null)
            {
                vm.PinRequested += (s, e) =>
                {
                    byte[]? bytes = window.GetResultBytes();
                    if (bytes != null) events.PinImageRequested(bytes);
                };
            }

            if (events.SaveImageAsRequested != null)
            {
                vm.SaveAsRequested += async () =>
                {
                    byte[]? bytes = window.GetResultBytes();
                    if (bytes != null) await events.SaveImageAsRequested(bytes);
                };
            }

            window.Closed += (s, e) =>
            {
                onResult();
            };
        }
    }
}