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

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Number annotation - auto-incrementing numbered circle markers
/// </summary>
public class NumberAnnotation : Annotation
{
    /// <summary>
    /// Number to display (typically auto-incremented)
    /// </summary>
    public int Number { get; set; } = 1;

    /// <summary>
    /// Font size for the number
    /// </summary>
    public float FontSize { get; set; } = 32;

    /// <summary>
    /// Circle radius
    /// </summary>
    public float Radius { get; set; } = 25;

    public NumberAnnotation()
    {
        ToolType = EditorTool.Number;
    }

    /// <summary>
    /// Creates the Avalonia visual for this annotation (Grid with Ellipse and TextBlock)
    /// </summary>
    public Control CreateVisual()
    {
        var brush = new SolidColorBrush(Color.Parse(StrokeColor));
        var grid = new Grid
        {
            Width = Radius * 2,
            Height = Radius * 2,
            Tag = this
        };

        var bg = new Avalonia.Controls.Shapes.Ellipse
        {
            Fill = brush,
            Stroke = Brushes.White,
            StrokeThickness = 2
        };

        var numText = new TextBlock
        {
            Text = Number.ToString(),
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.Bold,
            FontSize = FontSize / 2 // Scale font to fit in circle
        };

        grid.Children.Add(bg);
        grid.Children.Add(numText);

        return grid;
    }

    public override void Render(SKCanvas canvas)
    {
        var center = StartPoint;

        // Draw filled circle
        using var fillPaint = CreateFillPaint();
        canvas.DrawCircle(center, Radius, fillPaint);

        // Draw circle border
        using var strokePaint = CreateStrokePaint();
        canvas.DrawCircle(center, Radius, strokePaint);

        // Draw number text
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = FontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        var text = Number.ToString();

        // Center text vertically
        var textBounds = new SKRect();
        textPaint.MeasureText(text, ref textBounds);
        var textY = center.Y + textBounds.Height / 2 - textBounds.Bottom;

        canvas.DrawText(text, center.X, textY, textPaint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 5)
    {
        var dx = point.X - StartPoint.X;
        var dy = point.Y - StartPoint.Y;
        var distance = (float)Math.Sqrt(dx * dx + dy * dy);
        return distance <= (Radius + tolerance);
    }

    public override SKRect GetBounds()
    {
        return new SKRect(
            StartPoint.X - Radius,
            StartPoint.Y - Radius,
            StartPoint.X + Radius,
            StartPoint.Y + Radius);
    }
}
