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
using Avalonia.Media;
using SkiaSharp;

namespace ShareX.ImageEditor.Annotations;

/// <summary>
/// Rectangle annotation
/// </summary>
public class RectangleAnnotation : Annotation
{
    public RectangleAnnotation()
    {
        ToolType = EditorTool.Rectangle;
    }

    /// <summary>
    /// Creates the Avalonia visual for this annotation
    /// </summary>
    public Control CreateVisual()
    {
        var strokeBrush = new SolidColorBrush(Color.Parse(StrokeColor));
        IBrush fillBrush = string.IsNullOrEmpty(FillColor) || FillColor == "#00000000"
            ? Brushes.Transparent
            : new SolidColorBrush(Color.Parse(FillColor));
        var rect = new Avalonia.Controls.Shapes.Rectangle
        {
            Stroke = strokeBrush,
            StrokeThickness = StrokeWidth,
            Fill = fillBrush,
            Tag = this
        };

        if (ShadowEnabled)
        {
            rect.Effect = new Avalonia.Media.DropShadowEffect
            {
                OffsetX = 3,
                OffsetY = 3,
                BlurRadius = 4,
                Color = Avalonia.Media.Color.FromArgb(128, 0, 0, 0)
            };
        }

        return rect;
    }

    public override void Render(SKCanvas canvas)
    {
        var rect = GetBounds();

        // Draw fill first (if not transparent)
        if (!string.IsNullOrEmpty(FillColor) && FillColor != "#00000000")
        {
            using var fillPaint = CreateFillPaint();
            canvas.DrawRect(rect, fillPaint);
        }

        // Draw stroke on top
        using var strokePaint = CreateStrokePaint();
        canvas.DrawRect(rect, strokePaint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 5)
    {
        var rect = GetBounds();
        var expanded = SKRect.Inflate(rect, tolerance, tolerance);
        return expanded.Contains(point);
    }
}

