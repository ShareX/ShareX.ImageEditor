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

using Avalonia.Threading;
using SkiaSharp;
using System.Diagnostics;

namespace ShareX.ImageEditor.Presentation.EasterEggs;

internal sealed class ShaderEasterEggPlayer : IDisposable
{
    private readonly ShaderEasterEggOverlay _overlay;
    private readonly IShaderEasterEggEffect _effectDefinition;
    private readonly DispatcherTimer _timer;
    private bool _isPlaying;

    public ShaderEasterEggPlayer(ShaderEasterEggOverlay overlay, IShaderEasterEggEffect effectDefinition)
    {
        _overlay = overlay;
        _effectDefinition = effectDefinition;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = effectDefinition.Duration
        };
        _timer.Tick += OnTimerTick;
    }

    public bool IsPlaying => _isPlaying;

    public bool Play(SKBitmap snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Stop();

        try
        {
            _overlay.IsVisible = true;
            _overlay.IsHitTestVisible = true;
            if (!_overlay.Start(_effectDefinition, snapshot))
            {
                Stop();
                return false;
            }

            _isPlaying = true;
            _timer.Start();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to start image editor easter egg: {ex}");
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        _timer.Stop();
        _isPlaying = false;
        _overlay.Stop();
        _overlay.IsVisible = false;
        _overlay.IsHitTestVisible = false;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Stop();
    }

    public void Dispose()
    {
        Stop();
        _timer.Tick -= OnTimerTick;
        _overlay.Dispose();
    }
}
