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
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ShareX.ImageEditor.Presentation.Controls
{
    public partial class NumberPickerDropdown : UserControl
    {
        public static readonly StyledProperty<int> SelectedNumberProperty =
            AvaloniaProperty.Register<NumberPickerDropdown, int>(
                nameof(SelectedNumber),
                defaultValue: 1);

        public static readonly StyledProperty<IEnumerable<int>> NumberOptionsProperty =
            AvaloniaProperty.Register<NumberPickerDropdown, IEnumerable<int>>(
                nameof(NumberOptions),
                defaultValue: GetDefaultNumbers());

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<NumberPickerDropdown, string>(
                nameof(Label),
                defaultValue: "Value");

        public int SelectedNumber
        {
            get => GetValue(SelectedNumberProperty);
            set => SetValue(SelectedNumberProperty, value);
        }

        public IEnumerable<int> NumberOptions
        {
            get => GetValue(NumberOptionsProperty);
            set => SetValue(NumberOptionsProperty, value);
        }

        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public NumberPickerDropdown()
        {
            AvaloniaXamlLoader.Load(this);

            var popup = this.FindControl<Popup>("NumberPopup");
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
            var popup = this.FindControl<Popup>("NumberPopup");
            if (popup?.Child is Border border && border.Child is ItemsControl itemsControl)
            {
                foreach (var item in itemsControl.GetRealizedContainers())
                {
                    if (item is ContentPresenter presenter && presenter.Child is Button button)
                    {
                        if (button.CommandParameter is int number && number == SelectedNumber)
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
            var popup = this.FindControl<Popup>("NumberPopup");
            if (popup != null)
            {
                popup.IsOpen = !popup.IsOpen;
            }
        }

        private void OnNumberSelected(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int selectedNumber)
            {
                SelectedNumber = selectedNumber;

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var popup = this.FindControl<Popup>("NumberPopup");
                    if (popup != null)
                    {
                        popup.IsOpen = false;
                    }
                }, Avalonia.Threading.DispatcherPriority.Input);
            }
        }

        private static IEnumerable<int> GetDefaultNumbers()
        {
            return new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        }
    }
}