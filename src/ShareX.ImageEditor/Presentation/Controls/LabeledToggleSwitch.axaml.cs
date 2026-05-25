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
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ShareX.ImageEditor.Presentation.Controls
{
    public partial class LabeledToggleSwitch : UserControl
    {
        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<LabeledToggleSwitch, string>(
                nameof(Label),
                defaultValue: string.Empty);

        public static readonly StyledProperty<bool> IsCheckedProperty =
            AvaloniaProperty.Register<LabeledToggleSwitch, bool>(
                nameof(IsChecked),
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<IBrush?> ToggleForegroundProperty =
            AvaloniaProperty.Register<LabeledToggleSwitch, IBrush?>(
                nameof(ToggleForeground));

        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public bool IsChecked
        {
            get => GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public IBrush? ToggleForeground
        {
            get => GetValue(ToggleForegroundProperty);
            set => SetValue(ToggleForegroundProperty, value);
        }

        public LabeledToggleSwitch()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}