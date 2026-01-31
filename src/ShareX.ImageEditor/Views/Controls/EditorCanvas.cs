#region License Information (GPL v3)

/*
    ShareX.ImageEditor - The UI-agnostic Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team

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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SkiaSharp;

namespace ShareX.ImageEditor.Views.Controls;

/// <summary>
/// Avalonia control that hosts EditorCore and renders via SkiaSharp.
/// </summary>
public class EditorCanvas : Control
{
    private readonly EditorCore _editor = new();
    private SKBitmap? _renderTarget;

    public EditorCore Editor => _editor;

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<EditorCanvas, double>(nameof(Zoom), 1.0);

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    static EditorCanvas()
    {
        AffectsRender<EditorCanvas>(ZoomProperty);
    }

    public EditorCanvas()
    {
        ClipToBounds = true;
        Focusable = true;

        _editor.InvalidateRequested += InvalidateVisual;
    }

    public void LoadImage(SKBitmap bitmap)
    {
        _editor.LoadImage(bitmap);
        Width = bitmap.Width;
        Height = bitmap.Height;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_editor.SourceImage == null) return;

        int width = _editor.SourceImage.Width;
        int height = _editor.SourceImage.Height;

        if (width <= 0 || height <= 0) return;

        if (_renderTarget == null || _renderTarget.Width != width || _renderTarget.Height != height)
        {
            _renderTarget?.Dispose();
            _renderTarget = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        }

        using (var canvas = new SKCanvas(_renderTarget))
        {
            _editor.Render(canvas);
        }

        var writeableBitmap = new Avalonia.Media.Imaging.WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Premul);

        using (var frameBuffer = writeableBitmap.Lock())
        {
            var srcPtr = _renderTarget.GetPixels();
            var dstPtr = frameBuffer.Address;
            var size = width * height * 4;

            var buffer = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy(srcPtr, buffer, 0, size);
            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, dstPtr, size);
        }

        var destRect = new Rect(0, 0, width * Zoom, height * Zoom);
        context.DrawImage(writeableBitmap, new Rect(0, 0, width, height), destRect);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;

        var canvasPoint = new SKPoint((float)(point.X / Zoom), (float)(point.Y / Zoom));

        _editor.OnPointerPressed(canvasPoint, props.IsRightButtonPressed);

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            !e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        var point = e.GetPosition(this);
        var canvasPoint = new SKPoint((float)(point.X / Zoom), (float)(point.Y / Zoom));

        _editor.OnPointerMoved(canvasPoint);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var point = e.GetPosition(this);
        var canvasPoint = new SKPoint((float)(point.X / Zoom), (float)(point.Y / Zoom));

        _editor.OnPointerReleased(canvasPoint);

        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.Z:
                    _editor.Undo();
                    e.Handled = true;
                    break;
                case Key.Y:
                    _editor.Redo();
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.Delete:
                    _editor.DeleteSelected();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    _editor.Deselect();
                    e.Handled = true;
                    break;
            }
        }
    }

    public SKBitmap? GetSnapshot() => _editor.GetSnapshot();
}
