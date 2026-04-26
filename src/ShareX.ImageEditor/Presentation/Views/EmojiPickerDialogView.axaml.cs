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

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ShareX.ImageEditor.Presentation.ViewModels;

namespace ShareX.ImageEditor.Presentation.Views;

public partial class EmojiPickerDialogView : UserControl
{
    public EmojiPickerDialogView()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        // Handle Esc even when the search TextBox has focus (handledEventsToo: true
        // ensures this fires even after a child TextBox marks the KeyUp event as handled).
        AddHandler(KeyUpEvent, OnEscapeKeyUp, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnEscapeKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (DataContext is EmojiPickerDialogViewModel viewModel)
            {
                viewModel.CancelCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EmojiPickerDialogViewModel viewModel)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await viewModel.InitializeAsync();

                // Focus the search box after loading completes so it is enabled and ready.
                if (this.FindControl<TextBox>("SearchBox") is TextBox searchBox)
                {
                    searchBox.Focus();
                }
            }, DispatcherPriority.Background);
        }
    }
}
