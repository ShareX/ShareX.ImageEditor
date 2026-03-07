using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ShareX.ImageEditor.Presentation.Controls
{
    public partial class CornerRadiusPickerDropdown : UserControl
    {
        public static readonly StyledProperty<int> SelectedCornerRadiusProperty =
            AvaloniaProperty.Register<CornerRadiusPickerDropdown, int>(
                nameof(SelectedCornerRadius),
                defaultValue: 3);

        public static readonly StyledProperty<IEnumerable<int>> CornerRadiusOptionsProperty =
            AvaloniaProperty.Register<CornerRadiusPickerDropdown, IEnumerable<int>>(
                nameof(CornerRadiusOptions),
                defaultValue: GetDefaultCornerRadiusOptions());

        public int SelectedCornerRadius
        {
            get => GetValue(SelectedCornerRadiusProperty);
            set => SetValue(SelectedCornerRadiusProperty, value);
        }

        public IEnumerable<int> CornerRadiusOptions
        {
            get => GetValue(CornerRadiusOptionsProperty);
            set => SetValue(CornerRadiusOptionsProperty, value);
        }

        public event EventHandler<int>? CornerRadiusChanged;

        public CornerRadiusPickerDropdown()
        {
            AvaloniaXamlLoader.Load(this);

            var popup = this.FindControl<Popup>("CornerRadiusPopup");
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
            var popup = this.FindControl<Popup>("CornerRadiusPopup");
            if (popup?.Child is Border border && border.Child is ItemsControl itemsControl)
            {
                foreach (var item in itemsControl.GetRealizedContainers())
                {
                    if (item is ContentPresenter presenter && presenter.Child is Button button)
                    {
                        if (button.CommandParameter is int radius && radius == SelectedCornerRadius)
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
            var popup = this.FindControl<Popup>("CornerRadiusPopup");
            if (popup != null)
            {
                popup.IsOpen = !popup.IsOpen;
            }
        }

        private void OnCornerRadiusSelected(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int selectedRadius)
            {
                SelectedCornerRadius = selectedRadius;

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var popup = this.FindControl<Popup>("CornerRadiusPopup");
                    if (popup != null)
                    {
                        popup.IsOpen = false;
                    }

                    CornerRadiusChanged?.Invoke(this, selectedRadius);
                }, Avalonia.Threading.DispatcherPriority.Input);
            }
        }

        private static IEnumerable<int> GetDefaultCornerRadiusOptions()
        {
            return new List<int> { 0, 3, 6, 9, 12, 18, 24, 32 };
        }
    }
}
