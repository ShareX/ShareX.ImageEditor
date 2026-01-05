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

namespace ShareX.Editor.Annotations;

/// <summary>
/// Text annotation
/// </summary>
public class TextAnnotation : Annotation
{
    /// <summary>
    /// Text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Font size in pixels
    /// </summary>
    public float FontSize { get; set; } = 48;

    /// <summary>
    /// Font family
    /// </summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>
    /// Bold style
    /// </summary>
    public bool IsBold { get; set; }

    /// <summary>
    /// Italic style
    /// </summary>
    public bool IsItalic { get; set; }

    public TextAnnotation()
    {
        ToolType = EditorTool.Text;
    }

    public override void Render(SKCanvas canvas)
    {
        if (string.IsNullOrEmpty(Text)) return;

        using var paint = new SKPaint
        {
            Color = ParseColor(StrokeColor),
            TextSize = FontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(
                FontFamily,
                IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)
        };

        // Treat StartPoint as the top-left of the text box with a small padding like the Avalonia TextBox.
        const float padding = 4f;
        var metrics = paint.FontMetrics;
        float baseline = StartPoint.Y + padding - metrics.Ascent; // ascent is negative

        canvas.DrawText(Text, StartPoint.X + padding, baseline, paint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 5)
    {
        if (string.IsNullOrEmpty(Text)) return false;

        var textBounds = GetBounds();
        var inflatedBounds = SKRect.Inflate(textBounds, tolerance, tolerance);
        return inflatedBounds.Contains(point);
    }

    public override SKRect GetBounds()
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new SKRect(StartPoint.X, StartPoint.Y, StartPoint.X + 10, StartPoint.Y + 10);
        }

        using var paint = new SKPaint
        {
            TextSize = FontSize,
            Typeface = SKTypeface.FromFamilyName(FontFamily)
        };

        var textWidth = paint.MeasureText(Text);
        var metrics = paint.FontMetrics;
        var textHeight = metrics.Descent - metrics.Ascent;

        const float padding = 4f;
        return new SKRect(
            StartPoint.X,
            StartPoint.Y,
            StartPoint.X + textWidth + padding * 2,
            StartPoint.Y + textHeight + padding * 2);
    }
}
