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

namespace ShareX.ImageEditor.Core.Annotations;

internal static class CurvedSegmentHelper
{
    private const float CurveToleranceEpsilon = 0.5f;

    public static bool SupportsCurve(ICurvedSegmentAnnotation annotation)
    {
        return annotation switch
        {
            ArrowAnnotation { Style: ArrowStyle.Modern } => false,
            _ => true
        };
    }

    public static SKPoint GetMidpoint(SKPoint startPoint, SKPoint endPoint)
    {
        return new SKPoint((startPoint.X + endPoint.X) * 0.5f, (startPoint.Y + endPoint.Y) * 0.5f);
    }

    public static SKPoint GetEffectiveCurvePoint(ICurvedSegmentAnnotation annotation)
    {
        return SupportsCurve(annotation) && annotation.CurvePointActivated
            ? annotation.CurvePoint
            : GetMidpoint(annotation.StartPoint, annotation.EndPoint);
    }

    public static SKPoint GetQuadraticControlPoint(ICurvedSegmentAnnotation annotation)
    {
        var anchorPoint = GetEffectiveCurvePoint(annotation);
        return GetQuadraticControlPoint(annotation.StartPoint, annotation.EndPoint, anchorPoint);
    }

    public static SKPoint GetQuadraticControlPoint(SKPoint startPoint, SKPoint endPoint, SKPoint anchorPoint)
    {
        return new SKPoint(
            2f * anchorPoint.X - 0.5f * (startPoint.X + endPoint.X),
            2f * anchorPoint.Y - 0.5f * (startPoint.Y + endPoint.Y));
    }

    public static void ResetCurvePoint(ICurvedSegmentAnnotation annotation)
    {
        annotation.CurvePoint = GetMidpoint(annotation.StartPoint, annotation.EndPoint);
        annotation.CurvePointActivated = false;
    }

    public static bool HasCurve(ICurvedSegmentAnnotation annotation)
    {
        if (!SupportsCurve(annotation) || !annotation.CurvePointActivated)
        {
            return false;
        }

        var midpoint = GetMidpoint(annotation.StartPoint, annotation.EndPoint);
        var dx = annotation.CurvePoint.X - midpoint.X;
        var dy = annotation.CurvePoint.Y - midpoint.Y;
        return dx * dx + dy * dy > CurveToleranceEpsilon * CurveToleranceEpsilon;
    }

    public static void EnsureCurveActivated(ICurvedSegmentAnnotation annotation)
    {
        if (!SupportsCurve(annotation))
        {
            ResetCurvePoint(annotation);
            return;
        }

        if (!annotation.CurvePointActivated)
        {
            annotation.CurvePoint = GetMidpoint(annotation.StartPoint, annotation.EndPoint);
            annotation.CurvePointActivated = true;
        }
    }

    public static void SetCurvePoint(ICurvedSegmentAnnotation annotation, SKPoint curvePoint)
    {
        if (!SupportsCurve(annotation))
        {
            ResetCurvePoint(annotation);
            return;
        }

        annotation.CurvePoint = curvePoint;
        annotation.CurvePointActivated = true;
    }

    public static void OffsetCurvePoint(ICurvedSegmentAnnotation annotation, float deltaX, float deltaY)
    {
        if (!SupportsCurve(annotation) || !annotation.CurvePointActivated)
        {
            return;
        }

        annotation.CurvePoint = new SKPoint(annotation.CurvePoint.X + deltaX, annotation.CurvePoint.Y + deltaY);
    }

    public static void SetEndpoints(ICurvedSegmentAnnotation annotation, SKPoint startPoint, SKPoint endPoint)
    {
        bool keepNeutralCurvePoint = SupportsCurve(annotation) && annotation.CurvePointActivated && !HasCurve(annotation);

        annotation.StartPoint = startPoint;
        annotation.EndPoint = endPoint;

        if (keepNeutralCurvePoint)
        {
            annotation.CurvePoint = GetMidpoint(startPoint, endPoint);
        }
        else if (!SupportsCurve(annotation))
        {
            ResetCurvePoint(annotation);
        }
    }

