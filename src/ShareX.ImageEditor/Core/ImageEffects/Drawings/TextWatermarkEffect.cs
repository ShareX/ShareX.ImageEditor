using SkiaSharp;

namespace ShareX.ImageEditor.Core.ImageEffects.Drawings;

public sealed class TextWatermarkEffect : ImageEffect
{
    private int _cornerRadius = 4;
    private int _borderSize = 1;

    public string Text { get; set; } = "Text watermark";

    public DrawingPlacement Placement { get; set; } = DrawingPlacement.BottomRight;

    public SKPointI Offset { get; set; } = new SKPointI(5, 5);

    public bool AutoHide { get; set; }

    public string FontFamily { get; set; } = "Arial";

    public float FontSize { get; set; } = 15f;

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public SKColor TextColor { get; set; } = new SKColor(235, 235, 235);

    public bool DrawTextShadow { get; set; }

    public SKColor TextShadowColor { get; set; } = SKColors.Black;

    public SKPointI TextShadowOffset { get; set; } = new SKPointI(-1, -1);

    public int CornerRadius
    {
        get => _cornerRadius;
        set => _cornerRadius = Math.Max(0, value);
    }

    public int PaddingLeft { get; set; } = 5;

    public int PaddingTop { get; set; } = 5;

    public int PaddingRight { get; set; } = 5;

    public int PaddingBottom { get; set; } = 5;

    public bool DrawBorder { get; set; } = true;

    public SKColor BorderColor { get; set; } = SKColors.Black;

    public int BorderSize
    {
        get => _borderSize;
        set => _borderSize = Math.Max(0, value);
    }

    public bool DrawBackground { get; set; } = true;

    public SKColor BackgroundColor { get; set; } = new SKColor(42, 47, 56);

    public override string Name => "Text watermark";

    public override ImageEffectCategory Category => ImageEffectCategory.Drawings;

    public override bool HasParameters => true;

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (FontSize < 1f)
        {
            return source.Copy();
        }

        string text = DrawingEffectHelpers.ExpandTextVariables(Text, new SKSizeI(source.Width, source.Height));
        if (string.IsNullOrWhiteSpace(text))
        {
            return source.Copy();
        }

        int paddingLeft = Math.Max(0, PaddingLeft);
        int paddingTop = Math.Max(0, PaddingTop);
        int paddingRight = Math.Max(0, PaddingRight);
        int paddingBottom = Math.Max(0, PaddingBottom);

        SKFontStyleWeight weight = Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        SKFontStyleSlant slant = Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

        using SKTypeface? typeface = SKTypeface.FromFamilyName(FontFamily, weight, SKFontStyleWidth.Normal, slant);
        using SKPaint textPaint = new SKPaint
        {
            IsAntialias = true,
            Typeface = typeface,
            TextSize = FontSize,
            Color = TextColor
        };

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0)
        {
            return source.Copy();
        }

        SKFontMetrics metrics = textPaint.FontMetrics;
        float rawTextHeight = Math.Max(metrics.Descent - metrics.Ascent, textPaint.TextSize);
        float lineHeight = Math.Max(rawTextHeight + metrics.Leading, textPaint.TextSize);
        float baselineOffset = -metrics.Ascent;

        float maxTextWidth = 0f;
        foreach (string line in lines)
        {
            maxTextWidth = Math.Max(maxTextWidth, textPaint.MeasureText(line));
        }

        float totalTextHeight = rawTextHeight + Math.Max(0, lines.Length - 1) * lineHeight;
        if (maxTextWidth <= 0f && totalTextHeight <= 0f)
        {
            return source.Copy();
        }

        SKSizeI watermarkSize = new SKSizeI(
            (int)Math.Ceiling(maxTextWidth + paddingLeft + paddingRight),
            (int)Math.Ceiling(totalTextHeight + paddingTop + paddingBottom));

        if (watermarkSize.Width <= 0 || watermarkSize.Height <= 0)
        {
            return source.Copy();
        }

        SKPointI watermarkPosition = DrawingEffectHelpers.GetPosition(
            Placement,
            Offset,
            new SKSizeI(source.Width, source.Height),
            watermarkSize);

        SKRectI watermarkRect = new SKRectI(
            watermarkPosition.X,
            watermarkPosition.Y,
            watermarkPosition.X + watermarkSize.Width,
            watermarkPosition.Y + watermarkSize.Height);

        if (AutoHide && !DrawingEffectHelpers.Contains(new SKRectI(0, 0, source.Width, source.Height), watermarkRect))
        {
            return source.Copy();
        }

        SKBitmap result = source.Copy();
        using SKCanvas canvas = new SKCanvas(result);

        float radius = MathF.Min(CornerRadius, MathF.Min(watermarkRect.Width, watermarkRect.Height) / 2f);
        SKRect backgroundRect = new SKRect(watermarkRect.Left, watermarkRect.Top, watermarkRect.Right, watermarkRect.Bottom);

        if (DrawBackground && BackgroundColor.Alpha > 0)
        {
            using SKPaint backgroundPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = BackgroundColor
            };

            canvas.DrawRoundRect(backgroundRect, radius, radius, backgroundPaint);
        }

        if (DrawBorder && BorderColor.Alpha > 0)
        {
            using SKPaint borderPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                Color = BorderColor,
                StrokeWidth = Math.Max(1, BorderSize)
            };

            canvas.DrawRoundRect(backgroundRect, radius, radius, borderPaint);
        }

        float textX = watermarkRect.Left + paddingLeft;
        float textY = watermarkRect.Top + paddingTop + baselineOffset;

        if (DrawTextShadow && TextShadowColor.Alpha > 0)
        {
            using SKPaint shadowPaint = new SKPaint
            {
                IsAntialias = true,
                Typeface = typeface,
                TextSize = FontSize,
                Color = TextShadowColor
            };

            DrawLines(canvas, lines, textX + TextShadowOffset.X, textY + TextShadowOffset.Y, lineHeight, shadowPaint);
        }

        DrawLines(canvas, lines, textX, textY, lineHeight, textPaint);
        return result;
    }

    private static void DrawLines(SKCanvas canvas, IReadOnlyList<string> lines, float x, float baselineY, float lineHeight, SKPaint paint)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            canvas.DrawText(line, x, baselineY + (i * lineHeight), paint);
        }
    }
}
