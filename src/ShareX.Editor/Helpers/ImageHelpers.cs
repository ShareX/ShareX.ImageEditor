#region License Information (GPL v3)

/*
    ShareX.Editor - The UI-agnostic Editor library for ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using SkiaSharp;

namespace ShareX.Editor.Helpers;

/// <summary>
/// Image manipulation utilities for the Editor library.
/// </summary>
public static class ImageHelpers
{
    public static SKBitmap Crop(SKBitmap bitmap, SKRectI rect)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));

        SKRectI bounded = new SKRectI(
            Math.Max(0, rect.Left),
            Math.Max(0, rect.Top),
            Math.Min(bitmap.Width, rect.Right),
            Math.Min(bitmap.Height, rect.Bottom));

        if (bounded.Width <= 0 || bounded.Height <= 0)
        {
            return new SKBitmap();
        }

        SKBitmap subset = new SKBitmap(bounded.Width, bounded.Height);
        return bitmap.ExtractSubset(subset, bounded) ? subset : new SKBitmap();
    }

    public static SKBitmap Crop(SKBitmap bitmap, int x, int y, int width, int height)
    {
        return Crop(bitmap, new SKRectI(x, y, x + width, y + height));
    }

    /// <summary>
    /// Cut out a horizontal or vertical section from the image and join the remaining parts
    /// </summary>
    /// <param name="bitmap">Source bitmap</param>
    /// <param name="startPos">Start position (X for vertical cut, Y for horizontal cut)</param>
    /// <param name="endPos">End position (X for vertical cut, Y for horizontal cut)</param>
    /// <param name="isVertical">True for vertical cut (left-right), false for horizontal cut (top-bottom)</param>
    /// <returns>New bitmap with the section cut out</returns>
    public static SKBitmap CutOut(SKBitmap bitmap, int startPos, int endPos, bool isVertical)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));

        // Ensure start is before end
        if (startPos > endPos)
        {
            (startPos, endPos) = (endPos, startPos);
        }

        if (isVertical)
        {
            // Vertical cut: remove columns from startPos to endPos
            int cutWidth = endPos - startPos;
            if (cutWidth <= 0 || cutWidth >= bitmap.Width)
            {
                return bitmap; // Nothing to cut or invalid range
            }

            int newWidth = bitmap.Width - cutWidth;
            int newHeight = bitmap.Height;

            SKBitmap result = new SKBitmap(newWidth, newHeight);
            using SKCanvas canvas = new SKCanvas(result);

            // Draw left part (0 to startPos)
            if (startPos > 0)
            {
                SKRect srcLeft = new SKRect(0, 0, startPos, bitmap.Height);
                SKRect dstLeft = new SKRect(0, 0, startPos, newHeight);
                canvas.DrawBitmap(bitmap, srcLeft, dstLeft);
            }

            // Draw right part (endPos to width), positioned after left part
            if (endPos < bitmap.Width)
            {
                SKRect srcRight = new SKRect(endPos, 0, bitmap.Width, bitmap.Height);
                SKRect dstRight = new SKRect(startPos, 0, newWidth, newHeight);
                canvas.DrawBitmap(bitmap, srcRight, dstRight);
            }

            return result;
        }
        else
        {
            // Horizontal cut: remove rows from startPos to endPos
            int cutHeight = endPos - startPos;
            if (cutHeight <= 0 || cutHeight >= bitmap.Height)
            {
                return bitmap; // Nothing to cut or invalid range
            }

            int newWidth = bitmap.Width;
            int newHeight = bitmap.Height - cutHeight;

            SKBitmap result = new SKBitmap(newWidth, newHeight);
            using SKCanvas canvas = new SKCanvas(result);

            // Draw top part (0 to startPos)
            if (startPos > 0)
            {
                SKRect srcTop = new SKRect(0, 0, bitmap.Width, startPos);
                SKRect dstTop = new SKRect(0, 0, newWidth, startPos);
                canvas.DrawBitmap(bitmap, srcTop, dstTop);
            }

            // Draw bottom part (endPos to height), positioned after top part
            if (endPos < bitmap.Height)
            {
                SKRect srcBottom = new SKRect(0, endPos, bitmap.Width, bitmap.Height);
                SKRect dstBottom = new SKRect(0, startPos, newWidth, newHeight);
                canvas.DrawBitmap(bitmap, srcBottom, dstBottom);
            }

            return result;
        }
    }

    public static void SaveBitmap(SKBitmap bitmap, string filePath, int quality = 100)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);

        SKEncodedImageFormat format = GetEncodedFormat(filePath);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, quality);
        using FileStream stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static SKEncodedImageFormat GetEncodedFormat(string filePath)
    {
        string extension = Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant() ?? string.Empty;
        return extension switch
        {
            "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
            "bmp" => SKEncodedImageFormat.Bmp,
            "gif" => SKEncodedImageFormat.Gif,
            "webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };
    }

    /// <summary>
    /// Resize a bitmap to the specified dimensions with optional aspect ratio preservation
    /// </summary>
    /// <param name="source">Source bitmap to resize</param>
    /// <param name="width">Target width</param>
    /// <param name="height">Target height</param>
    /// <param name="maintainAspectRatio">If true, scales proportionally to fit within width/height</param>
    /// <param name="quality">Filter quality for scaling</param>
    /// <returns>New resized bitmap</returns>
    public static SKBitmap Resize(SKBitmap source, int width, int height, bool maintainAspectRatio = false, SKFilterQuality quality = SKFilterQuality.High)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (width <= 0) throw new ArgumentException("Width must be greater than 0", nameof(width));
        if (height <= 0) throw new ArgumentException("Height must be greater than 0", nameof(height));

        int targetWidth = width;
        int targetHeight = height;

        if (maintainAspectRatio)
        {
            double sourceAspect = (double)source.Width / source.Height;
            double targetAspect = (double)width / height;

            if (sourceAspect > targetAspect)
            {
                // Source is wider, fit to width
                targetHeight = (int)Math.Round(width / sourceAspect);
            }
            else
            {
                // Source is taller, fit to height
                targetWidth = (int)Math.Round(height * sourceAspect);
            }
        }

        SKImageInfo info = new SKImageInfo(targetWidth, targetHeight, source.ColorType, source.AlphaType, source.ColorSpace);
        return source.Resize(info, quality);
    }

    /// <summary>
    /// Rotate bitmap 90 degrees clockwise
    /// </summary>
    public static SKBitmap Rotate90Clockwise(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Height, source.Width, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(result.Width, 0);
            canvas.RotateDegrees(90);
            canvas.DrawBitmap(source, 0, 0);
        }
        return result;
    }

    /// <summary>
    /// Rotate bitmap 90 degrees counter-clockwise
    /// </summary>
    public static SKBitmap Rotate90CounterClockwise(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Height, source.Width, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(0, result.Height);
            canvas.RotateDegrees(-90);
            canvas.DrawBitmap(source, 0, 0);
        }
        return result;
    }

    /// <summary>
    /// Rotate bitmap 180 degrees
    /// </summary>
    public static SKBitmap Rotate180(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(result.Width, result.Height);
            canvas.RotateDegrees(180);
            canvas.DrawBitmap(source, 0, 0);
        }
        return result;
    }

    /// <summary>
    /// Flip bitmap horizontally (mirror left-to-right)
    /// </summary>
    public static SKBitmap FlipHorizontal(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(-1, 1, source.Width / 2f, source.Height / 2f);
            canvas.DrawBitmap(source, 0, 0);
        }
        return result;
    }

    /// <summary>
    /// Flip bitmap vertically (mirror top-to-bottom)
    /// </summary>
    public static SKBitmap FlipVertical(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(1, -1, source.Width / 2f, source.Height / 2f);
            canvas.DrawBitmap(source, 0, 0);
        }
        return result;
    }

    /// <summary>
    /// Resize canvas by adding padding around the image
    /// </summary>
    /// <param name="source">Source bitmap</param>
    /// <param name="left">Left padding in pixels</param>
    /// <param name="top">Top padding in pixels</param>
    /// <param name="right">Right padding in pixels</param>
    /// <param name="bottom">Bottom padding in pixels</param>
    /// <param name="backgroundColor">Background color for the padding</param>
    /// <returns>New bitmap with padding</returns>
    public static SKBitmap ResizeCanvas(SKBitmap source, int left, int top, int right, int bottom, SKColor backgroundColor)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        int newWidth = source.Width + left + right;
        int newHeight = source.Height + top + bottom;

        if (newWidth <= 0 || newHeight <= 0)
        {
            throw new ArgumentException("Canvas size after padding must be greater than 0");
        }

        SKBitmap result = new SKBitmap(newWidth, newHeight, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(backgroundColor);
            canvas.DrawBitmap(source, left, top);
        }
        return result;
    }

    /// <summary>
    /// Auto-crops the image by removing edges that match the specified color within tolerance
    /// </summary>
    /// <param name="source">Source bitmap</param>
    /// <param name="color">Color to match for cropping</param>
    /// <param name="tolerance">Color tolerance (0-255)</param>
    /// <returns>Cropped bitmap</returns>
    public static SKBitmap AutoCrop(SKBitmap source, SKColor color, int tolerance = 0)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        int width = source.Width;
        int height = source.Height;

        int minX = width, minY = height, maxX = 0, maxY = 0;
        bool hasContent = false;

        // Scan all pixels to find content bounds
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                SKColor pixel = source.GetPixel(x, y);
                if (!ColorsMatch(pixel, color, tolerance))
                {
                    hasContent = true;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (!hasContent)
        {
            // No content found, return 1x1 transparent bitmap
            return new SKBitmap(1, 1, source.ColorType, source.AlphaType);
        }

        int cropWidth = maxX - minX + 1;
        int cropHeight = maxY - minY + 1;

        return Crop(source, minX, minY, cropWidth, cropHeight);
    }

    private static bool ColorsMatch(SKColor c1, SKColor c2, int tolerance)
    {
        return Math.Abs(c1.Red - c2.Red) <= tolerance &&
               Math.Abs(c1.Green - c2.Green) <= tolerance &&
               Math.Abs(c1.Blue - c2.Blue) <= tolerance &&
               Math.Abs(c1.Alpha - c2.Alpha) <= tolerance;
    }

    // --- Color Adjustments ---

    public static SKBitmap ApplyBrightness(SKBitmap source, float amount)
    {
        // amount is -100 to 100
        // Matrix logic:
        // [ 1 0 0 0 amount ]
        // [ 0 1 0 0 amount ]
        // [ 0 0 1 0 amount ]
        // [ 0 0 0 1 0 ]

        float value = amount / 100f; // Scale to -1..1 range
        float[] matrix = {
            1, 0, 0, 0, value,
            0, 1, 0, 0, value,
            0, 0, 1, 0, value,
            0, 0, 0, 1, 0
        };

        return ApplyColorMatrix(source, matrix);
    }

    public static SKBitmap ApplyContrast(SKBitmap source, float amount)
    {
        // amount is -100 to 100
        // Scale factor: (100 + amount) / 100  (simplistic)
        // or more standard formula: s = (amount + 100) / 100; s*s ? 
        // Let's use: scale = (100 + amount) / 100
        // shift = 128 * (1 - scale)

        float scale = (100f + amount) / 100f;
        scale = scale * scale; // Curve it a bit for better feel

        float shift = 0.5f * (1f - scale);

        float[] matrix = {
            scale, 0, 0, 0, shift,
            0, scale, 0, 0, shift,
            0, 0, scale, 0, shift,
            0, 0, 0, 1, 0
        };

        return ApplyColorMatrix(source, matrix);
    }

    public static SKBitmap ApplyHue(SKBitmap source, float amount)
    {
        // amount is -180 to 180 degrees
        // Using RotateColor method logic or similar implementation
        // There isn't a direct 5x4 matrix for Hue easily without complex calculation.
        // But SkiaSharp has CreateLighting or CreateBlend, but CreateHighContrast doesn't do Hue.
        // Ideally we use SKColorFilter.CreateColorMatrix.
        // Calculating hue rotation matrix is complex. 
        // Simplification: Iterate pixels or use optimized math.
        // For performance, let's use a pixel loop for Hue if ColorFilter isn't easy, 
        // BUT SkiaSharp has no built-in Hue filter.
        // Users expect "Hue cycle".

        // Actually, let's defer detailed matrix math to a separate helper or use a pixel loop for now
        // if we want to be absolutely sure of correctness, though slower.
        // Given this is an Editor, performance is key. 
        // Let's try to approximate or use a known matrix algorithm for Hue.

        // Using pixel manipulation for Hue to ensure accuracy
        return ApplyPixelOperation(source, (color) =>
        {
            color.ToHsl(out float h, out float s, out float l);
            h = (h + amount) % 360;
            if (h < 0) h += 360;
            return SKColor.FromHsl(h, s, l, color.Alpha);
        });
    }

    public static SKBitmap ApplySaturation(SKBitmap source, float amount)
    {
        // amount is -100 to 100
        // -100 = grayscale, 0 = normal, 100 = 2x saturation

        float x = 1f + (amount / 100f);
        float lumR = 0.3086f;
        float lumG = 0.6094f;
        float lumB = 0.0820f;

        float invSat = 1f - x;

        float r = (invSat * lumR);
        float g = (invSat * lumG);
        float b = (invSat * lumB);

        float[] matrix = {
            r + x, g,     b,     0, 0,
            r,     g + x, b,     0, 0,
            r,     g,     b + x, 0, 0,
            0,     0,     0,     1, 0
        };

        return ApplyColorMatrix(source, matrix);
    }

    public static SKBitmap ApplyGamma(SKBitmap source, float amount)
    {
        // Gamma is non-linear, so matrix won't work perfectly.
        // Use SkiaSharp TableColorFilter? Or pixel loop.
        // SKColorFilter.CreateTable is suitable.

        byte[] table = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            float val = i / 255f;
            float corrected = (float)Math.Pow(val, 1.0 / amount);
            table[i] = (byte)(Math.Max(0, Math.Min(1, corrected)) * 255);
        }

        // Apply to RGB, keep Alpha
        using var filter = SKColorFilter.CreateTable(null, table, table, table);
        return ApplyColorFilter(source, filter);
    }

    public static SKBitmap ApplyAlpha(SKBitmap source, float amount)
    {
        // amount 0 to 100 (percentage)
        float a = amount / 100f;

        float[] matrix = {
            1, 0, 0, 0, 0,
            0, 1, 0, 0, 0,
            0, 0, 1, 0, 0,
            0, 0, 0, a, 0
        };

        return ApplyColorMatrix(source, matrix);
    }

    // --- Filters ---

    public static SKBitmap ApplyInvert(SKBitmap source)
    {
        float[] matrix = {
            -1,  0,  0, 0, 1,
             0, -1,  0, 0, 1,
             0,  0, -1, 0, 1,
             0,  0,  0, 1, 0
        };
        return ApplyColorMatrix(source, matrix);
    }

    public static SKBitmap ApplyGrayscale(SKBitmap source)
    {
        // BT.709
        float[] matrix = {
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0,       0,       0,       1, 0
        };
        return ApplyColorMatrix(source, matrix);
    }

    public static SKBitmap ApplyBlackAndWhite(SKBitmap source)
    {
        // Simple thresholding
        return ApplyPixelOperation(source, (color) =>
        {
            float lum = offsetLum(color);
            return lum > 127 ? SKColors.White : SKColors.Black;
        });

        static float offsetLum(SKColor c) => 0.2126f * c.Red + 0.7152f * c.Green + 0.0722f * c.Blue;
    }

    public static SKBitmap ApplySepia(SKBitmap source)
    {
        float[] matrix = {
            0.393f, 0.769f, 0.189f, 0, 0,
            0.349f, 0.686f, 0.168f, 0, 0,
            0.272f, 0.534f, 0.131f, 0, 0,
            0,      0,      0,      1, 0
        };
        return ApplyColorMatrix(source, matrix);
    }

    public static SKBitmap ApplyPolaroid(SKBitmap source)
    {
        // Slight shift to orange/yellow + reduced saturation + contrast
        // Simplified matrix roughly mimicking polaroid
        float[] matrix = {
            1.438f, -0.062f, -0.062f, 0, 0,
            -0.122f, 1.378f, -0.122f, 0, 0,
            -0.016f, -0.016f, 1.483f, 0, 0,
            0,       0,       0,      1, 0
        };
        return ApplyColorMatrix(source, matrix);
    }

    public static SKBitmap ApplyColorize(SKBitmap source, SKColor color, float strength)
    {
        // strength 0 to 100
        if (strength <= 0) return source.Copy();
        if (strength > 100) strength = 100;

        using var paint = new SKPaint();

        // 1. Grayscale matrix
        var grayscaleMatrix = new float[] {
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0,       0,       0,       1, 0
        };
        using var grayscale = SKColorFilter.CreateColorMatrix(grayscaleMatrix);

        // 2. Tint using BlendMode (Modulate or Color?)
        // Modulate multiplies, which works well if we start with grayscale (white becomes color, black stays black).
        // SKBlendMode.Color is better for preserving luminosity but changing hue/sat.
        // Let's use Modulate for "Colorize" behavior typically expected (tinting everything).
        using var tint = SKColorFilter.CreateBlendMode(color, SKBlendMode.Modulate);

        // Compose
        using var composed = SKColorFilter.CreateCompose(tint, grayscale);

        // If strength < 100, we blend with original
        paint.ColorFilter = composed;
        
        // Create result
        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);
            
            if (strength >= 100)
            {
                // Draw only modified
                canvas.DrawBitmap(source, 0, 0, paint);
            }
            else
            {
                // Draw original
                canvas.DrawBitmap(source, 0, 0);
                
                // Draw modified on top with opacity
                paint.Color = new SKColor(255, 255, 255, (byte)(255 * (strength / 100f)));
                canvas.DrawBitmap(source, 0, 0, paint);
            }
        }
        return result;
    }

    public enum SelectiveColorRange
    {
        Reds,
        Yellows,
        Greens,
        Cyans,
        Blues,
        Magentas,
        Whites,
        Neutrals,
        Blacks
    }

    public static SKBitmap ApplySelectiveColor(SKBitmap source, SelectiveColorRange range, float hueShift, float satShift, float lightShift)
    {
        // Hue shift: -180 to 180
        // Sat/Light shift: -100 to 100

        return ApplyPixelOperation(source, (c) =>
        {
            c.ToHsl(out float h, out float s, out float l); // h:0-360, s:0-100, l:0-100 usually in Skia extensions or 0-1?
            // SKColor.ToHsl returns h=0..360, s=0..100, l=0..100.
            
            bool match = false;

            switch (range)
            {
                case SelectiveColorRange.Reds:     match = (h >= 330 || h <= 30); break;
                case SelectiveColorRange.Yellows:  match = (h >= 30 && h < 90); break;
                case SelectiveColorRange.Greens:   match = (h >= 90 && h < 150); break;
                case SelectiveColorRange.Cyans:    match = (h >= 150 && h < 210); break;
                case SelectiveColorRange.Blues:    match = (h >= 210 && h < 270); break;
                case SelectiveColorRange.Magentas: match = (h >= 270 && h < 330); break;
                case SelectiveColorRange.Whites:   match = (l > 80); break; // Simplified
                case SelectiveColorRange.Blacks:   match = (l < 20); break; // Simplified
                case SelectiveColorRange.Neutrals: match = (s < 10 && l >= 20 && l <= 80); break; // Simplified
            }

            if (match)
            {
                h = (h + hueShift) % 360;
                if (h < 0) h += 360;

                s = Math.Clamp(s + s * (satShift / 100f), 0, 100); // Scale relative? or absolute add? User usually expects add.
                // Let's effectively add percentage of S. 
                // Or simplistic: S = S + shift.
                // Better: S = S + shift (clamped 0-100).
                s = Math.Clamp(s + satShift, 0, 100);

                l = Math.Clamp(l + lightShift, 0, 100);
            }

            return SKColor.FromHsl(h, s, l, c.Alpha);
        });
    }

    public static SKBitmap ApplyReplaceColor(SKBitmap source, SKColor targetColor, SKColor replaceColor, float tolerance)
    {
        // tolerance 0-100 usually. Convert to byte range 0-255.
        int tol = (int)(tolerance * 2.55f);

        return ApplyPixelOperation(source, (c) =>
        {
            if (ColorsMatch(c, targetColor, tol))
            {
                // Apply replacement logic
                // Should we preserve Alpha of original pixel or use replacement alpha?
                // Usually replace usage implies full replacement, but maybe preserve alpha if target was transparent?
                // Let's use replaceColor directly but respect original alpha if needed?
                // Standard bucket fill logic: replace.
                
                // Optional: Blend edges? For now, hard replace.
                return replaceColor;
            }
            return c;
        });
    }

    // --- Helpers ---

    private static SKBitmap ApplyColorMatrix(SKBitmap source, float[] matrix)
    {
        using var filter = SKColorFilter.CreateColorMatrix(matrix);
        return ApplyColorFilter(source, filter);
    }

    private static SKBitmap ApplyColorFilter(SKBitmap source, SKColorFilter filter)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);
            using (SKPaint paint = new SKPaint())
            {
                paint.ColorFilter = filter;
                canvas.DrawBitmap(source, 0, 0, paint);
            }
        }
        return result;
    }

    public static SKBitmap ApplySelectiveColorAdvanced(SKBitmap source, Dictionary<SelectiveColorRange, (float h, float s, float l)> adjustments)
    {
        return ApplyPixelOperation(source, (c) =>
        {
            c.ToHsl(out float h, out float s, out float l); 
            
            // Determine range preference: Whites/Blacks/Neutrals first
            SelectiveColorRange? range = null;

            // Simplified HSL range detection
            if (l > 80) range = SelectiveColorRange.Whites;
            else if (l < 20) range = SelectiveColorRange.Blacks;
            else if (s < 10) range = SelectiveColorRange.Neutrals;
            else
            {
                // Hue based
                float hDeg = h;
                if (hDeg >= 330 || hDeg <= 30) range = SelectiveColorRange.Reds;
                else if (hDeg >= 30 && hDeg < 90) range = SelectiveColorRange.Yellows;
                else if (hDeg >= 90 && hDeg < 150) range = SelectiveColorRange.Greens;
                else if (hDeg >= 150 && hDeg < 210) range = SelectiveColorRange.Cyans;
                else if (hDeg >= 210 && hDeg < 270) range = SelectiveColorRange.Blues;
                else if (hDeg >= 270 && hDeg < 330) range = SelectiveColorRange.Magentas;
            }

            if (range.HasValue && adjustments.TryGetValue(range.Value, out var adj))
            {
                if (adj.h != 0 || adj.s != 0 || adj.l != 0)
                {
                    h = (h + adj.h) % 360;
                    if (h < 0) h += 360;

                    s = Math.Clamp(s + adj.s, 0, 100);
                    l = Math.Clamp(l + adj.l, 0, 100);
                    
                    return SKColor.FromHsl(h, s, l, c.Alpha);
                }
            }

            return c;
        });
    }

    private static SKBitmap ApplyPixelOperation(SKBitmap source, Func<SKColor, SKColor> operation)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);

        // Lock pixels for direct access (fast)
        IntPtr srcPtr = source.GetPixels();
        IntPtr dstPtr = result.GetPixels();

        int count = source.Width * source.Height;

        // Iterate assuming 32-bit (8888) format which is standard for SKBitmap usually
        // Unsafe block would be faster, but let's stick to safe GetPixel/SetPixel if possible 
        // or loop over buffer.
        // For simplicity and safety in this context:

        for (int x = 0; x < source.Width; x++)
        {
            for (int y = 0; y < source.Height; y++)
            {
                SKColor original = source.GetPixel(x, y);
                SKColor modified = operation(original);
                result.SetPixel(x, y, modified);
            }
        }

        return result;
    }
}
