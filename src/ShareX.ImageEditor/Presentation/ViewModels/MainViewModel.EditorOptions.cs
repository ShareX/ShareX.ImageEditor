using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;

namespace ShareX.ImageEditor.Presentation.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private static readonly IReadOnlyList<string> SupportedEditorThemes = new[]
        {
            "Dark",
            "Light"
        };

        public event EventHandler? OpenOptionsPanelRequested;

        public IReadOnlyList<string> EditorThemeOptions => SupportedEditorThemes;

        public bool EditorUseSystemTheme
        {
            get => Options.UseSystemTheme;
            set
            {
                if (Options.UseSystemTheme == value)
                {
                    return;
                }

                Options.UseSystemTheme = value;
                OnPropertyChanged(nameof(EditorUseSystemTheme));
                OnPropertyChanged(nameof(CanEditEditorTheme));
            }
        }

        public bool CanEditEditorTheme => !EditorUseSystemTheme;

        public string EditorTheme
        {
            get => NormalizeEditorTheme(Options.Theme);
            set
            {
                string normalizedTheme = NormalizeEditorTheme(value);
                if (string.Equals(Options.Theme, normalizedTheme, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Options.Theme = normalizedTheme;
                OnPropertyChanged(nameof(EditorTheme));
            }
        }

        public bool EditorUseSystemAccentColor
        {
            get => Options.UseSystemAccentColor;
            set
            {
                if (Options.UseSystemAccentColor == value)
                {
                    return;
                }

                Options.UseSystemAccentColor = value;
                OnPropertyChanged(nameof(EditorUseSystemAccentColor));
                OnPropertyChanged(nameof(CanEditEditorAccentColor));
            }
        }

        public bool CanEditEditorAccentColor => !EditorUseSystemAccentColor;

        public Color EditorAccentColor
        {
            get => Options.AccentColor;
            set
            {
                if (Options.AccentColor == value)
                {
                    return;
                }

                Options.AccentColor = value;
                OnPropertyChanged(nameof(EditorAccentColor));
                OnPropertyChanged(nameof(EditorAccentColorHex));
            }
        }

        public string EditorAccentColorHex => Options.AccentColorHex;

        public bool EditorRememberWindowState
        {
            get => Options.RememberWindowState;
            set
            {
                if (Options.RememberWindowState == value)
                {
                    return;
                }

                Options.RememberWindowState = value;
                OnPropertyChanged(nameof(EditorRememberWindowState));
            }
        }

        public bool EditorShowExitConfirmation
        {
            get => Options.ShowExitConfirmation;
            set
            {
                if (Options.ShowExitConfirmation == value)
                {
                    return;
                }

                Options.ShowExitConfirmation = value;
                OnPropertyChanged(nameof(EditorShowExitConfirmation));
            }
        }

        public bool EditorZoomToFitOnOpen
        {
            get => Options.ZoomToFitOnOpen;
            set
            {
                if (Options.ZoomToFitOnOpen == value)
                {
                    return;
                }

                Options.ZoomToFitOnOpen = value;
                OnPropertyChanged(nameof(EditorZoomToFitOnOpen));
            }
        }

        public bool EditorQuickCrop
        {
            get => Options.QuickCrop;
            set
            {
                if (Options.QuickCrop == value)
                {
                    return;
                }

                Options.QuickCrop = value;
                OnPropertyChanged(nameof(EditorQuickCrop));
            }
        }

        public bool EditorAutoCloseEditorOnTask
        {
            get => Options.AutoCloseEditorOnTask;
            set
            {
                if (Options.AutoCloseEditorOnTask == value)
                {
                    return;
                }

                Options.AutoCloseEditorOnTask = value;
                OnPropertyChanged(nameof(EditorAutoCloseEditorOnTask));
            }
        }

        public bool EditorAutoCopyImageToClipboard
        {
            get => Options.AutoCopyImageToClipboard;
            set
            {
                if (Options.AutoCopyImageToClipboard == value)
                {
                    return;
                }

                Options.AutoCopyImageToClipboard = value;
                OnPropertyChanged(nameof(EditorAutoCopyImageToClipboard));
            }
        }

        public bool EditorShowInsertImageDialog
        {
            get => Options.ShowInsertImageDialog;
            set
            {
                if (Options.ShowInsertImageDialog == value)
                {
                    return;
                }

                Options.ShowInsertImageDialog = value;
                OnPropertyChanged(nameof(EditorShowInsertImageDialog));
            }
        }

        [RelayCommand]
        private void OpenOptionsPanel()
        {
            OpenOptionsPanelRequested?.Invoke(this, EventArgs.Empty);
        }

        private static string NormalizeEditorTheme(string? theme)
        {
            return !string.IsNullOrWhiteSpace(theme) &&
                theme.Contains("Light", StringComparison.OrdinalIgnoreCase)
                ? "Light"
                : "Dark";
        }
    }
}