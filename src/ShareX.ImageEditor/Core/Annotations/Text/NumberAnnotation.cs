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

using SkiaSharp;
using System.Text.Json.Serialization;

namespace ShareX.ImageEditor.Core.Annotations;

/// <summary>
/// Number annotation - auto-incrementing numbered circle markers
/// </summary>
public partial class NumberAnnotation : Annotation
{
    private const float GeometryEpsilon = 0.001f;
    private const float TextPaddingMultiplier = 0.35f;

    public override AnnotationCategory Category => AnnotationCategory.Text;
    /// <summary>
    /// Number to display (typically auto-incremented)
    /// </summary>
    public int Number { get; set; } = 1;

    /// <summary>
    /// Display style for the step label.
    /// </summary>
    public StepType StepType { get; set; } = StepType.Numeric;

    /// <summary>
    /// Font size for the number
    /// </summary>
    public float FontSize { get; set; } = 24;

    /// <summary>
    /// Text body color
    /// </summary>
    public string TextColor { get; set; } = "#FFFFFFFF";

    /// <summary>
    /// Bold style for the step label.
    /// </summary>
    public bool IsBold { get; set; } = true;

    /// <summary>
    /// Circle radius - auto-calculated based on FontSize if not explicitly set
    /// </summary>
    public float Radius
    {
        get => CalculateRadius();
        set { } // Allow setting but use calculated value
    }

    /// <summary>
    /// Tail point (absolute position). The tail is only rendered after this point is explicitly set.
    /// </summary>
    public SKPoint TailPoint { get; set; }

    /// <summary>
    /// Tracks whether the tail point was explicitly initialized.
    /// </summary>
    public bool TailPointInitialized { get; set; }

    /// <summary>
    /// Controls whether the tail geometry and handle are shown.
    /// </summary>
    public bool TailEnabled { get; set; } = true;

    /// <summary>
    /// Calculate radius based on font size to ensure text fits
    /// </summary>
    private float CalculateRadius()
    {
        float baseRadius = Math.Max(12, FontSize * 0.7f);
        string displayText = GetDisplayText();

        using var font = new SKFont(SKTypeface.Default, Math.Max(1, FontSize * 0.6f))
        {
            Embolden = IsBold
        };

        float textWidth = font.MeasureText(displayText);
        SKFontMetrics metrics = font.Metrics;
        float textHeight = metrics.Descent - metrics.Ascent;
        float padding = Math.Max(6, FontSize * TextPaddingMultiplier);
        float measuredRadius = Math.Max(textWidth, textHeight) * 0.5f + padding;

        return Math.Max(baseRadius, measuredRadius);
    }

    public NumberAnnotation()
    {
        ToolType = EditorTool.Step;
    }

    [JsonIgnore]
    public bool HasTailPoint => TailPointInitialized;

    [JsonIgnore]
    public string DisplayText => GetDisplayText();

    public string GetDisplayText() => StepTypeFormatter.Format(Number, StepType);

    public SKPoint GetDefaultTailHandlePoint()
    {
        var bounds = GetBounds();
        // For arrow tails, the tail point needs to be outside the circle so the
        // arrowhead geometry (which places the shaft base headHeight before the tip)
        // doesn't end up inside the circle body. Use 2x radius offset so the default
        // tail is well outside the circle with visible shaft length.
        return new SKPoint(bounds.Right + Radius, bounds.Bottom + Radius);
    }

    public SKPoint GetTailHandlePoint() => HasTailPoint ? TailPoint : GetDefaultTailHandlePoint();

    public void SetTailPoint(SKPoint tailPoint)
    {
        TailPoint = tailPoint;
        TailPointInitialized = true;
    }

    public bool IsTailVisible()
    {
        if (!TailEnabled)
        {
            return false;
        }

        return TailStyle switch
        {
            StepTailStyle.Arrow => TryGetArrowTailOutline(out _, out _, out _, out _, out _, out _, out _),
            _ => TryGetTailPolygon(out _, out _, out _)
        };
    }

    public SKRect GetInteractionBounds(float tolerance = 0)
    {
        var interactionBounds = GetBounds();

        if (TailStyle == StepTailStyle.Arrow)
        {
            if (TryGetArrowTailOutline(
                out var shaftStartLeft,
                out var shaftEndLeft,
                out var wingLeft,
                out var tailTip,
                out var wingRight,
                out var shaftEndRight,
                out var shaftStartRight))
            {
                interactionBounds = UnionPoints(
                    interactionBounds,
                    shaftStartLeft,
                    shaftEndLeft,
                    wingLeft,
                    tailTip,
                    wingRight,
                    shaftEndRight,
                    shaftStartRight);
            }
        }
        else if (TryGetTailPolygon(out var tailBaseStart, out var tailTip, out var tailBaseEnd))
        {
            interactionBounds = UnionPoints(interactionBounds, tailBaseStart, tailTip, tailBaseEnd);
        }

        if (tolerance > 0)
        {
            interactionBounds = SKRect.Inflate(interactionBounds, tolerance, tolerance);
        }

        return interactionBounds;
    }

