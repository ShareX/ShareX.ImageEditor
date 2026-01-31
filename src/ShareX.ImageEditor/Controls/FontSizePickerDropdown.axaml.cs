using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ShareX.ImageEditor.Controls
{
    public partial class FontSizePickerDropdown : UserControl
    {
        public static readonly StyledProperty<float> SelectedFontSizeProperty =
            AvaloniaProperty.Register<FontSizePickerDropdown, float>(
                nameof(SelectedFontSize),
                defaultValue: 30);

        public static readonly StyledProperty<IEnumerable<float>> FontSizeOptionsProperty =
            AvaloniaProperty.Register<FontSizePickerDropdown, IEnumerable<float>>(
                nameof(FontSizeOptions),
                defaultValue: GetDefaultFontSizes());

        public float SelectedFontSize
        {
            get => GetValue(SelectedFontSizeProperty);
            set => SetValue(SelectedFontSizeProperty, value);
        }

        public IEnumerable<float> FontSizeOptions
        {
            get => GetValue(FontSizeOptionsProperty);
            set => SetValue(FontSizeOptionsProperty, value);
        }

        public event EventHandler<float>? FontSizeChanged;

        public FontSizePickerDropdown()
        {
            AvaloniaXamlLoader.Load(this);

            var popup = this.FindControl<Popup>("FontSizePopup");
            if (popup != null)
            {
                popup.Opened += OnPopupOpened;
            }
        }

        private void OnPopupOpened(object? sender, EventArgs e)
        {
            UpdateActiveStates();
        }

        private void UpdateActiveStates()
        {
            var popup = this.FindControl<Popup>("FontSizePopup");
            if (popup?.Child is Border border && border.Child is ItemsControl itemsControl)
            {
                foreach (var item in itemsControl.GetRealizedContainers())
                {
                    if (item is ContentPresenter presenter && presenter.Child is Button button)
                    {
                        if (button.CommandParameter is float size && size == SelectedFontSize)
                        {
                            button.Classes.Add("active");
                        }
                        else
                        {
                            button.Classes.Remove("active");
                        }
                    }
                }
            }
        }

        private void OnDropdownButtonClick(object? sender, RoutedEventArgs e)
        {
            var popup = this.FindControl<Popup>("FontSizePopup");
            if (popup != null)
            {
                popup.IsOpen = !popup.IsOpen;
            }
        }

        private void OnFontSizeSelected(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is float selectedSize)
            {
                SelectedFontSize = selectedSize;
                FontSizeChanged?.Invoke(this, selectedSize);

                // Update active states before closing
                UpdateActiveStates();

                // Close the popup
                var popup = this.FindControl<Popup>("FontSizePopup");
                if (popup != null)
                {
                    popup.IsOpen = false;
                }
            }
        }

        private static IEnumerable<float> GetDefaultFontSizes()
        {
            return new List<float> { 10, 13, 16, 20, 24, 30, 48, 72, 96, 144, 216, 288 };
        }
    }
}
