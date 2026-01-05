using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ShareX.Editor.Annotations;

namespace ShareX.Editor.Controls
{
    /// <summary>
    /// Custom control for rendering spotlight annotations with proper darkening effect
    /// </summary>
    public class SpotlightControl : Control
    {

        public static readonly StyledProperty<SpotlightAnnotation?> AnnotationProperty =
            AvaloniaProperty.Register<SpotlightControl, SpotlightAnnotation?>(nameof(Annotation));

        public SpotlightAnnotation? Annotation
        {
            get => GetValue(AnnotationProperty);
            set => SetValue(AnnotationProperty, value);
        }

        static SpotlightControl()
        {
            AffectsRender<SpotlightControl>(AnnotationProperty);
        }

        public SpotlightControl()
        {
            // Make this control take up the full canvas space
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var annotation = Annotation;
            if (annotation != null)
            {
                // Create the hole geometry
                var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
                var annotatedBounds = annotation.GetBounds();
                var spotlightRect = new Rect(annotatedBounds.Left, annotatedBounds.Top, annotatedBounds.Width, annotatedBounds.Height);

                var geometry = new PathGeometry();
                var figure = new PathFigure { StartPoint = bounds.TopLeft, IsClosed = true };
                figure.Segments?.Add(new LineSegment { Point = bounds.TopRight });
                figure.Segments?.Add(new LineSegment { Point = bounds.BottomRight });
                figure.Segments?.Add(new LineSegment { Point = bounds.BottomLeft });
                geometry.Figures?.Add(figure);

                var holeFigure = new PathFigure { StartPoint = spotlightRect.TopLeft, IsClosed = true };
                holeFigure.Segments?.Add(new LineSegment { Point = spotlightRect.TopRight });
                holeFigure.Segments?.Add(new LineSegment { Point = spotlightRect.BottomRight });
                holeFigure.Segments?.Add(new LineSegment { Point = spotlightRect.BottomLeft });
                geometry.Figures?.Add(holeFigure);

                geometry.FillRule = FillRule.EvenOdd;

                // Create brush from DarkenOpacity (byte)
                var color = Color.FromUInt32((uint)((annotation.DarkenOpacity << 24) | 0x000000));
                var brush = new SolidColorBrush(color);

                context.DrawGeometry(brush, null, geometry);
            }
        }
    }
}
