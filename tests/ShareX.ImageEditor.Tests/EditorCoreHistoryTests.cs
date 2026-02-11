using ShareX.ImageEditor.Annotations;
using ShareX.ImageEditor.Serialization;
using SkiaSharp;

namespace ShareX.ImageEditor.Tests;

public class EditorCoreHistoryTests
{
    [Fact]
    public void AnnotationUndoRedo_RestoresRectangleAnnotation()
    {
        using var core = new EditorCore();
        core.LoadImage(CreateTestBitmap(96, 64));
        core.ActiveTool = EditorTool.Rectangle;

        core.OnPointerPressed(new SKPoint(8, 8));
        core.OnPointerMoved(new SKPoint(40, 32));
        core.OnPointerReleased(new SKPoint(40, 32));

        Assert.Single(core.Annotations);
        Assert.IsType<RectangleAnnotation>(core.Annotations[0]);
        Assert.True(core.CanUndo);

        core.Undo();
        Assert.Empty(core.Annotations);
        Assert.True(core.CanRedo);

        core.Redo();
        Assert.Single(core.Annotations);
        var rect = Assert.IsType<RectangleAnnotation>(core.Annotations[0]);
        Assert.Equal(8f, rect.GetBounds().Left);
        Assert.Equal(8f, rect.GetBounds().Top);
        Assert.Equal(40f, rect.GetBounds().Right);
        Assert.Equal(32f, rect.GetBounds().Bottom);
    }

    [Fact]
    public void EffectAnnotationUndoRedo_RestoresBlurAnnotation()
    {
        using var core = new EditorCore();
        core.LoadImage(CreateTestBitmap(120, 80));
        core.ActiveTool = EditorTool.Blur;

        core.OnPointerPressed(new SKPoint(10, 10));
        core.OnPointerMoved(new SKPoint(42, 30));
        core.OnPointerReleased(new SKPoint(42, 30));

        Assert.Single(core.Annotations);
        Assert.IsType<BlurAnnotation>(core.Annotations[0]);
        Assert.True(core.CanUndo);

        core.Undo();
        Assert.Empty(core.Annotations);
        Assert.True(core.CanRedo);

        core.Redo();
        Assert.Single(core.Annotations);
        Assert.IsType<BlurAnnotation>(core.Annotations[0]);
    }

    [Fact]
    public void CanvasCropUndoRedo_RestoresBitmapDimensions()
    {
        using var core = new EditorCore();
        core.LoadImage(CreateTestBitmap(160, 100));

        core.Crop(new SKRect(20, 10, 120, 70));

        Assert.NotNull(core.SourceImage);
        Assert.Equal(100, core.SourceImage!.Width);
        Assert.Equal(60, core.SourceImage.Height);
        Assert.True(core.CanUndo);

        core.Undo();
        Assert.NotNull(core.SourceImage);
        Assert.Equal(160, core.SourceImage!.Width);
        Assert.Equal(100, core.SourceImage.Height);
        Assert.True(core.CanRedo);

        core.Redo();
        Assert.NotNull(core.SourceImage);
        Assert.Equal(100, core.SourceImage!.Width);
        Assert.Equal(60, core.SourceImage.Height);
    }

    [Fact]
    public void AnnotationSerialization_RoundTripsTypesAndKeyProperties()
    {
        List<Annotation> annotations =
        [
            new RectangleAnnotation
            {
                StartPoint = new SKPoint(5, 6),
                EndPoint = new SKPoint(25, 20),
                StrokeColor = "#FF00FF00",
                FillColor = "#80FF0000",
                StrokeWidth = 3
            },
            new NumberAnnotation
            {
                StartPoint = new SKPoint(30, 30),
                Number = 7,
                FontSize = 22,
                FillColor = "#FFFFFFFF",
                StrokeColor = "#FF112233"
            },
            new TextAnnotation
            {
                StartPoint = new SKPoint(12, 40),
                EndPoint = new SKPoint(90, 70),
                Text = "hello",
                FontSize = 18,
                StrokeColor = "#FFABCDEF"
            }
        ];

        string json = AnnotationSerializer.Serialize(annotations);
        List<Annotation>? restored = AnnotationSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(3, restored!.Count);
        Assert.IsType<RectangleAnnotation>(restored[0]);
        Assert.IsType<NumberAnnotation>(restored[1]);
        Assert.IsType<TextAnnotation>(restored[2]);

        var number = (NumberAnnotation)restored[1];
        Assert.Equal(7, number.Number);
        Assert.Equal("#FF112233", number.StrokeColor);

        var text = (TextAnnotation)restored[2];
        Assert.Equal("hello", text.Text);
        Assert.Equal(18, text.FontSize);
    }

    private static SKBitmap CreateTestBitmap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();

        for (int y = 0; y < height; y++)
        {
            byte shade = (byte)(255 * y / Math.Max(1, height - 1));
            paint.Color = new SKColor(shade, (byte)(255 - shade), 180, 255);
            canvas.DrawLine(0, y, width, y, paint);
        }

        return bitmap;
    }
}
