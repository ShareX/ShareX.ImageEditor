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

using Avalonia.Data.Converters;
using ShareX.ImageEditor.Core.Annotations;
using System.Globalization;

namespace ShareX.ImageEditor.Presentation.Converters
{
    public class CursorTypeDisplayNameConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            CursorType cursorType = value is CursorType typedCursor ? typedCursor : CursorType.Default;

            return cursorType switch
            {
                CursorType.AppStarting => "App starting",
                CursorType.Arrow => "Arrow",
                CursorType.Cross => "Cross",
                CursorType.Default => "Default",
                CursorType.Hand => "Hand",
                CursorType.Help => "Help",
                CursorType.HSplit => "H split",
                CursorType.IBeam => "I-beam",
                CursorType.No => "No",
                CursorType.NoMove2D => "No move 2D",
                CursorType.NoMoveHoriz => "No move horiz",
                CursorType.NoMoveVert => "No move vert",
                CursorType.PanEast => "Pan east",
                CursorType.PanNE => "Pan NE",
                CursorType.PanNorth => "Pan north",
                CursorType.PanNW => "Pan NW",
                CursorType.PanSE => "Pan SE",
                CursorType.PanSouth => "Pan south",
                CursorType.PanSW => "Pan SW",
                CursorType.PanWest => "Pan west",
                CursorType.SizeAll => "Size all",
                CursorType.SizeNESW => "Size NESW",
                CursorType.SizeNS => "Size NS",
                CursorType.SizeNWSE => "Size NWSE",
                CursorType.SizeWE => "Size WE",
                CursorType.UpArrow => "Up arrow",
                CursorType.VSplit => "V split",
                CursorType.WaitCursor => "Wait cursor",
                _ => cursorType.ToString()
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return CursorType.Default;
        }
    }
}