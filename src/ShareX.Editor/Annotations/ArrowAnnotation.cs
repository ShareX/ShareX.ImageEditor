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
