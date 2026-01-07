using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ShareX.Editor.Controls
{
    public partial class ColorPickerDropdown : UserControl
    {
        public static readonly StyledProperty<IBrush> SelectedColorProperty =
            AvaloniaProperty.Register<ColorPickerDropdown, IBrush>(
                nameof(SelectedColor),
                defaultValue: Brushes.Red);

        public static readonly StyledProperty<IEnumerable<IBrush>> ColorPaletteProperty =
            AvaloniaProperty.Register<ColorPickerDropdown, IEnumerable<IBrush>>(
                nameof(ColorPalette),
                defaultValue: GetDefaultColorPalette());

        public IBrush SelectedColor
        {
            get => GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public IEnumerable<IBrush> ColorPalette
        {
            get => GetValue(ColorPaletteProperty);
            set => SetValue(ColorPaletteProperty, value);
        }

        public event EventHandler<IBrush>? ColorChanged;

        public ColorPickerDropdown()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnDropdownButtonClick(object? sender, RoutedEventArgs e)
        {
            var popup = this.FindControl<Popup>("ColorPopup");
            if (popup != null)
            {
                popup.IsOpen = !popup.IsOpen;
            }
        }

        private void OnColorSelected(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is IBrush selectedColor)
            {
                SelectedColor = selectedColor;
                ColorChanged?.Invoke(this, selectedColor);

                // Close the popup
                var popup = this.FindControl<Popup>("ColorPopup");
                if (popup != null)
                {
                    popup.IsOpen = false;
                }
            }
        }

        private static IEnumerable<IBrush> GetDefaultColorPalette()
        {
            return new List<IBrush>
            {
                new SolidColorBrush(Color.Parse("#EF4444")), // Red
                new SolidColorBrush(Color.Parse("#F97316")), // Orange
                new SolidColorBrush(Color.Parse("#EAB308")), // Yellow
                new SolidColorBrush(Color.Parse("#22C55E")), // Green
                new SolidColorBrush(Color.Parse("#0EA5E9")), // Blue
                new SolidColorBrush(Color.Parse("#6366F1")), // Indigo
                new SolidColorBrush(Color.Parse("#A855F7")), // Purple
                new SolidColorBrush(Color.Parse("#EC4899")), // Pink
                new SolidColorBrush(Color.Parse("#FFFFFF")), // White
                new SolidColorBrush(Color.Parse("#000000")), // Black
                new SolidColorBrush(Color.Parse("#64748B")), // Gray
                new SolidColorBrush(Color.Parse("#1E293B"))  // Dark
            };
        }
    }
}
