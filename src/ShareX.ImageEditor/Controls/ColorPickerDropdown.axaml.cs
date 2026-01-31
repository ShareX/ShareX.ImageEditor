using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ShareX.ImageEditor.Controls
{
    public partial class ColorPickerDropdown : UserControl
    {
        public static readonly StyledProperty<IBrush> SelectedColorProperty =
            AvaloniaProperty.Register<ColorPickerDropdown, IBrush>(
                nameof(SelectedColor),
                defaultValue: Brushes.Red);

        public static readonly StyledProperty<Color> SelectedColorValueProperty =
            AvaloniaProperty.Register<ColorPickerDropdown, Color>(
                nameof(SelectedColorValue),
                defaultValue: Colors.Red);

        public static readonly StyledProperty<HsvColor> SelectedHsvColorProperty =
            AvaloniaProperty.Register<ColorPickerDropdown, HsvColor>(
                nameof(SelectedHsvColor));

        public IBrush SelectedColor
        {
            get => GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public Color SelectedColorValue
        {
            get => GetValue(SelectedColorValueProperty);
            set => SetValue(SelectedColorValueProperty, value);
        }

        public HsvColor SelectedHsvColor
        {
            get => GetValue(SelectedHsvColorProperty);
            set => SetValue(SelectedHsvColorProperty, value);
        }

        public event EventHandler<IBrush>? ColorChanged;

        static ColorPickerDropdown()
        {
            // Sync SelectedColor -> SelectedColorValue
            SelectedColorProperty.Changed.AddClassHandler<ColorPickerDropdown>((s, e) =>
            {
                if (e.NewValue is SolidColorBrush brush)
                {
                    s.SelectedColorValue = brush.Color;
                }
            });

            // Sync SelectedColorValue -> SelectedColor
            SelectedColorValueProperty.Changed.AddClassHandler<ColorPickerDropdown>((s, e) =>
            {
                if (e.NewValue is Color color)
                {
                    var newBrush = new SolidColorBrush(color);
                    s.SelectedColor = newBrush;
                    s.ColorChanged?.Invoke(s, newBrush);
                }
            });
        }

        public ColorPickerDropdown()
        {
            AvaloniaXamlLoader.Load(this);

            Loaded += (s, e) =>
            {
                var colorView = this.FindControl<Avalonia.Controls.ColorView>("ColorViewControl");
                if (colorView?.PaletteColors != null)
                {
                    var paletteColors = colorView.PaletteColors.ToList();
                    int count = paletteColors.Count;
                    paletteColors[count - 7] = Color.FromArgb(255, 235, 235, 235);
                    paletteColors[count - 1] = Color.FromArgb(255, 20, 20, 20);
                    colorView.PaletteColors = paletteColors;
                }
            };
        }

        private void OnDropdownButtonClick(object? sender, RoutedEventArgs e)
        {
            var popup = this.FindControl<Popup>("ColorPopup");
            if (popup != null)
            {
                popup.IsOpen = !popup.IsOpen;
            }
        }
    }
}
