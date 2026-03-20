using ShareX.ImageEditor.Core.ImageEffects.Helpers;
using SkiaSharp;

namespace ShareX.ImageEditor.Core.ImageEffects.Filters;

public class HeatHazeRefractionImageEffect : FilterImageEffect
{
    public override string Name => "Heat haze refraction";
    public override bool HasParameters => true;

    public float Strength { get; set; } = 45f; // 0..100
    public float Frequency { get; set; } = 40f; // 0..100
    public float BlurRadius { get; set; } = 10f; // 0..200 (clamped internally)
    public float Offset { get; set; } = 6f; // -200..200
    public float LuminanceInfluence { get; set; } = 55f; // 0..100
    public int Seed { get; set; } = 2026;

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        float strength01 = Math.Clamp(Strength, 0f, 100f) / 100f;
        if (strength01 <= 0f)
        {
            return source.Copy();
        }

        float frequency01 = Math.Clamp(Frequency, 0f, 100f) / 100f;
        float blurRadiusPx = Math.Clamp(BlurRadius, 0f, 30f);
        float maxOffsetPx = Math.Clamp(Offset, -200f, 200f) * (0.5f + 1.2f * frequency01) * strength01;
        float lumInfluence01 = Math.Clamp(LuminanceInfluence, 0f, 100f) / 100f;

        int width = source.Width;
        int height = source.Height;
        if (width <= 0 || height <= 0)
        {
            return source.Copy();
        }

        SKColor[] srcPixels = source.Pixels;
        SKColor[] dstPixels = new SKColor[srcPixels.Length];

        SKBitmap? blurredBitmap = null;
        SKColor[]? blurredPixels = null;
        if (blurRadiusPx > 0.01f && strength01 > 0.01f)
        {
            blurredBitmap = CreateBlurred(source, blurRadiusPx);
            blurredPixels = blurredBitmap.Pixels;
        }

        // Precompute for speed.
        float invW = 1f / MathF.Max(1, width - 1);
        float invH = 1f / MathF.Max(1, height - 1);
        float phase = (Seed % 10_000) * 0.017f;

        Parallel.For(0, height, y =>
        {
            int row = y * width;
            float v = y * invH;

            for (int x = 0; x < width; x++)
            {
                int idx = row + x;
                SKColor src = srcPixels[idx];

                float lum01 = ((0.2126f * src.Red) + (0.7152f * src.Green) + (0.0722f * src.Blue)) / (255f * 1f);
                float lumWeight = ProceduralEffectHelper.Lerp(1f, lum01, lumInfluence01);

                float u = x * invW;

                // Procedural "heat shimmer": layered sin/cos + seeded noise.
                float w1 = MathF.Sin((u * 10f + v * 6f) * (0.6f + frequency01 * 1.8f) + phase);
                float w2 = MathF.Cos((u * 7f - v * 12f) * (0.4f + frequency01 * 1.7f) - phase * 1.23f);
                float w3 = (ProceduralEffectHelper.Hash01(x, y, Seed) * 2f) - 1f;

                float dx = (w1 * 0.60f + w2 * 0.40f) * maxOffsetPx * lumWeight;
                float dy = (w2 * 0.55f - w1 * 0.45f) * maxOffsetPx * lumWeight;

                float sx = x + dx;
                float sy = y + dy;

                SKColor warped = ProceduralEffectHelper.BilinearSample(srcPixels, width, height, sx, sy);

                float r01 = src.Red / 255f;
                float g01 = src.Green / 255f;
                float b01 = src.Blue / 255f;

                float wr = warped.Red / 255f;
                float wg = warped.Green / 255f;
                float wb = warped.Blue / 255f;

                // Optional: blend in a single blurred sample for softer refraction.
                if (blurredPixels != null)
                {
                    SKColor bl = blurredPixels[idx];
                    float blurMix = strength01 * (0.25f + 0.75f * (blurRadiusPx / 30f));
                    wr = ProceduralEffectHelper.Lerp(wr, bl.Red / 255f, blurMix);
                    wg = ProceduralEffectHelper.Lerp(wg, bl.Green / 255f, blurMix);
                    wb = ProceduralEffectHelper.Lerp(wb, bl.Blue / 255f, blurMix);
                }

                float outR = ProceduralEffectHelper.Lerp(r01, wr, strength01);
                float outG = ProceduralEffectHelper.Lerp(g01, wg, strength01);
                float outB = ProceduralEffectHelper.Lerp(b01, wb, strength01);

                dstPixels[idx] = new SKColor(
                    ProceduralEffectHelper.ClampToByte(outR * 255f),
                    ProceduralEffectHelper.ClampToByte(outG * 255f),
                    ProceduralEffectHelper.ClampToByte(outB * 255f),
                    src.Alpha);
            }
        });

        blurredBitmap?.Dispose();
        return new SKBitmap(width, height, source.ColorType, source.AlphaType)
        {
            Pixels = dstPixels
        };
    }

    private static SKBitmap CreateBlurred(SKBitmap source, float radius)
    {
        if (radius <= 0.01f) return source.Copy();

        SKBitmap blurred = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using SKCanvas canvas = new SKCanvas(blurred);
        using SKPaint paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
            ImageFilter = SKImageFilter.CreateBlur(radius, radius)
        };
        canvas.DrawBitmap(source, 0, 0, paint);
        return blurred;
    }
}

