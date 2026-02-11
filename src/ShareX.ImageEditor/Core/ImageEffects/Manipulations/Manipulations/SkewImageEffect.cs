using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Manipulations;

public class SkewImageEffect : ImageEffect
{
    public override string Name => "Skew";
    public override ImageEffectCategory Category => ImageEffectCategory.Manipulations;
    public override bool HasParameters => true;

    public float Horizontally { get; set; } = 0;
    public float Vertically { get; set; } = 0;

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (Horizontally == 0 && Vertically == 0) return source.Copy();

        // Calculate new dimensions
        float absH = Math.Abs(Horizontally);
        float absV = Math.Abs(Vertically);

        int newWidth = source.Width + (int)(source.Height * absH / 90f);
        int newHeight = source.Height + (int)(source.Width * absV / 90f);

        SKBitmap result = new SKBitmap(newWidth, newHeight, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);

            // Create skew matrix
            SKMatrix matrix = SKMatrix.CreateSkew(
                (float)Math.Tan(Horizontally * Math.PI / 180),
                (float)Math.Tan(Vertically * Math.PI / 180)
            );

            // Adjust for position
            float offsetX = Horizontally < 0 ? source.Height * Math.Abs((float)Math.Tan(Horizontally * Math.PI / 180)) : 0;
            float offsetY = Vertically < 0 ? source.Width * Math.Abs((float)Math.Tan(Vertically * Math.PI / 180)) : 0;

            canvas.Translate(offsetX, offsetY);
            canvas.SetMatrix(canvas.TotalMatrix.PreConcat(matrix));
            canvas.DrawBitmap(source, 0, 0);
        }

        return result;
    }
}

