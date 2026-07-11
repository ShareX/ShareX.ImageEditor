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
using SkiaSharp;

namespace ShareX.ImageEditor.Presentation.EasterEggs;

internal sealed class EditorEasterEggController : IDisposable
{
    private readonly KonamiCodeDetector _detector = new();
    private readonly Func<SKBitmap?> _snapshotProvider;
    private readonly ShaderEasterEggPlayer _player;
    private readonly Action? _activated;

    public EditorEasterEggController(
        ShaderEasterEggOverlay overlay,
        Func<SKBitmap?> snapshotProvider,
        IShaderEasterEggEffect effect,
        Action? activated = null)
    {
        _snapshotProvider = snapshotProvider;
        _player = new ShaderEasterEggPlayer(overlay, effect);
        _activated = activated;
    }

    /// <summary>
    /// Returns true only when the editor should suppress normal handling: while playback is
    /// active, when Escape cancels playback, or for the final key that activates the effect.
    /// </summary>
    public bool HandleKeyDown(Key key, KeyModifiers modifiers)
    {
        if (_player.IsPlaying)
        {
            if (key == Key.Escape && modifiers == KeyModifiers.None)
            {
                _player.Stop();
            }

            return true;
        }

        if (modifiers != KeyModifiers.None)
        {
            _detector.Reset();
            return false;
        }

        if (!_detector.Push(key))
        {
            return false;
        }

        SKBitmap? snapshot = _snapshotProvider();
        if (snapshot == null || !_player.Play(snapshot))
        {
            return false;
        }

        _activated?.Invoke();
        return true;
    }

    public void Stop()
    {
        _detector.Reset();
        _player.Stop();
    }

    public void Dispose()
    {
        _player.Dispose();
    }
}
