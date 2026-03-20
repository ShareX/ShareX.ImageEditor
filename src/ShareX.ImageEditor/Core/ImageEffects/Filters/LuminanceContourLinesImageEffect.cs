using ShareX.ImageEditor.Core.ImageEffects.Helpers;
using SkiaSharp;

namespace ShareX.ImageEditor.Core.ImageEffects.Filters;

public class LuminanceContourLinesImageEffect : FilterImageEffect
{
    public override string Name => "Luminance contour lines";
    public override bool HasParameters => true;

    public int Levels { get; set; } = 12; // ~2..64
    public float LineWidth { get; set; } = 6f; // 0..200
    public float LineStrength { get; set; } = 65f; // 0..100
    public float BackgroundStrength { get; set; } = 20f; // 0..100
    public float Threshold { get; set; } = 0f; // 0..255 heuristic (used as luminance bias)
    public bool Invert { get; set; }
    public SKColor LineColor { get; set; } = new SKColor(0, 0, 0, 255);

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        int levels = Math.Clamp(Levels, 2, 64);
        if (levels <= 0)
        {
            return source.Copy();
        }

        float lineStrength01 = Math.Clamp(LineStrength, 0f, 100f) / 100f;
        if (lineStrength01 <= 0f)
        {
            return source.Copy();
        }

        float backgroundStrength01 = Math.Clamp(BackgroundStrength, 0f, 100f) / 100f;
        float thresholdBias = Math.Clamp(Threshold, 0f, 255f) / 255f;

        float lineWidth01 = Math.Clamp(LineWidth, 0f, 200f) / 200f; // 0..1
        float feather = 0.04f + (0.22f * lineWidth01); // fraction around step boundaries

        float lineColorA01 = LineColor.Alpha / 255f;
        float lineColorR = LineColor.Red / 255f;
        float lineColorG = LineColor.Green / 255f;
        float lineColorB = LineColor.Blue / 255f;

        int width = source.Width;
        int height = source.Height;
        if (width <= 0 || height <= 0)
        {
            return source.Copy();
        }

        SKColor[] srcPixels = source.Pixels;
        SKColor[] dstPixels = new SKColor[srcPixels.Length];

        Parallel.For(0, height, y =>
        {
            int row = y * width;

            for (int x = 0; x < width; x++)
            {
                int idx = row + x;
                SKColor src = srcPixels[idx];

                float lum01 = ((0.2126f * src.Red) + (0.7152f * src.Green) + (0.0722f * src.Blue)) / 255f;
                lum01 = ProceduralEffectHelper.Clamp01(lum01 + thresholdBias);

                float scaled = lum01 * levels;
                float qIndex = MathF.Floor(scaled);
                float frac = scaled - qIndex; // 0..1 inside the current quantization bucket

                // Quantized luminance for the background.
                float qLum = (qIndex + 0.5f) / levels;
                qLum = ProceduralEffectHelper.Clamp01(qLum);

                // Distance to the nearest bucket boundary (0 at boundary, 0.5 at center).
                float distToBoundary = MathF.Min(frac, 1f - frac);

                // Line mask appears around bucket boundaries.
                float t = ProceduralEffectHelper.SmoothStep(0f, feather, distToBoundary);
                float lineMask = 1f - t;
                lineMask = lineMask * lineMask;

                if (Invert)
                {
                    lineMask = 1f - lineMask;
                }

                float bgLum = ProceduralEffectHelper.Lerp(lum01, qLum, backgroundStrength01);
                bgLum = ProceduralEffectHelper.Clamp01(bgLum);

                float baseR = bgLum;
                float baseG = bgLum;
                float baseB = bgLum;

                float lineMix = lineMask * lineStrength01 * lineColorA01;

                float outR = ProceduralEffectHelper.Lerp(baseR, lineColorR, lineMix);
                float outG = ProceduralEffectHelper.Lerp(baseG, lineColorG, lineMix);
                float outB = ProceduralEffectHelper.Lerp(baseB, lineColorB, lineMix);

                dstPixels[idx] = new SKColor(
                    ProceduralEffectHelper.ClampToByte(outR * 255f),
                    ProceduralEffectHelper.ClampToByte(outG * 255f),
                    ProceduralEffectHelper.ClampToByte(outB * 255f),
                    src.Alpha);
            }
        });

        return new SKBitmap(width, height, source.ColorType, source.AlphaType)
        {
            Pixels = dstPixels
        };
    }
}

