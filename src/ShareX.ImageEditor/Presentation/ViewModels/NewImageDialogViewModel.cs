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

using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShareX.ImageEditor.Presentation.ViewModels
{
    public partial class NewImageDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _width = 800;

        [ObservableProperty]
        private int _height = 600;

        [ObservableProperty]
        private bool _transparent = true;

        [ObservableProperty]
        private Color _backgroundColor = Colors.White;

        public bool IsSolidBackground
        {
            get => !Transparent;
            set => Transparent = !value;
        }

        public IRelayCommand OkCommand { get; }
        public IRelayCommand CancelCommand { get; }

        partial void OnTransparentChanged(bool value)
        {
            OnPropertyChanged(nameof(IsSolidBackground));
        }

        public NewImageDialogViewModel(Action<NewImageDialogViewModel> onOk, Action onCancel)
        {
            OkCommand = new RelayCommand(() => onOk(this));
            CancelCommand = new RelayCommand(onCancel);
        }
    }
}