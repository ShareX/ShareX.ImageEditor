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

using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Presentation.Controls;
using ShareX.ImageEditor.Presentation.Emoji;
using SkiaSharp;

namespace ShareX.ImageEditor.Presentation.Rendering;

/// <summary>
/// Indicates how an annotation visual is used by the host.
/// </summary>
public enum AnnotationVisualMode
{
    Persisted,
    Preview
}

/// <summary>
/// Shared factory/synchronizer for annotation visuals used by editor and region-capture hosts.
/// </summary>
public static class AnnotationVisualFactory
{
    /// <summary>
    /// Creates the visual control for the provided annotation.
    /// </summary>
    public static Control? CreateVisualControl(Annotation annotation, AnnotationVisualMode mode = AnnotationVisualMode.Persisted)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (mode == AnnotationVisualMode.Preview)
        {
            return annotation switch
            {
                TextAnnotation text => CreateTextPreviewPlaceholder(text),
                SpeechBalloonAnnotation balloon => CreateSpeechBalloonPreviewPlaceholder(balloon),
                SpotlightAnnotation spotlight => CreateSpotlightPreviewPlaceholder(spotlight),
                BlurAnnotation blur => CreateBlurPreviewPlaceholder(blur),
                PixelateAnnotation pixelate => CreatePixelatePreviewPlaceholder(pixelate),
                MagnifyAnnotation magnify => CreateMagnifyPreviewPlaceholder(magnify),
                HighlightAnnotation highlight => CreateHighlightPreviewPlaceholder(highlight),
                _ => CreatePersistedVisualControl(annotation)
            };
        }

