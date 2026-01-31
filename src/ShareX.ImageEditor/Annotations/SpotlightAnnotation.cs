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

using Avalonia.Controls;
using ShareX.ImageEditor.Controls;
using SkiaSharp;

namespace ShareX.ImageEditor.Annotations;

/// <summary>
/// Spotlight annotation - darkens entire image except highlighted area
/// </summary>
public class SpotlightAnnotation : Annotation
{
    /// <summary>
    /// Darkening overlay opacity (0-255)
    /// </summary>
    public byte DarkenOpacity { get; set; } = 180;

    /// <summary>
    /// Size of the canvas (needed for full overlay)
    /// </summary>
    public SKSize CanvasSize { get; set; }

    public SpotlightAnnotation()
    {
        ToolType = EditorTool.Spotlight;
    }

    /// <summary>
    /// Creates the Avalonia visual for this annotation (SpotlightControl)
    /// </summary>
    public Control CreateVisual()
    {
        return new SpotlightControl
        {
            Annotation = this,
            IsHitTestVisible = false,
            Tag = this
        };
    }

    public override void Render(SKCanvas canvas)
    {
        if (CanvasSize.Width <= 0 || CanvasSize.Height <= 0) return;

        var spotlightRect = GetBounds();

        // Create dark overlay using path with EvenOdd fill rule
        using var path = new SKPath { FillType = SKPathFillType.EvenOdd };

        // Outer rectangle: full canvas
        path.AddRect(new SKRect(0, 0, CanvasSize.Width, CanvasSize.Height));

        // Inner rectangle: spotlight (hole)
        path.AddRect(spotlightRect);

        // Draw the overlay (darkens everything except the rectangle)
        using var paint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, DarkenOpacity),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawPath(path, paint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 5)
    {
        var bounds = GetBounds();
        var inflated = SKRect.Inflate(bounds, tolerance, tolerance);
        return inflated.Contains(point);
    }
}
