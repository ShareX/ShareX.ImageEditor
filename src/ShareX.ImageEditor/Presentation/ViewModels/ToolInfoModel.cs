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

namespace ShareX.ImageEditor.Presentation.ViewModels;

/// <summary>
/// Structured view model for the Tool Info panel. Exposes the current tool or
/// selected annotation's identity and relevant property summary values.
/// </summary>
public sealed partial class ToolInfoModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Select";

    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private bool _showPrimaryColor;

    [ObservableProperty]
    private Color _primaryColor = Colors.Transparent;

    [ObservableProperty]
    private bool _showSecondaryColor;

    [ObservableProperty]
    private Color _secondaryColor = Colors.Transparent;

    [ObservableProperty]
    private bool _showTextColor;

    [ObservableProperty]
    private Color _textColor = Colors.Transparent;

    [ObservableProperty]
    private bool _showThickness;

    [ObservableProperty]
    private int _thickness;

    [ObservableProperty]
    private bool _showFontSize;

    [ObservableProperty]
    private float _fontSize;

    [ObservableProperty]
    private bool _showStrength;

    [ObservableProperty]
    private float _strength;

    [ObservableProperty]
    private bool _showDimensions;

    [ObservableProperty]
    private double _infoWidth;

    [ObservableProperty]
    private double _infoHeight;

    [ObservableProperty]
    private bool _showTextStyle;

    [ObservableProperty]
    private bool _isBold;

    [ObservableProperty]
    private bool _isItalic;

    [ObservableProperty]
    private bool _isUnderline;

    [ObservableProperty]
    private bool _showShadow;

    [ObservableProperty]
    private bool _shadowEnabled;
}
