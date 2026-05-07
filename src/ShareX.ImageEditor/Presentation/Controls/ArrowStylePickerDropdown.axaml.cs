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
using Avalonia.Threading;
using ShareX.ImageEditor.Core.Annotations;

namespace ShareX.ImageEditor.Presentation.Controls
{
    public partial class ArrowStylePickerDropdown : UserControl
    {
        public static readonly StyledProperty<ArrowStyle> SelectedArrowStyleProperty =
            AvaloniaProperty.Register<ArrowStylePickerDropdown, ArrowStyle>(
                nameof(SelectedArrowStyle),
                defaultValue: ArrowStyle.Classic);

        public static readonly StyledProperty<IEnumerable<ArrowStyle>> ArrowStyleOptionsProperty =
            AvaloniaProperty.Register<ArrowStylePickerDropdown, IEnumerable<ArrowStyle>>(
                nameof(ArrowStyleOptions),
                defaultValue: Array.Empty<ArrowStyle>());

        public ArrowStyle SelectedArrowStyle
        {
            get => GetValue(SelectedArrowStyleProperty);
            set => SetValue(SelectedArrowStyleProperty, value);
        }

        public IEnumerable<ArrowStyle> ArrowStyleOptions
        {
            get => GetValue(ArrowStyleOptionsProperty);
            set => SetValue(ArrowStyleOptionsProperty, value);
        }

        public event EventHandler<ArrowStyle>? ArrowStyleChanged;

        public ArrowStylePickerDropdown()
        {
            AvaloniaXamlLoader.Load(this);

            var popup = this.FindControl<Popup>("ArrowStylePopup");
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
            var popup = this.FindControl<Popup>("ArrowStylePopup");
            if (popup?.Child is Border border && border.Child is ItemsControl itemsControl)
            {
                foreach (var item in itemsControl.GetRealizedContainers())
                {
                    if (item is ContentPresenter presenter && presenter.Child is Button button)
                    {
                        if (button.CommandParameter is ArrowStyle arrowStyle && arrowStyle == SelectedArrowStyle)
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
            var popup = this.FindControl<Popup>("ArrowStylePopup");
            if (popup != null)
            {
                popup.IsOpen = !popup.IsOpen;
            }
        }

        private void OnArrowStyleSelected(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is ArrowStyle selectedArrowStyle)
            {
                SelectedArrowStyle = selectedArrowStyle;
                UpdateActiveStates();

                Dispatcher.UIThread.Post(() =>
                {
                    var popup = this.FindControl<Popup>("ArrowStylePopup");
                    if (popup != null)
                    {
                        popup.IsOpen = false;
                    }

                    ArrowStyleChanged?.Invoke(this, selectedArrowStyle);
                }, DispatcherPriority.Input);
            }
        }
    }
}