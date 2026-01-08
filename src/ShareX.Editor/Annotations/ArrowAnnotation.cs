#region License Information (GPL v3)

/*
    ShareX.Editor - The UI-agnostic Editor library for ShareX
    Copyright (c) 2007-2025 ShareX Team

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
using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Arrow annotation (line with arrowhead)
/// </summary>
public class ArrowAnnotation : Annotation
{
    /// <summary>
    /// Arrow head size in pixels
    /// </summary>
    public float ArrowHeadSize { get; set; } = 12;

    public ArrowAnnotation()
    {
        ToolType = EditorTool.Arrow;
    }

    /// <summary>
    /// Creates the Avalonia visual for this annotation
    /// </summary>
    public Control CreateVisual()
    {
        var brush = new SolidColorBrush(Color.Parse(StrokeColor));
        return new Avalonia.Controls.Shapes.Path
        {
            Stroke = brush,
            StrokeThickness = StrokeWidth,
            Fill = brush, // Fill arrowhead
            Data = new PathGeometry(),
            Tag = this
        };
    }

    /// <summary>
    /// Creates arrow geometry for the Avalonia Path (relocated from EditorView)
    /// </summary>
    public Geometry CreateArrowGeometry(Avalonia.Point start, Avalonia.Point end, double headSize)
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
                var arrowAngle = Math.PI / 5.14; // 35 degrees

                var arrowBase = new Avalonia.Point(
                    end.X - enlargedHeadSize * ux,
                    end.Y - enlargedHeadSize * uy);

                var arrowheadBaseWidth = enlargedHeadSize * Math.Tan(arrowAngle);

                var arrowBaseLeft = new Avalonia.Point(
                    arrowBase.X + perpX * arrowheadBaseWidth,
                    arrowBase.Y + perpY * arrowheadBaseWidth);

                var arrowBaseRight = new Avalonia.Point(
                    arrowBase.X - perpX * arrowheadBaseWidth,
                    arrowBase.Y - perpY * arrowheadBaseWidth);

                var shaftEndWidth = enlargedHeadSize * 0.30;

                var shaftEndLeft = new Avalonia.Point(
                    arrowBase.X + perpX * shaftEndWidth,
                    arrowBase.Y + perpY * shaftEndWidth);

                var shaftEndRight = new Avalonia.Point(
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
                ctx.BeginFigure(new Avalonia.Point(start.X - radius, start.Y), true);
                ctx.ArcTo(new Avalonia.Point(start.X + radius, start.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(new Avalonia.Point(start.X - radius, start.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                ctx.EndFigure(true);
            }
        }
        return geometry;
    }

    public override void Render(SKCanvas canvas)
    {
        using var strokePaint = CreateStrokePaint();
        using var fillPaint = CreateFillPaint();

        // Calculate arrow head
        var dx = EndPoint.X - StartPoint.X;
        var dy = EndPoint.Y - StartPoint.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);

        if (length > 0)
        {
            var ux = dx / length;
            var uy = dy / length;

            // Modern arrow: narrower angle (20 degrees instead of 30)
            var arrowAngle = Math.PI / 9; // 20 degrees for sleeker look
            var angle = Math.Atan2(dy, dx);

            // Calculate arrowhead base point
            var arrowBase = new SKPoint(
                EndPoint.X - ArrowHeadSize * ux,
                EndPoint.Y - ArrowHeadSize * uy);

            // Draw line from start to arrow base
            canvas.DrawLine(StartPoint, arrowBase, strokePaint);

            // Arrow head wing points
            var point1 = new SKPoint(
                (float)(EndPoint.X - ArrowHeadSize * Math.Cos(angle - arrowAngle)),
                (float)(EndPoint.Y - ArrowHeadSize * Math.Sin(angle - arrowAngle)));

            var point2 = new SKPoint(
                (float)(EndPoint.X - ArrowHeadSize * Math.Cos(angle + arrowAngle)),
                (float)(EndPoint.Y - ArrowHeadSize * Math.Sin(angle + arrowAngle)));

            // Draw filled arrow head triangle
            using var path = new SKPath();
            path.MoveTo(EndPoint);
            path.LineTo(point1);
            path.LineTo(point2);
            path.Close();

            canvas.DrawPath(path, fillPaint);
            canvas.DrawPath(path, strokePaint);
        }
        else
        {
            // Fallback for zero-length arrow
            canvas.DrawLine(StartPoint, EndPoint, strokePaint);
        }
    }

    public override bool HitTest(SKPoint point, float tolerance = 5)
    {
        // Reuse line hit test logic
        var dx = EndPoint.X - StartPoint.X;
        var dy = EndPoint.Y - StartPoint.Y;
        var lineLength = (float)Math.Sqrt(dx * dx + dy * dy);
        if (lineLength < 0.001f) return false;

        var t = Math.Max(0, Math.Min(1,
            ((point.X - StartPoint.X) * (EndPoint.X - StartPoint.X) +
             (point.Y - StartPoint.Y) * (EndPoint.Y - StartPoint.Y)) / (lineLength * lineLength)));

        var projection = new SKPoint(
            StartPoint.X + (float)t * (EndPoint.X - StartPoint.X),
            StartPoint.Y + (float)t * (EndPoint.Y - StartPoint.Y));

        var pdx = point.X - projection.X;
        var pdy = point.Y - projection.Y;
        var distance = (float)Math.Sqrt(pdx * pdx + pdy * pdy);
        return distance <= tolerance;
    }
}
