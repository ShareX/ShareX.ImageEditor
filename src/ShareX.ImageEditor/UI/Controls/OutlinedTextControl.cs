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
using ShareX.ImageEditor.Annotations;
using System.Globalization;

namespace ShareX.ImageEditor.Controls
{
    /// <summary>
    /// Custom control for rendering text with both an outline (stroke) and a fill.
    /// Avalonia's native TextBox does not support text strokes, so we render via FormattedText and Geometry.
    /// </summary>
    public class OutlinedTextControl : Control
    {
        public static readonly StyledProperty<TextAnnotation?> AnnotationProperty =
            AvaloniaProperty.Register<OutlinedTextControl, TextAnnotation?>(nameof(Annotation));

        public TextAnnotation? Annotation
        {
            get => GetValue(AnnotationProperty);
            set => SetValue(AnnotationProperty, value);
        }

        static OutlinedTextControl()
        {
            AffectsRender<OutlinedTextControl>(AnnotationProperty);
            AffectsMeasure<OutlinedTextControl>(AnnotationProperty);
        }

        public OutlinedTextControl()
        {
            ClipToBounds = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Annotation == null || string.IsNullOrEmpty(Annotation.Text)) return;

            // Match generic padding used by the TextBox (4px)
            var padding = new Thickness(4);
            var bounds = new Rect(padding.Left, padding.Top, Bounds.Width - padding.Left - padding.Right, Bounds.Height - padding.Top - padding.Bottom);

            var typeface = new Typeface(
                Annotation.FontFamily,
                Annotation.IsItalic ? FontStyle.Italic : FontStyle.Normal,
                Annotation.IsBold ? FontWeight.Bold : FontWeight.Normal);

            var formattedText = new FormattedText(
                Annotation.Text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                Annotation.FontSize,
                Brushes.Black); // Avalonia requires a brush to properly construct text extents in some backends

            // Create geometry from formatted text
            var textGeometry = formattedText.BuildGeometry(new Point(padding.Left, padding.Top));
            if (textGeometry == null) return;

            // Setup brushes based on the annotation's color properties.
            // Text color is now FillColor, Outline color is StrokeColor.
            
            // Standard behavior in many editors is that transparent fill means no fill.
            IBrush? fillBrush = null;
            if (!string.IsNullOrEmpty(Annotation.FillColor))
            {
                var fillColor = Color.Parse(Annotation.FillColor);
                if (fillColor.A > 0)
                {
                    fillBrush = new SolidColorBrush(fillColor);
                }
            }
            
            // If fill is completely transparent and stroke is set, we just want to draw the stroke.
            // Wait, actually, the user wants FillColor = text color, StrokeColor = outline color.
            // If fill brush is null, the text body will be invisible. If that's what they set, respect it.
            // However, default fill was transparent "#00000000" in TextAnnotation.
            // So if they create a default text with no fill, it might be invisible if we don't handle it.
            // BUT wait, EditorInputController already forces StrokeColor as text color if transparent. We'll fix EditorInputController later.

            IPen? strokePen = null;
            if (Annotation.StrokeWidth > 0 && !string.IsNullOrEmpty(Annotation.StrokeColor))
            {
                var strokeColor = Color.Parse(Annotation.StrokeColor);
                if (strokeColor.A > 0)
                {
                    strokePen = new Pen(new SolidColorBrush(strokeColor), Annotation.StrokeWidth, lineJoin: PenLineJoin.Round);
                }
            }

            // Draw shadow if enabled
            if (Annotation.ShadowEnabled)
            {
                var shadowOffset = new Point(3 + padding.Left, 3 + padding.Top);
                var shadowGeometry = formattedText.BuildGeometry(shadowOffset);
                if (shadowGeometry != null)
                {
                    var shadowBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));
                    
                    // If there's a stroke, we should draw the shadow of the stroked geometry too
                    if (strokePen != null)
                    {
                        // Avalonia drawing context doesn't easily let us stroke the shadow with BlurRadius in a simple way
                        // But we can just draw the shadow geometry filled. The drop shadow effect on TextBox blurred the bounds.
                        // For pure geometry, drawing it offset is decent.
                        context.DrawGeometry(shadowBrush, new Pen(shadowBrush, Annotation.StrokeWidth, lineJoin: PenLineJoin.Round), shadowGeometry);
                    }
                    else
                    {
                        context.DrawGeometry(shadowBrush, null, shadowGeometry);
                    }
                }
            }

            // Draw main text (Stroke then Fill, so fill is on top of stroke)
            // Avalonia's DrawGeometry draws stroke on top of fill by default.
            // To get the stroke BEHIND the fill (standard typography outline), we must draw the stroke geometry first, then fill geometry.
            if (strokePen != null)
            {
                context.DrawGeometry(null, strokePen, textGeometry);
            }

            if (fillBrush != null)
            {
                context.DrawGeometry(fillBrush, null, textGeometry);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Annotation == null || string.IsNullOrEmpty(Annotation.Text))
            {
                return new Size(0, 0);
            }

            var typeface = new Typeface(
                Annotation.FontFamily,
                Annotation.IsItalic ? FontStyle.Italic : FontStyle.Normal,
                Annotation.IsBold ? FontWeight.Bold : FontWeight.Normal);

            var formattedText = new FormattedText(
                Annotation.Text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                Annotation.FontSize,
                Brushes.Black);

            // Add padding (4px all sides) + stroke width padding to avoid clipping
            double strokePadding = Annotation.StrokeWidth;
            double padding = 8; // 4 * 2
            
            double width = formattedText.Width + padding + strokePadding;
            double height = formattedText.Height + padding + strokePadding;

            if (double.IsNaN(width) || width < 0) width = 0;
            if (double.IsNaN(height) || height < 0) height = 0;

            return new Size(width, height);
        }
    }
}