    public bool TryGetTailPolygon(out SKPoint tailBaseStart, out SKPoint tailTip, out SKPoint tailBaseEnd)
    {
        tailBaseStart = default;
        tailTip = default;
        tailBaseEnd = default;

        if (!TailEnabled || !HasTailPoint)
        {
            return false;
        }

        var center = StartPoint;
        float radius = Radius;
        if (radius <= GeometryEpsilon)
        {
            return false;
        }

        tailTip = TailPoint;

        float directionX = tailTip.X - center.X;
        float directionY = tailTip.Y - center.Y;
        float directionLength = MathF.Sqrt(directionX * directionX + directionY * directionY);
        if (directionLength <= radius + GeometryEpsilon)
        {
            return false;
        }

        float normalizedDirectionX = directionX / directionLength;
        float normalizedDirectionY = directionY / directionLength;

        var perpendicular = new SKPoint(-normalizedDirectionY, normalizedDirectionX);
        float projectionDistance = (radius * radius) / directionLength;
        float offsetDistance = radius * MathF.Sqrt((directionLength * directionLength) - (radius * radius)) / directionLength;
        var tangentCenter = new SKPoint(
            center.X + normalizedDirectionX * projectionDistance,
            center.Y + normalizedDirectionY * projectionDistance);

        tailBaseStart = new SKPoint(
            tangentCenter.X + perpendicular.X * offsetDistance,
            tangentCenter.Y + perpendicular.Y * offsetDistance);
        tailBaseEnd = new SKPoint(
            tangentCenter.X - perpendicular.X * offsetDistance,
            tangentCenter.Y - perpendicular.Y * offsetDistance);

        return true;
    }


