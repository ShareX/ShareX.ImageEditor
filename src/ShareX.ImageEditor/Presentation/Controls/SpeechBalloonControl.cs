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
using ShareX.ImageEditor.Core.Annotations;

namespace ShareX.ImageEditor.Presentation.Controls
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

            var bodyGeometry = CreateBodyGeometry(width, height);
            var tailGeometry = CreateTailGeometry(Annotation);
            Geometry geometry = tailGeometry != null
                ? new CombinedGeometry(GeometryCombineMode.Union, bodyGeometry, tailGeometry)
                : bodyGeometry;

            // Parse colors
            var strokeColor = Color.Parse(Annotation.StrokeColor);
            var fillColor = Color.Parse(Annotation.FillColor);
            var fillBrush = new SolidColorBrush(fillColor);
            var strokePen = new Pen(new SolidColorBrush(strokeColor), Annotation.StrokeWidth);

            context.DrawGeometry(
                fillBrush,
                null,
                geometry
            );

            context.DrawGeometry(
                null,
                strokePen,
                geometry
            );

            // Draw text if present
            if (!string.IsNullOrEmpty(Annotation.Text))
            {
                var textColor = Color.Parse(Annotation.TextColor);
                var typeface = new Typeface(FontFamily.Default);
                var formattedText = new FormattedText(
                    Annotation.Text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    Annotation.FontSize,
                    new SolidColorBrush(textColor)
                );

                // Calculate centered text position with padding
                var padding = 12; // Adjusted to match TextBox padding

                // Allow wrapping
                var maxTextWidth = Math.Max(0, width - (padding * 2));
                formattedText.MaxTextWidth = maxTextWidth;
                var textX = Math.Max(padding, (width - formattedText.Width) / 2);
                var textY = Math.Max(padding, (height - formattedText.Height) / 2);

                // Ensure text stays within bounds
                textX = Math.Min(textX, width - formattedText.Width - padding);
                textY = Math.Min(textY, height - formattedText.Height - padding);

                context.DrawText(formattedText, new Point(textX, textY));
            }
        }

        private static Geometry CreateBodyGeometry(double width, double height)
        {
            var geometry = new StreamGeometry();

            using (var ctx = geometry.Open())
            {
                const double radius = 10;
                double left = 0;
                double top = 0;
                double right = width;
                double bottom = height;

                // Start at top-left after the rounded corner
                ctx.BeginFigure(new Point(left + radius, top), true);

                ctx.LineTo(new Point(right - radius, top));

                // Top-right corner
                ctx.ArcTo(
                    new Point(right, top + radius),
                    new Size(radius, radius),
                    0,
                    false,
                    SweepDirection.Clockwise
                );

                ctx.LineTo(new Point(right, bottom - radius));

                // Bottom-right corner
                ctx.ArcTo(
                    new Point(right - radius, bottom),
                    new Size(radius, radius),
                    0,
                    false,
                    SweepDirection.Clockwise
                );

                ctx.LineTo(new Point(left + radius, bottom));

                // Bottom-left corner
                ctx.ArcTo(
                    new Point(left, bottom - radius),
                    new Size(radius, radius),
                    0,
                    false,
                    SweepDirection.Clockwise
                );

                ctx.LineTo(new Point(left, top + radius));

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

        private Geometry? CreateTailGeometry(SpeechBalloonAnnotation annotation)
        {
            if (!annotation.TryGetTailPolygon(out var tailBaseStart, out var tailTip, out var tailBaseEnd))
            {
                return null;
            }

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(ToRenderPoint(annotation, tailBaseStart), true);
                ctx.LineTo(ToRenderPoint(annotation, tailTip));
                ctx.LineTo(ToRenderPoint(annotation, tailBaseEnd));
                ctx.EndFigure(true);
            }

            return geometry;
        }

        private static Point ToRenderPoint(SpeechBalloonAnnotation annotation, SkiaSharp.SKPoint point)
        {
            var bounds = annotation.GetBounds();
            return new Point(point.X - bounds.Left, point.Y - bounds.Top);
        }
    }
}
