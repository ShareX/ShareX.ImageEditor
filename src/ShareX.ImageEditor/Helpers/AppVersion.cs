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

using System.Reflection;

namespace ShareX.ImageEditor.Helpers;

/// <summary>
/// Provides application version information for the window title (e.g. to confirm the running build).
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// Gets the version string of the entry assembly (e.g. ImageEditor.Loader when run standalone).
    /// Returns an empty string if the entry assembly or version cannot be determined.
    /// </summary>
    public static string GetVersionString()
    {
        var asm = Assembly.GetEntryAssembly();
        if (asm == null) return string.Empty;

        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!string.IsNullOrEmpty(info?.InformationalVersion))
            return info.InformationalVersion;

        var version = asm.GetName().Version;
        return version?.ToString() ?? string.Empty;
    }
}
