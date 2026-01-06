#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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
using ShareX.Editor.Annotations;
using SkiaSharp;

namespace ShareX.Editor.Controls
{
    /// <summary>
    /// Custom control for rendering a speech balloon with a draggable tail using Avalonia Path
    /// </summary>
    public class SpeechBalloonControl : Control
    {
        public static readonly StyledProperty<SpeechBalloonAnnotation?> AnnotationProperty =
            AvaloniaProperty.Register<SpeechBalloonControl, SpeechBalloonAnnotation?>(nameof(Annotation));

        public SpeechBalloonAnnotation? Annotation
        {
            get => GetValue(AnnotationProperty);
            set => SetValue(AnnotationProperty, value);
        }

        static SpeechBalloonControl()
        {
            AffectsRender<SpeechBalloonControl>(AnnotationProperty);
            AffectsMeasure<SpeechBalloonControl>(AnnotationProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Annotation == null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

            // Get the annotation's bounds
            var annotationBounds = Annotation.GetBounds();
            var width = Math.Max(annotationBounds.Width, 20);
            var height = Math.Max(annotationBounds.Height, 20);

            // Create the speech balloon path using Avalonia geometry
            var geometry = CreateSpeechBalloonGeometry(width, height, Annotation.TailPoint);

            // Parse colors
            var strokeColor = Color.Parse(Annotation.StrokeColor);
            var fillColor = Color.Parse(Annotation.FillColor);

            // Draw fill
            context.DrawGeometry(
                new SolidColorBrush(fillColor),
                null,
                geometry
            );

            // Draw stroke
            context.DrawGeometry(
                null,
                new Pen(new SolidColorBrush(strokeColor), Annotation.StrokeWidth),
                geometry
            );

            // Draw text if present
            if (!string.IsNullOrEmpty(Annotation.Text))
            {
                var typeface = new Typeface(FontFamily.Default);
                var formattedText = new FormattedText(
                    Annotation.Text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    Annotation.FontSize,
                    new SolidColorBrush(strokeColor)
                );

                var textX = (width - formattedText.Width) / 2;
                var textY = (height - formattedText.Height) / 2;

                context.DrawText(formattedText, new Point(textX, textY));
            }
        }

        private Geometry CreateSpeechBalloonGeometry(float width, float height, SKPoint tailPoint)
        {
            var geometry = new StreamGeometry();

            using (var ctx = geometry.Open())
            {
                float radius = 10;
                float left = 0;
                float top = 0;
                float right = width;
                float bottom = height;

                // Default tail point if not set - place it below and centered
                var absoluteTailPoint = tailPoint == default
                    ? new SKPoint(Annotation!.StartPoint.X + width / 2, Annotation!.StartPoint.Y + height + 20)
                    : tailPoint;

                // Convert tail point to relative coordinates (relative to the balloon's top-left corner)
                var renderTailPoint = new Point(
                    absoluteTailPoint.X - Annotation!.StartPoint.X,
                    absoluteTailPoint.Y - Annotation!.StartPoint.Y
                );

                // Determine which edge the tail should connect to
                // Calculate the center of the balloon
                float centerX = width / 2;
                float centerY = height / 2;

                // Determine which quadrant/edge the tail is in relative to the balloon center
                float tailX = (float)renderTailPoint.X;
                float tailY = (float)renderTailPoint.Y;

                // Calculate angles to determine which edge
                // If tail is outside the balloon bounds, use its position relative to center
                bool tailOnBottom = tailY > bottom;
                bool tailOnTop = tailY < top;
                bool tailOnLeft = tailX < left;
                bool tailOnRight = tailX > right;

                // If tail is inside bounds, determine by which side is closer
                if (!tailOnBottom && !tailOnTop && !tailOnLeft && !tailOnRight)
                {
                    // Tail is inside - determine closest edge
                    float distToBottom = bottom - tailY;
                    float distToTop = tailY - top;
                    float distToLeft = tailX - left;
                    float distToRight = right - tailX;

                    float minDist = Math.Min(Math.Min(distToBottom, distToTop), Math.Min(distToLeft, distToRight));

                    tailOnBottom = minDist == distToBottom;
                    tailOnTop = !tailOnBottom && minDist == distToTop;
                    tailOnLeft = !tailOnBottom && !tailOnTop && minDist == distToLeft;
                    tailOnRight = !tailOnBottom && !tailOnTop && !tailOnLeft && minDist == distToRight;
                }
                else
                {
                    // Tail is outside - need to determine primary direction
                    // Use angle-based approach for corners
                    float dx = tailX - centerX;
                    float dy = tailY - centerY;

                    // Determine primary direction based on which component is larger
                    if (Math.Abs(dx) > Math.Abs(dy))
                    {
                        // Horizontal direction dominates
                        tailOnLeft = dx < 0;
                        tailOnRight = dx > 0;
                        tailOnTop = false;
                        tailOnBottom = false;
                    }
                    else
                    {
                        // Vertical direction dominates
                        tailOnTop = dy < 0;
                        tailOnBottom = dy > 0;
                        tailOnLeft = false;
                        tailOnRight = false;
                    }
                }

                float baseTailWidth = 30;

                // Ensure we have valid ranges for clamping
                float minConnectionMargin = radius + 5;

                // Calculate connection points for the tail
                // These are the two points where the tail connects to the balloon edge
                Point tailStart, tailEnd;

                // Start at top-left after the rounded corner
                ctx.BeginFigure(new Point(left + radius, top), true);

                // Top edge
                if (tailOnTop)
                {
                    float minX = left + minConnectionMargin;
                    float maxX = right - minConnectionMargin;
                    // Ensure min is not greater than max
                    if (minX > maxX) minX = maxX = (left + right) / 2;

                    float connectionX = Math.Clamp(tailX, minX, maxX);

                    // Ensure tail base doesn't extend beyond the valid edge range
                    float halfTailWidth = baseTailWidth / 2;
                    float tailStartX = Math.Max(minX, connectionX - halfTailWidth);
                    float tailEndX = Math.Min(maxX, connectionX + halfTailWidth);

                    tailStart = new Point(tailStartX, top);
                    tailEnd = new Point(tailEndX, top);

                    ctx.LineTo(tailStart);
                    ctx.LineTo(renderTailPoint);
                    ctx.LineTo(tailEnd);
                    ctx.LineTo(new Point(right - radius, top));
                }
                else
                {
                    ctx.LineTo(new Point(right - radius, top));
                }

                // Top-right corner
                ctx.ArcTo(
                    new Point(right, top + radius),
                    new Size(radius, radius),
                    0,
                    false,
                    SweepDirection.Clockwise
                );

                // Right edge
                if (tailOnRight)
                {
                    float minY = top + minConnectionMargin;
                    float maxY = bottom - minConnectionMargin;
                    // Ensure min is not greater than max
                    if (minY > maxY) minY = maxY = (top + bottom) / 2;

                    float connectionY = Math.Clamp(tailY, minY, maxY);

                    // Ensure tail base doesn't extend beyond the valid edge range
                    float halfTailWidth = baseTailWidth / 2;
                    float tailStartY = Math.Max(minY, connectionY - halfTailWidth);
                    float tailEndY = Math.Min(maxY, connectionY + halfTailWidth);

                    tailStart = new Point(right, tailStartY);
                    tailEnd = new Point(right, tailEndY);

                    ctx.LineTo(tailStart);
                    ctx.LineTo(renderTailPoint);
                    ctx.LineTo(tailEnd);
                    ctx.LineTo(new Point(right, bottom - radius));
                }
                else
                {
                    ctx.LineTo(new Point(right, bottom - radius));
                }

                // Bottom-right corner
                ctx.ArcTo(
                    new Point(right - radius, bottom),
                    new Size(radius, radius),
                    0,
                    false,
                    SweepDirection.Clockwise
                );

                // Bottom edge
                if (tailOnBottom)
                {
                    float minX = left + minConnectionMargin;
                    float maxX = right - minConnectionMargin;
                    // Ensure min is not greater than max
                    if (minX > maxX) minX = maxX = (left + right) / 2;

                    float connectionX = Math.Clamp(tailX, minX, maxX);

                    // Ensure tail base doesn't extend beyond the valid edge range
                    float halfTailWidth = baseTailWidth / 2;
                    float tailStartX = Math.Min(maxX, connectionX + halfTailWidth);
                    float tailEndX = Math.Max(minX, connectionX - halfTailWidth);

                    tailStart = new Point(tailStartX, bottom);
                    tailEnd = new Point(tailEndX, bottom);

                    ctx.LineTo(tailStart);
                    ctx.LineTo(renderTailPoint);
                    ctx.LineTo(tailEnd);
                    ctx.LineTo(new Point(left + radius, bottom));
                }
                else
                {
                    ctx.LineTo(new Point(left + radius, bottom));
                }

                // Bottom-left corner
                ctx.ArcTo(
                    new Point(left, bottom - radius),
                    new Size(radius, radius),
                    0,
                    false,
                    SweepDirection.Clockwise
                );

                // Left edge
                if (tailOnLeft)
                {
                    float minY = top + minConnectionMargin;
                    float maxY = bottom - minConnectionMargin;
                    // Ensure min is not greater than max
                    if (minY > maxY) minY = maxY = (top + bottom) / 2;

                    float connectionY = Math.Clamp(tailY, minY, maxY);

                    // Ensure tail base doesn't extend beyond the valid edge range
                    float halfTailWidth = baseTailWidth / 2;
                    float tailStartY = Math.Min(maxY, connectionY + halfTailWidth);
                    float tailEndY = Math.Max(minY, connectionY - halfTailWidth);

                    tailStart = new Point(left, tailStartY);
                    tailEnd = new Point(left, tailEndY);

                    ctx.LineTo(tailStart);
                    ctx.LineTo(renderTailPoint);
                    ctx.LineTo(tailEnd);
                    ctx.LineTo(new Point(left, top + radius));
                }
                else
                {
                    ctx.LineTo(new Point(left, top + radius));
                }

                // Top-left corner
                ctx.ArcTo(
                    new Point(left + radius, top),
                    new Size(radius, radius),
                    0,
                    false,
                    SweepDirection.Clockwise
                );

                ctx.EndFigure(true);
            }

            return geometry;
        }
    }
}
