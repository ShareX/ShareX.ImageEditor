using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace ShareX.ImageEditor.Presentation.Theming
{
    public static class ThemeManager
    {
        public static readonly ThemeVariant ShareXDark = new ThemeVariant("ShareXDark", ThemeVariant.Dark);
        public static readonly ThemeVariant ShareXLight = new ThemeVariant("ShareXLight", ThemeVariant.Light);
        private static ThemeVariant _currentTheme = ShareXDark;

        public static event EventHandler<ThemeVariant>? ThemeChanged;

        public static void SetTheme(ThemeVariant theme, object? target = null)
        {
            _currentTheme = theme;

            if (target is Application app)
            {
                app.RequestedThemeVariant = theme;
            }
            else if (target is Window window)
            {
                window.RequestedThemeVariant = theme;
            }
            else if (target is ThemeVariantScope scope)
            {
                scope.RequestedThemeVariant = theme;
            }

            ThemeChanged?.Invoke(null, theme);
        }

        public static ThemeVariant GetCurrentTheme()
        {
            return _currentTheme;
        }
    }
}