    public override bool HitTest(SKPoint point, float tolerance = 5)
    {
        var dx = point.X - StartPoint.X;
        var dy = point.Y - StartPoint.Y;
        var distance = (float)Math.Sqrt(dx * dx + dy * dy);
        if (distance <= (Radius + tolerance))
        {
            return true;
        }

        if (TailStyle == StepTailStyle.Arrow)
        {
            if (!TryGetArrowTailOutline(
                out var shaftStartLeft,
                out var shaftEndLeft,
                out var wingLeft,
                out var tailTip,
                out var wingRight,
                out var shaftEndRight,
                out var shaftStartRight))
            {
                return false;
            }

            SKPoint[] polygon =
            [
                shaftStartLeft,
                shaftEndLeft,
                wingLeft,
                tailTip,
                wingRight,
                shaftEndRight,
                shaftStartRight
            ];

            if (PointInPolygon(point, polygon))
            {
                return true;
            }

            for (int i = 0; i < polygon.Length; i++)
            {
                var segmentStart = polygon[i];
                var segmentEnd = polygon[(i + 1) % polygon.Length];
                if (DistanceToSegment(point, segmentStart, segmentEnd) <= tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        if (!TryGetTailPolygon(out var tailBaseStart, out var triangleTailTip, out var tailBaseEnd))
        {
            return false;
        }

        if (PointInTriangle(point, tailBaseStart, triangleTailTip, tailBaseEnd))
        {
            return true;
        }

        return DistanceToSegment(point, tailBaseStart, triangleTailTip) <= tolerance ||
               DistanceToSegment(point, triangleTailTip, tailBaseEnd) <= tolerance ||
               DistanceToSegment(point, tailBaseEnd, tailBaseStart) <= tolerance;
    }

    public override SKRect GetBounds()
    {
        return new SKRect(
            StartPoint.X - Radius,
            StartPoint.Y - Radius,
            StartPoint.X + Radius,
            StartPoint.Y + Radius);
    }

    public bool TryGetArrowTailOutline(
        out SKPoint shaftStartLeft,
        out SKPoint shaftEndLeft,
        out SKPoint wingLeft,
        out SKPoint tailTip,
        out SKPoint wingRight,
        out SKPoint shaftEndRight,
        out SKPoint shaftStartRight)
    {
        shaftStartLeft = default;
        shaftEndLeft = default;
        wingLeft = default;
        tailTip = default;
        wingRight = default;
        shaftEndRight = default;
        shaftStartRight = default;

        if (!TailEnabled || !HasTailPoint)
        {
            return false;
        }

        float radius = Radius;
        if (radius <= GeometryEpsilon)
        {
            return false;
        }

        tailTip = GetTailHandlePoint();
        var center = StartPoint;

        float directionX = tailTip.X - center.X;
        float directionY = tailTip.Y - center.Y;
        float directionLength = MathF.Sqrt(directionX * directionX + directionY * directionY);
        if (directionLength <= radius + GeometryEpsilon)
        {
            return false;
        }

        var arrowPoints = ArrowAnnotation.ComputeArrowPoints(
            center.X,
            center.Y,
            tailTip.X,
            tailTip.Y,
            StrokeWidth * ArrowAnnotation.ClassicArrowHeadWidthMultiplier);
        if (arrowPoints is not { } points)
        {
            return false;
        }

        shaftEndLeft = points.ShaftEndLeft;
        shaftEndRight = points.ShaftEndRight;
        wingLeft = points.WingLeft;
        wingRight = points.WingRight;

        float shaftHalfWidth = Distance(shaftEndLeft, shaftEndRight) / 2f;
        float normalizedDirectionX = directionX / directionLength;
        float normalizedDirectionY = directionY / directionLength;
        var perpendicular = new SKPoint(-normalizedDirectionY, normalizedDirectionX);

        var shaftBaseLeft = new SKPoint(
            center.X + perpendicular.X * shaftHalfWidth,
            center.Y + perpendicular.Y * shaftHalfWidth);
        var shaftBaseRight = new SKPoint(
            center.X - perpendicular.X * shaftHalfWidth,
            center.Y - perpendicular.Y * shaftHalfWidth);

        return TryGetCircleSegmentExitPoint(center, radius, shaftBaseLeft, tailTip, out shaftStartLeft) &&
               TryGetCircleSegmentExitPoint(center, radius, shaftBaseRight, tailTip, out shaftStartRight);
    }

    private static bool TryGetCircleSegmentExitPoint(SKPoint center, float radius, SKPoint start, SKPoint end, out SKPoint intersection)
    {
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;

        float offsetX = start.X - center.X;
        float offsetY = start.Y - center.Y;

        float a = (dx * dx) + (dy * dy);
        float b = 2f * ((offsetX * dx) + (offsetY * dy));
        float c = (offsetX * offsetX) + (offsetY * offsetY) - (radius * radius);

        float discriminant = (b * b) - (4f * a * c);
        if (a <= GeometryEpsilon || discriminant < 0)
        {
            intersection = default;
            return false;
        }

        float sqrtDiscriminant = MathF.Sqrt(discriminant);
        float t1 = (-b - sqrtDiscriminant) / (2f * a);
        float t2 = (-b + sqrtDiscriminant) / (2f * a);
        float t = MathF.Max(t1, t2);

        if (t < -GeometryEpsilon || t > 1f + GeometryEpsilon)
        {
            intersection = default;
            return false;
        }

        t = Math.Clamp(t, 0f, 1f);
        intersection = new SKPoint(start.X + (dx * t), start.Y + (dy * t));
        return true;
    }

    private static bool PointInTriangle(SKPoint point, SKPoint a, SKPoint b, SKPoint c)
    {
        float d1 = Sign(point, a, b);
        float d2 = Sign(point, b, c);
        float d3 = Sign(point, c, a);

        bool hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPositive = d1 > 0 || d2 > 0 || d3 > 0;

        return !(hasNegative && hasPositive);
    }

    private static bool PointInPolygon(SKPoint point, IReadOnlyList<SKPoint> polygon)
    {
        bool inside = false;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var current = polygon[i];
            var previous = polygon[j];
            bool intersects = ((current.Y > point.Y) != (previous.Y > point.Y)) &&
                              (point.X < ((previous.X - current.X) * (point.Y - current.Y) / ((previous.Y - current.Y) + GeometryEpsilon)) + current.X);

            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static float Sign(SKPoint p1, SKPoint p2, SKPoint p3)
    {
        return (p1.X - p3.X) * (p2.Y - p3.Y) -
               (p2.X - p3.X) * (p1.Y - p3.Y);
    }

    private static float Distance(SKPoint a, SKPoint b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float DistanceToSegment(SKPoint point, SKPoint start, SKPoint end)
    {
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        float segmentLengthSquared = dx * dx + dy * dy;

        if (segmentLengthSquared <= GeometryEpsilon)
        {
            return MathF.Sqrt((point.X - start.X) * (point.X - start.X) + (point.Y - start.Y) * (point.Y - start.Y));
        }

        float t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / segmentLengthSquared;
        t = Math.Clamp(t, 0f, 1f);

        float projectionX = start.X + t * dx;
        float projectionY = start.Y + t * dy;
        float deltaX = point.X - projectionX;
        float deltaY = point.Y - projectionY;

        return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private static SKRect UnionPoints(SKRect initialBounds, params SKPoint[] points)
    {
        var result = initialBounds;

        foreach (var point in points)
        {
            var pointBounds = new SKRect(point.X, point.Y, point.X, point.Y);
            result = SKRect.Union(result, pointBounds);
        }

        return result;
    }
}
