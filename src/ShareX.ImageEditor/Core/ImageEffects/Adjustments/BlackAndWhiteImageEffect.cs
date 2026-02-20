using SkiaSharp;


namespace ShareX.ImageEditor.ImageEffects.Adjustments;

public class BlackAndWhiteImageEffect : ImageEffect
{
    public override string Name => "Black and White";
    public override string IconKey => "IconAdjust";
    public override SKBitmap Apply(SKBitmap source)
    {
        // Optimized Skia hardware-accelerated threshold
        byte[] alpha = new byte[256];
        byte[] rgb = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            alpha[i] = (byte)i;
            rgb[i] = (byte)(i > 127 ? 255 : 0);
        }

        using var tableFilter = SKColorFilter.CreateTable(alpha, rgb, rgb, rgb);
        
        using var grayFilter = SKColorFilter.CreateColorMatrix(new float[] {
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0,       0,       0,       1, 0
        });

        using var combinedFilter = SKColorFilter.CreateCompose(tableFilter, grayFilter);
        return ApplyColorFilter(source, combinedFilter);
    }
}

