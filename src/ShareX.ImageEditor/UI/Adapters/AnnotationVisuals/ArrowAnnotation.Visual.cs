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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ShareX.ImageEditor.Annotations;

public partial class ArrowAnnotation
{
    /// <summary>
    /// Creates the Avalonia visual for this annotation
    /// </summary>
    public Control CreateVisual()
    {
        var brush = new SolidColorBrush(Color.Parse(StrokeColor));
        var path = new Avalonia.Controls.Shapes.Path
        {
            Stroke = brush,
            StrokeThickness = StrokeWidth,
            Fill = brush,
            Tag = this
        };

        path.Data = CreateArrowGeometry(
            new Point(StartPoint.X, StartPoint.Y),
            new Point(EndPoint.X, EndPoint.Y),
            StrokeWidth * ArrowHeadWidthMultiplier);

        if (ShadowEnabled)
        {
            path.Effect = new Avalonia.Media.DropShadowEffect
            {
                OffsetX = 3,
                OffsetY = 3,
                BlurRadius = 4,
                Color = Avalonia.Media.Color.FromArgb(128, 0, 0, 0)
            };
        }

        return path;
    }

    /// <summary>
    /// Creates arrow geometry for the Avalonia Path.
    /// </summary>
    public Geometry CreateArrowGeometry(Point start, Point end, double headSize)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var d = end - start;
            var length = Math.Sqrt(d.X * d.X + d.Y * d.Y);

            if (length > 0)
            {
                var ux = d.X / length;
                var uy = d.Y / length;

                var perpX = -uy;
                var perpY = ux;

                var enlargedHeadSize = headSize * 1.5;
                var arrowAngle = Math.PI / 5.14;

                var arrowBase = new Point(
                    end.X - enlargedHeadSize * ux,
                    end.Y - enlargedHeadSize * uy);

                var arrowheadBaseWidth = enlargedHeadSize * Math.Tan(arrowAngle);

                var arrowBaseLeft = new Point(
                    arrowBase.X + perpX * arrowheadBaseWidth,
                    arrowBase.Y + perpY * arrowheadBaseWidth);

                var arrowBaseRight = new Point(
                    arrowBase.X - perpX * arrowheadBaseWidth,
                    arrowBase.Y - perpY * arrowheadBaseWidth);

                var shaftEndWidth = enlargedHeadSize * 0.30;

                var shaftEndLeft = new Point(
                    arrowBase.X + perpX * shaftEndWidth,
                    arrowBase.Y + perpY * shaftEndWidth);

                var shaftEndRight = new Point(
                    arrowBase.X - perpX * shaftEndWidth,
                    arrowBase.Y - perpY * shaftEndWidth);

                ctx.BeginFigure(start, true);
                ctx.LineTo(shaftEndLeft);
                ctx.LineTo(arrowBaseLeft);
                ctx.LineTo(end);
                ctx.LineTo(arrowBaseRight);
                ctx.LineTo(shaftEndRight);
                ctx.EndFigure(true);
            }
            else
            {
                var radius = 2.0;
                ctx.BeginFigure(new Point(start.X - radius, start.Y), true);
                ctx.ArcTo(new Point(start.X + radius, start.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(new Point(start.X - radius, start.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                ctx.EndFigure(true);
            }
        }

        return geometry;
    }
}
