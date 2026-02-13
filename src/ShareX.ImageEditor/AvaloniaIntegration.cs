using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using ShareX.ImageEditor.Views;
using System;
using System.IO;

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

    public static class AvaloniaIntegration
    {
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (!_initialized)
            {
                if (Application.Current == null)
                {
                    AppBuilder.Configure<AvaloniaApp>()
                        .UsePlatformDetect()
                        .WithInterFont()
                        .LogToTrace()
                        .SetupWithoutStarting();
                }
                
                _initialized = true;
            }
        }

        public static void ShowEditor(string filePath)
        {
            Initialize();

            // We need to run this on the UI thread if we are already in an Avalonia context, 
            // but since ShareX is WinForms, we likely need to be careful about threading.
            // However, SetupWithoutStarting() initializes Avalonia on the current thread (which should be UI thread of ShareX).
            // Let's assume this is called from ShareX's UI thread or InvokeSafe.

            var window = new EditorWindow();
            window.Show();
            
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                window.LoadImage(filePath);
            }
        }

        public static void ShowEditor(Stream imageStream)
        {
            Initialize();

            var window = new EditorWindow();
            window.Show();

            if (imageStream != null)
            {
                window.LoadImage(imageStream);
            }
        }
    }
}