        return CreatePersistedVisualControl(annotation);
    }

    /// <summary>
    /// Updates an existing visual control to reflect the annotation's current geometry and position.
    /// </summary>
    public static void UpdateVisualControl(
        Control control,
        Annotation annotation,
        AnnotationVisualMode mode = AnnotationVisualMode.Persisted,
        double canvasWidth = 0,
        double canvasHeight = 0,
        bool useInteractiveEmojiRender = false)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(annotation);

        bool ensureMinimumSize = mode == AnnotationVisualMode.Preview;

        switch (annotation)
        {
            case RectangleAnnotation rectangle when control is Rectangle rectangleControl:
                ApplyBoundsControl(rectangleControl, rectangle.GetBounds(), ensureMinimumSize);
                rectangleControl.RadiusX = Math.Max(0, rectangle.CornerRadius);
                rectangleControl.RadiusY = Math.Max(0, rectangle.CornerRadius);
                break;

            case LineAnnotation lineAnnotation when control is Avalonia.Controls.Shapes.Path linePath:
                linePath.Data = lineAnnotation.CreateLineGeometry();
                break;

            case ArrowAnnotation arrow when control is Avalonia.Controls.Shapes.Path arrowPath:
                var brush = new SolidColorBrush(Color.Parse(arrow.StrokeColor));
                arrowPath.Stroke = brush;
                arrowPath.Fill = brush;
                arrowPath.StrokeThickness = arrow.StrokeWidth;
                arrowPath.StrokeLineCap = PenLineCap.Round;
                arrowPath.StrokeJoin = PenLineJoin.Round;
                arrowPath.Data = arrow.CreateArrowGeometry();
                break;

            case FreehandAnnotation freehand when control is Avalonia.Controls.Shapes.Path freehandPath:
                freehandPath.Data = freehand.CreateSmoothedGeometry();
                break;

            case NumberAnnotation number when control is StepControl stepControl:
                stepControl.Annotation = number;
                ApplyBoundsControl(stepControl, number.GetBounds(), ensureMinimumSize);
                stepControl.InvalidateVisual();
                break;

            case TextAnnotation text when mode == AnnotationVisualMode.Preview && control is Rectangle:
                ApplyBoundsControl(control, text.GetBounds(), ensureMinimumSize: true);
                break;

            case TextAnnotation text when control is OutlinedTextControl textControl:
                var textBounds = text.GetBounds();
                textControl.Annotation = text;
                Canvas.SetLeft(textControl, textBounds.Left);
                Canvas.SetTop(textControl, textBounds.Top);
                textControl.Width = Math.Max(1, textBounds.Width);
                textControl.Height = Math.Max(1, textBounds.Height);

                // Note: The text content, font size, bold/italic, etc. are handled automatically by the control's rendering
                // using the bound Annotation property, but we must apply the transform and invalidate it explicitly here.

                ApplyRotationTransform(textControl, text.RotationAngle);

                textControl.InvalidateVisual();
                textControl.InvalidateMeasure();
                break;

            case SpeechBalloonAnnotation balloon when mode == AnnotationVisualMode.Preview && control is Rectangle:
                ApplyBoundsControl(control, balloon.GetBounds(), ensureMinimumSize: true);
                if (control is Rectangle balloonPreview)
                {
                    balloonPreview.RadiusX = Math.Max(0, balloon.CornerRadius);
                    balloonPreview.RadiusY = Math.Max(0, balloon.CornerRadius);
                }
                break;

            case SpeechBalloonAnnotation balloon when control is SpeechBalloonControl balloonControl:
                balloonControl.Annotation = balloon;
                ApplyBoundsControl(balloonControl, balloon.GetBounds(), ensureMinimumSize);
                balloonControl.InvalidateVisual();
                break;

            case SpotlightAnnotation spotlight when mode == AnnotationVisualMode.Preview && control is Rectangle:
                ApplyBoundsControl(control, spotlight.GetBounds(), ensureMinimumSize: true);
                break;

            case SpotlightAnnotation spotlight when control is SpotlightControl spotlightControl:
                if (canvasWidth > 0 && canvasHeight > 0)
                {
                    spotlight.CanvasSize = new SKSize((float)canvasWidth, (float)canvasHeight);
                }

                spotlightControl.Annotation = spotlight;
                Canvas.SetLeft(spotlightControl, 0);
                Canvas.SetTop(spotlightControl, 0);
                spotlightControl.Width = Math.Max(1, spotlight.CanvasSize.Width);
                spotlightControl.Height = Math.Max(1, spotlight.CanvasSize.Height);
                spotlightControl.InvalidateVisual();
                break;

            case CursorAnnotation cursorAnnotation when control is Image cursorControl:
                RefreshCursorImage(cursorAnnotation, cursorControl);
                break;

            case EmojiAnnotation emojiAnnotation when control is Image emojiControl:
                RefreshEmojiImage(emojiAnnotation, emojiControl, useInteractiveEmojiRender);
                break;

            case ImageAnnotation imageAnnotation when control is Image imageControl:
                if (imageAnnotation.ImageBitmap != null)
                {
                    imageControl.Source = BitmapConversionHelpers.ToAvaloniBitmap(imageAnnotation.ImageBitmap);
                }

                var imageBounds = imageAnnotation.GetBounds();
                Canvas.SetLeft(imageControl, imageBounds.Left);
                Canvas.SetTop(imageControl, imageBounds.Top);
                imageControl.Width = Math.Max(1, imageBounds.Width);
                imageControl.Height = Math.Max(1, imageBounds.Height);
                ApplyRotationTransform(imageControl, imageAnnotation.RotationAngle);
                break;

            default:
                ApplyBoundsControl(control, annotation.GetBounds(), ensureMinimumSize);
                break;
        }
    }

    private static Control? CreatePersistedVisualControl(Annotation annotation)
    {
        return annotation switch
        {
            SmartEraserAnnotation smartEraser => smartEraser.CreateVisual(),
            RectangleAnnotation rect => rect.CreateVisual(),
            EllipseAnnotation ellipse => ellipse.CreateVisual(),
            LineAnnotation line => line.CreateVisual(),
            ArrowAnnotation arrow => arrow.CreateVisual(),
            TextAnnotation text => text.CreateVisual(),
            SpeechBalloonAnnotation balloon => balloon.CreateVisual(),
            NumberAnnotation number => number.CreateVisual(),
            BlurAnnotation blur => blur.CreateVisual(),
            PixelateAnnotation pixelate => pixelate.CreateVisual(),
            MagnifyAnnotation magnify => magnify.CreateVisual(),
            HighlightAnnotation highlight => highlight.CreateVisual(),
            SpotlightAnnotation spotlight => spotlight.CreateVisual(),
            FreehandAnnotation freehand => freehand.CreateVisual(),
            CursorAnnotation cursor => CreateCursorVisual(cursor),
            EmojiAnnotation emoji => CreateEmojiVisual(emoji),
            ImageAnnotation image => CreateImageVisual(image),
            _ => null
        };
    }

    private static Control CreateCursorVisual(CursorAnnotation cursorAnnotation)
    {
        var image = new Image
        {
            Tag = cursorAnnotation
        };

        RefreshCursorImage(cursorAnnotation, image);
        return image;
    }

    private static Control CreateEmojiVisual(EmojiAnnotation emojiAnnotation)
    {
        var image = new Image
        {
            Tag = emojiAnnotation
        };

        RefreshEmojiImage(emojiAnnotation, image);
        return image;
    }

    private static Control CreateImageVisual(ImageAnnotation imageAnnotation)
    {
        var image = new Image
        {
            Tag = imageAnnotation
        };

        if (imageAnnotation.ImageBitmap != null)
        {
            image.Source = BitmapConversionHelpers.ToAvaloniBitmap(imageAnnotation.ImageBitmap);
        }

        var imageBounds = imageAnnotation.GetBounds();
        image.Width = Math.Max(1, imageBounds.Width);
        image.Height = Math.Max(1, imageBounds.Height);
        ApplyRotationTransform(image, imageAnnotation.RotationAngle);

        return image;
    }

    private static void RefreshCursorImage(CursorAnnotation cursorAnnotation, Image imageControl)
    {
        if (cursorAnnotation.ImageBitmap == null)
        {
            SKBitmap? renderedBitmap = WindowsCursorBitmapRenderer.CreateAnnotationBitmap(cursorAnnotation.CursorType);
            if (renderedBitmap != null)
            {
                cursorAnnotation.SetImage(renderedBitmap);
            }
        }

        imageControl.Source = cursorAnnotation.ImageBitmap != null
            ? BitmapConversionHelpers.ToAvaloniBitmap(cursorAnnotation.ImageBitmap)
            : null;

        var cursorBounds = cursorAnnotation.GetBounds();
        Canvas.SetLeft(imageControl, cursorBounds.Left);
        Canvas.SetTop(imageControl, cursorBounds.Top);
        imageControl.Width = Math.Max(1, cursorBounds.Width);
        imageControl.Height = Math.Max(1, cursorBounds.Height);
        ApplyRotationTransform(imageControl, cursorAnnotation.RotationAngle);
    }

    private static void RefreshEmojiImage(EmojiAnnotation emojiAnnotation, Image imageControl, bool useInteractiveRender = false)
    {
        var imageBounds = emojiAnnotation.GetBounds();
        int renderSize = Math.Max(1, (int)Math.Ceiling(Math.Max(imageBounds.Width, imageBounds.Height)));
        int targetBitmapSize = useInteractiveRender
            ? WindowsEmojiBitmapRenderer.GetInteractiveStickerSize(renderSize)
            : renderSize;

        bool needsBitmapRefresh = !string.IsNullOrWhiteSpace(emojiAnnotation.UnicodeSequence)
            && (emojiAnnotation.ImageBitmap == null
                || emojiAnnotation.ImageBitmap.Width != targetBitmapSize
                || emojiAnnotation.ImageBitmap.Height != targetBitmapSize);

        if (needsBitmapRefresh)
        {
            var renderedBitmap = useInteractiveRender
                ? WindowsEmojiBitmapRenderer.RenderInteractiveStickerBitmap(emojiAnnotation.UnicodeSequence, renderSize)
                : WindowsEmojiBitmapRenderer.RenderStickerBitmap(emojiAnnotation.UnicodeSequence, renderSize);

            if (renderedBitmap != null)
            {
                emojiAnnotation.SetImage(renderedBitmap);
            }
        }

        if (emojiAnnotation.ImageBitmap != null && (needsBitmapRefresh || imageControl.Source == null))
        {
            if (imageControl.Source is IDisposable previousSource)
            {
                previousSource.Dispose();
            }

            imageControl.Source = BitmapConversionHelpers.ToAvaloniBitmap(emojiAnnotation.ImageBitmap);
        }

        Canvas.SetLeft(imageControl, imageBounds.Left);
        Canvas.SetTop(imageControl, imageBounds.Top);
        imageControl.Width = Math.Max(1, imageBounds.Width);
        imageControl.Height = Math.Max(1, imageBounds.Height);
        ApplyRotationTransform(imageControl, emojiAnnotation.RotationAngle);
    }

    private static Control CreateTextPreviewPlaceholder(TextAnnotation annotation)
    {
        return new Rectangle
        {
            Stroke = new SolidColorBrush(Color.Parse(annotation.StrokeColor)),
            StrokeThickness = 1,
            StrokeDashArray = new AvaloniaList<double> { 4, 4 },
            Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Tag = annotation
        };
    }

    private static Control CreateSpeechBalloonPreviewPlaceholder(SpeechBalloonAnnotation annotation)
    {
        return new Rectangle
        {
            Stroke = new SolidColorBrush(Color.Parse(annotation.StrokeColor)),
            StrokeThickness = annotation.StrokeWidth,
            Fill = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            RadiusX = Math.Max(0, annotation.CornerRadius),
            RadiusY = Math.Max(0, annotation.CornerRadius),
            Tag = annotation
        };
    }

    private static Control CreateSpotlightPreviewPlaceholder(SpotlightAnnotation annotation)
    {
        return new Rectangle
        {
            Fill = Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            StrokeThickness = 2,
            StrokeDashArray = new AvaloniaList<double> { 6, 3 },
            Tag = annotation
        };
    }

    private static Control CreateBlurPreviewPlaceholder(BlurAnnotation annotation)
    {
        return new Rectangle
        {
            Fill = new SolidColorBrush(Color.Parse("#200000FF")),
            Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 0, 255)),
            StrokeThickness = 1,
            Tag = annotation
        };
    }

    private static Control CreatePixelatePreviewPlaceholder(PixelateAnnotation annotation)
    {
        return new Rectangle
        {
            Fill = new SolidColorBrush(Color.Parse("#2000FF00")),
            Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
            StrokeThickness = 1,
            Tag = annotation
        };
    }

    private static Control CreateMagnifyPreviewPlaceholder(MagnifyAnnotation annotation)
    {
        return new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(30, 211, 211, 211)),
            Stroke = new SolidColorBrush(Color.FromArgb(80, 100, 100, 100)),
            StrokeThickness = 1,
            Tag = annotation
        };
    }

    private static Control CreateHighlightPreviewPlaceholder(HighlightAnnotation annotation)
    {
        Color baseColor = Color.Parse(annotation.StrokeColor);
        Color highlightColor = Color.FromArgb(0x55, baseColor.R, baseColor.G, baseColor.B);
        return new Rectangle
        {
            Fill = new SolidColorBrush(highlightColor),
            Stroke = Brushes.Transparent,
            StrokeThickness = 0,
            Tag = annotation
        };
    }

    private static void ApplyBoundsControl(Control control, SKRect bounds, bool ensureMinimumSize)
    {
        double left = bounds.Left;
        double top = bounds.Top;
        double width = ensureMinimumSize ? Math.Max(1, bounds.Width) : bounds.Width;
        double height = ensureMinimumSize ? Math.Max(1, bounds.Height) : bounds.Height;

        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);
        control.Width = width;
        control.Height = height;
    }

    private static void ApplyRotationTransform(Control control, float rotationAngle)
    {
        if (rotationAngle != 0)
        {
            control.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
            control.RenderTransform = new RotateTransform(rotationAngle);
        }
        else
        {
            control.RenderTransform = null;
        }
    }
}