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

using Avalonia.Input;

namespace ShareX.ImageEditor.Presentation.ViewModels;

internal static class ToolbarHotkeyHelper
{
    public static bool TryParse(string? hotkey, out Key key, out KeyModifiers modifiers)
    {
        key = Key.None;
        modifiers = KeyModifiers.None;

        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return false;
        }

        string[] parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= KeyModifiers.Control;
                    break;
                case "SHIFT":
                    modifiers |= KeyModifiers.Shift;
                    break;
                case "ALT":
                    modifiers |= KeyModifiers.Alt;
                    break;
                case "WIN":
                case "META":
                    modifiers |= KeyModifiers.Meta;
                    break;
                default:
                    return false;
            }
        }

        return Enum.TryParse(parts[^1], ignoreCase: true, out key) && key != Key.None;
    }

    public static bool Matches(string? hotkey, Key key, KeyModifiers modifiers)
    {
        return TryParse(hotkey, out Key parsedKey, out KeyModifiers parsedModifiers) &&
            parsedKey == key &&
            parsedModifiers == modifiers;
    }

    public static string Normalize(string hotkey)
    {
        return TryParse(hotkey, out Key key, out KeyModifiers modifiers)
            ? Format(key, modifiers)
            : hotkey.Trim();
    }

    public static string Format(Key key, KeyModifiers modifiers)
    {
        List<string> parts = new();

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Meta");
        }

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
