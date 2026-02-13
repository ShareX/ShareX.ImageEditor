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
    /// Matches the rendering style of the Render() method for consistency.
    /// </summary>
    public Geometry CreateArrowGeometry(Point start, Point end, double headSize)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);

            if (length > 0)
            {
                var ux = dx / length;
                var uy = dy / length;

                // Match Render() method: 20 degrees for sleeker look
                var arrowAngle = Math.PI / 9;
                var angle = Math.Atan2(dy, dx);

                // Calculate arrowhead base point (matches Render() logic)
                var arrowBase = new Point(
                    end.X - headSize * ux,
                    end.Y - headSize * uy);

                // Arrow head wing points
                var point1 = new Point(
                    end.X - headSize * Math.Cos(angle - arrowAngle),
                    end.Y - headSize * Math.Sin(angle - arrowAngle));

                var point2 = new Point(
                    end.X - headSize * Math.Cos(angle + arrowAngle),
                    end.Y - headSize * Math.Sin(angle + arrowAngle));

                // Draw line from start to arrow base, then arrow head triangle
                ctx.BeginFigure(start, true);
                ctx.LineTo(arrowBase);
                ctx.LineTo(point1);
                ctx.LineTo(end);
                ctx.LineTo(point2);
                ctx.LineTo(arrowBase);
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
