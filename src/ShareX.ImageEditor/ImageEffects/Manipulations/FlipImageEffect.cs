using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Manipulations;

public class FlipImageEffect : ImageEffect
{
    public enum FlipDirection { Horizontal, Vertical }

    private readonly FlipDirection _direction;
    private readonly string _name;

    public override string Name => _name;
    public override ImageEffectCategory Category => ImageEffectCategory.Manipulations;

    public FlipImageEffect(FlipDirection direction, string name)
    {
        _direction = direction;
        _name = name;
    }

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);
            if (_direction == FlipDirection.Horizontal)
            {
                canvas.Scale(-1, 1, source.Width / 2f, source.Height / 2f);
            }
            else
            {
                canvas.Scale(1, -1, source.Width / 2f, source.Height / 2f);
            }
            canvas.DrawBitmap(source, 0, 0);
        }
        return result;
    }

    public static FlipImageEffect Horizontal => new FlipImageEffect(FlipDirection.Horizontal, "Flip horizontal");
    public static FlipImageEffect Vertical => new FlipImageEffect(FlipDirection.Vertical, "Flip vertical");
}

