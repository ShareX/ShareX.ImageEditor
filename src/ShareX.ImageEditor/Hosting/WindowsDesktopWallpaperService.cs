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

using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ShareX.ImageEditor.Hosting;

/// <summary>
/// Default Windows wallpaper resolver used by Avalonia hosts that do not provide a custom implementation.
/// </summary>
internal sealed class WindowsDesktopWallpaperService : IDesktopWallpaperService
{
    private const int SpiGetDesktopWallpaper = 0x0073;
    private const int MaxWallpaperPath = 260;

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool TryGetDesktopWallpaper(out DesktopWallpaperInfo? wallpaper)
    {
        wallpaper = null;

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        StringBuilder buffer = new StringBuilder(MaxWallpaperPath);
        if (!SystemParametersInfo(SpiGetDesktopWallpaper, buffer.Capacity, buffer, 0))
        {
            return false;
        }

        string wallpaperPath = buffer.ToString().TrimEnd('\0');
        if (string.IsNullOrWhiteSpace(wallpaperPath) || !File.Exists(wallpaperPath))
        {
            return false;
        }

        wallpaper = new DesktopWallpaperInfo
        {
            Path = wallpaperPath,
            Layout = DesktopWallpaperLayout.Fill
        };

        return true;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, StringBuilder pvParam, int fWinIni);
}
