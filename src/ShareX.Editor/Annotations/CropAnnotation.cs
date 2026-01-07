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
/// Crop annotation - modifies the image dimensions
/// Note: This is a special annotation that triggers actual image modification
/// </summary>
public class CropAnnotation : Annotation
{
    public CropAnnotation()
    {
        ToolType = EditorTool.Crop;
    }

    public override void Render(SKCanvas canvas)
    {
        var rect = GetBounds();

        // Draw dashed border
        using var dashPaint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke,
            PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0),
            IsAntialias = true
        };
        canvas.DrawRect(rect, dashPaint);

        // Draw resize handles at corners and edges
        DrawHandle(canvas, new SKPoint(rect.Left, rect.Top));
        DrawHandle(canvas, new SKPoint(rect.Right, rect.Top));
        DrawHandle(canvas, new SKPoint(rect.Left, rect.Bottom));
        DrawHandle(canvas, new SKPoint(rect.Right, rect.Bottom));
        DrawHandle(canvas, new SKPoint(rect.MidX, rect.Top));
        DrawHandle(canvas, new SKPoint(rect.MidX, rect.Bottom));
        DrawHandle(canvas, new SKPoint(rect.Left, rect.MidY));
        DrawHandle(canvas, new SKPoint(rect.Right, rect.MidY));
    }

    private void DrawHandle(SKCanvas canvas, SKPoint center)
    {
        const float handleSize = 8;
        var rect = new SKRect(
            center.X - handleSize / 2,
            center.Y - handleSize / 2,
            center.X + handleSize / 2,
            center.Y + handleSize / 2);

        using var fillPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(rect, fillPaint);

        using var strokePaint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        canvas.DrawRect(rect, strokePaint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 5)
    {
        var rect = GetBounds();

        // Check if point is on the crop rectangle border (within tolerance)
        var outerRect = SKRect.Inflate(rect, tolerance, tolerance);
        var innerRect = SKRect.Inflate(rect, -tolerance, -tolerance);

        return outerRect.Contains(point) && !innerRect.Contains(point);
    }
}
