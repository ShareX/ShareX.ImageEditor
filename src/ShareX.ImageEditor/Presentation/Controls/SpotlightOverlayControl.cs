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

using ShareX.ImageEditor.Core.Annotations;
using SkiaSharp;

namespace ShareX.ImageEditor.Presentation.Controls;

public class SpotlightOverlayControl : SKCanvasControl
{
    public void UpdateSpotlights(IReadOnlyList<SpotlightAnnotation> spotlights, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        Initialize(width, height);

        if (spotlights == null || spotlights.Count == 0)
        {
            IsVisible = false;
            Draw(canvas => canvas.Clear(SKColors.Transparent));
            return;
        }

        IsVisible = true;

        byte darkenOpacity = spotlights.Max(spotlight => spotlight.DarkenOpacity);

        Draw(canvas =>
        {
            canvas.Clear(SKColors.Transparent);

            using var darkPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, darkenOpacity),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            using var clearPaint = new SKPaint
            {
                BlendMode = SKBlendMode.Clear,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            canvas.DrawRect(new SKRect(0, 0, width, height), darkPaint);

            foreach (SpotlightAnnotation spotlight in spotlights)
            {
                canvas.DrawRect(spotlight.GetBounds(), clearPaint);
            }
        });
    }
}
