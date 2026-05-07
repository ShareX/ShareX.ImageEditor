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

namespace ShareX.ImageEditor.Presentation.Controls
{
    public partial class FontFamilyPickerDropdown : UserControl
    {
        public static readonly StyledProperty<string> SelectedFontFamilyProperty =
            AvaloniaProperty.Register<FontFamilyPickerDropdown, string>(
                nameof(SelectedFontFamily),
                defaultValue: "Segoe UI");

        public static readonly StyledProperty<IEnumerable<string>> FontFamilyOptionsProperty =
            AvaloniaProperty.Register<FontFamilyPickerDropdown, IEnumerable<string>>(
                nameof(FontFamilyOptions),
                defaultValue: Array.Empty<string>());

        public string SelectedFontFamily
        {
            get => GetValue(SelectedFontFamilyProperty);
            set => SetValue(SelectedFontFamilyProperty, value);
        }

        public IEnumerable<string> FontFamilyOptions
        {
            get => GetValue(FontFamilyOptionsProperty);
            set => SetValue(FontFamilyOptionsProperty, value);
        }

        public event EventHandler<string>? FontFamilyChanged;

        public FontFamilyPickerDropdown()
        {
            AvaloniaXamlLoader.Load(this);

            var popup = this.FindControl<Popup>("FontFamilyPopup");
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
            var popup = this.FindControl<Popup>("FontFamilyPopup");
            if (popup?.Child is Border border && border.Child is ScrollViewer viewer && viewer.Content is ItemsControl itemsControl)
            {
                foreach (var item in itemsControl.GetRealizedContainers())
                {
                    if (item is ContentPresenter presenter && presenter.Child is Button button)
                    {
                        if (button.CommandParameter is string fontFamily && string.Equals(fontFamily, SelectedFontFamily, StringComparison.Ordinal))
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
            var popup = this.FindControl<Popup>("FontFamilyPopup");
            if (popup != null)
            {
                popup.IsOpen = !popup.IsOpen;
            }
        }

        private void OnFontFamilySelected(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string selectedFontFamily)
            {
                SelectedFontFamily = selectedFontFamily;
                UpdateActiveStates();

                Dispatcher.UIThread.Post(() =>
                {
                    var popup = this.FindControl<Popup>("FontFamilyPopup");
                    if (popup != null)
                    {
                        popup.IsOpen = false;
                    }

                    FontFamilyChanged?.Invoke(this, selectedFontFamily);
                }, DispatcherPriority.Input);
            }
        }
    }
}