    public static List<SKPoint> GetPathPoints(ICurvedSegmentAnnotation annotation, int segments = 24)
    {
        if (!HasCurve(annotation))
        {
            return new List<SKPoint> { annotation.StartPoint, annotation.EndPoint };
        }

        return SampleQuadraticBezier(annotation.StartPoint, GetQuadraticControlPoint(annotation), annotation.EndPoint, segments);
    }

    public static float DistanceToPath(ICurvedSegmentAnnotation annotation, SKPoint point, int segments = 24)
    {
        var pathPoints = GetPathPoints(annotation, segments);
        if (pathPoints.Count == 0)
        {
            return float.MaxValue;
        }

        if (pathPoints.Count == 1)
        {
            return Distance(point, pathPoints[0]);
        }

        float minDistance = float.MaxValue;

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            float distance = DistanceToSegment(point, pathPoints[i], pathPoints[i + 1]);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }

    public static SKRect GetBounds(ICurvedSegmentAnnotation annotation)
    {
        var pathPoints = GetPathPoints(annotation);
        if (pathPoints.Count == 0)
        {
            return SKRect.Empty;
        }

        float left = pathPoints[0].X;
        float top = pathPoints[0].Y;
        float right = pathPoints[0].X;
        float bottom = pathPoints[0].Y;

        foreach (var point in pathPoints)
        {
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }

        return new SKRect(left, top, right, bottom);
    }

    public static SKPoint GetQuadraticTangentAtStart(ICurvedSegmentAnnotation annotation)
    {
        var controlPoint = GetQuadraticControlPoint(annotation);
        var tangent = new SKPoint(controlPoint.X - annotation.StartPoint.X, controlPoint.Y - annotation.StartPoint.Y);

        if (Math.Abs(tangent.X) < 0.001f && Math.Abs(tangent.Y) < 0.001f)
        {
            tangent = new SKPoint(annotation.EndPoint.X - annotation.StartPoint.X, annotation.EndPoint.Y - annotation.StartPoint.Y);
        }

        return tangent;
    }

    public static SKPoint GetQuadraticTangentAtEnd(ICurvedSegmentAnnotation annotation)
    {
        var controlPoint = GetQuadraticControlPoint(annotation);
        var tangent = new SKPoint(annotation.EndPoint.X - controlPoint.X, annotation.EndPoint.Y - controlPoint.Y);

        if (Math.Abs(tangent.X) < 0.001f && Math.Abs(tangent.Y) < 0.001f)
        {
            tangent = new SKPoint(annotation.EndPoint.X - annotation.StartPoint.X, annotation.EndPoint.Y - annotation.StartPoint.Y);
        }

        return tangent;
    }

    private static List<SKPoint> SampleQuadraticBezier(SKPoint startPoint, SKPoint controlPoint, SKPoint endPoint, int segments)
    {
        int segmentCount = Math.Max(2, segments);
        var points = new List<SKPoint>(segmentCount + 1);

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float oneMinusT = 1f - t;
            points.Add(new SKPoint(
                oneMinusT * oneMinusT * startPoint.X + 2f * oneMinusT * t * controlPoint.X + t * t * endPoint.X,
                oneMinusT * oneMinusT * startPoint.Y + 2f * oneMinusT * t * controlPoint.Y + t * t * endPoint.Y));
        }

        return points;
    }

    private static float Distance(SKPoint first, SKPoint second)
    {
        float dx = first.X - second.X;
        float dy = first.Y - second.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static float DistanceToSegment(SKPoint point, SKPoint startPoint, SKPoint endPoint)
    {
        var dx = endPoint.X - startPoint.X;
        var dy = endPoint.Y - startPoint.Y;
        var segmentLengthSquared = dx * dx + dy * dy;

        if (segmentLengthSquared <= 0.001f)
        {
            return Distance(point, startPoint);
        }

        float t = ((point.X - startPoint.X) * dx + (point.Y - startPoint.Y) * dy) / segmentLengthSquared;
        t = Math.Clamp(t, 0f, 1f);

        var projection = new SKPoint(startPoint.X + t * dx, startPoint.Y + t * dy);
        return Distance(point, projection);
    }
}