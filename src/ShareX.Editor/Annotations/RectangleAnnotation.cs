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
using Avalonia.Media;
using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Rectangle annotation
/// </summary>
public class RectangleAnnotation : Annotation
{
    public RectangleAnnotation()
    {
        ToolType = EditorTool.Rectangle;
    }

    /// <summary>
    /// Creates the Avalonia visual for this annotation
    /// </summary>
    public Control CreateVisual()
    {
        var brush = new SolidColorBrush(Color.Parse(StrokeColor));
        return new Avalonia.Controls.Shapes.Rectangle
        {
            Stroke = brush,
            StrokeThickness = StrokeWidth,
            Fill = Brushes.Transparent,
            Tag = this
        };
    }

    public override void Render(SKCanvas canvas)
    {
        var rect = GetBounds();
        using var paint = CreateStrokePaint();
        canvas.DrawRect(rect, paint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 5)
    {
        var rect = GetBounds();
        var expanded = SKRect.Inflate(rect, tolerance, tolerance);
        return expanded.Contains(point);
    }
}

