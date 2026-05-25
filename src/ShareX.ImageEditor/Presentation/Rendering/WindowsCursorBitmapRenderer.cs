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

using Avalonia.Media.Imaging;
using ShareX.ImageEditor.Core.Annotations;
using SkiaSharp;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ShareX.ImageEditor.Presentation.Rendering
{
    internal static class WindowsCursorBitmapRenderer
    {
        private const uint BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;
        private const int DI_NORMAL = 0x0003;

        private static readonly object SyncRoot = new();
        private static readonly Dictionary<CursorType, SKBitmap?> AnnotationBitmapCache = new();
        private static readonly Dictionary<(CursorType CursorType, int PreviewSize), Bitmap?> PreviewBitmapCache = new();
        private static readonly Lazy<Type?> WinFormsCursorsType = new(TryGetWinFormsCursorsType);
        private static readonly IReadOnlyDictionary<CursorType, int> FallbackCursorIds = new Dictionary<CursorType, int>
        {
            [CursorType.AppStarting] = 32650,
            [CursorType.Arrow] = 32512,
            [CursorType.Cross] = 32515,
            [CursorType.Default] = 32512,
            [CursorType.Hand] = 32649,
            [CursorType.Help] = 32651,
            [CursorType.HSplit] = 32652,
            [CursorType.IBeam] = 32513,
            [CursorType.No] = 32648,
            [CursorType.NoMove2D] = 32654,
            [CursorType.NoMoveHoriz] = 32652,
            [CursorType.NoMoveVert] = 32653,
            [CursorType.PanEast] = 32658,
            [CursorType.PanNE] = 32660,
            [CursorType.PanNorth] = 32655,
            [CursorType.PanNW] = 32659,
            [CursorType.PanSE] = 32662,
            [CursorType.PanSouth] = 32656,
            [CursorType.PanSW] = 32661,
            [CursorType.PanWest] = 32657,
            [CursorType.SizeAll] = 32646,
            [CursorType.SizeNESW] = 32643,
            [CursorType.SizeNS] = 32645,
            [CursorType.SizeNWSE] = 32642,
            [CursorType.SizeWE] = 32644,
            [CursorType.UpArrow] = 32516,
            [CursorType.VSplit] = 32653,
            [CursorType.WaitCursor] = 32514
        };

        public static SKBitmap? CreateAnnotationBitmap(CursorType cursorType)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            lock (SyncRoot)
            {
                if (!AnnotationBitmapCache.TryGetValue(cursorType, out SKBitmap? cachedBitmap))
                {
                    cachedBitmap = RenderCursorBitmap(cursorType);
                    AnnotationBitmapCache[cursorType] = cachedBitmap;
                }

                return cachedBitmap?.Copy();
            }
        }

        public static Bitmap? GetPreviewBitmap(CursorType cursorType, int previewSize = 28)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            lock (SyncRoot)
            {
                var cacheKey = (cursorType, previewSize);

                if (!PreviewBitmapCache.TryGetValue(cacheKey, out Bitmap? previewBitmap))
                {
                    previewBitmap = CreatePreviewBitmap(cursorType, previewSize);
                    PreviewBitmapCache[cacheKey] = previewBitmap;
                }

                return previewBitmap;
            }
        }

        private static Bitmap? CreatePreviewBitmap(CursorType cursorType, int previewSize)
        {
            using SKBitmap? annotationBitmap = CreateAnnotationBitmap(cursorType);
            if (annotationBitmap == null)
            {
                return null;
            }

            int canvasSize = Math.Max(16, previewSize);

            using var previewBitmap = new SKBitmap(new SKImageInfo(canvasSize, canvasSize, SKColorType.Bgra8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(previewBitmap);
            using var paint = new SKPaint { IsAntialias = true };

            canvas.Clear(SKColors.Transparent);

            float maxWidth = Math.Max(1, canvasSize - 4f);
            float maxHeight = Math.Max(1, canvasSize - 4f);
            float scale = Math.Min(maxWidth / annotationBitmap.Width, maxHeight / annotationBitmap.Height);

            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0)
            {
                scale = 1f;
            }

            float drawWidth = annotationBitmap.Width * scale;
            float drawHeight = annotationBitmap.Height * scale;
            float left = (canvasSize - drawWidth) / 2f;
            float top = (canvasSize - drawHeight) / 2f;

            canvas.DrawBitmap(annotationBitmap, new SKRect(left, top, left + drawWidth, top + drawHeight), paint);
            canvas.Flush();

            return BitmapConversionHelpers.ToAvaloniBitmap(previewBitmap);
        }

        private static SKBitmap? RenderCursorBitmap(CursorType cursorType)
        {
            IntPtr cursorHandle = TryLoadWinFormsCursorHandle(cursorType);

            if (cursorHandle == IntPtr.Zero)
            {
                cursorHandle = TryLoadFallbackCursorHandle(cursorType);
            }

            if (cursorHandle == IntPtr.Zero)
            {
                return null;
            }

            if (!TryGetCursorSize(cursorHandle, out int width, out int height))
            {
                width = 32;
                height = 32;
            }

            return DrawCursorToBitmap(cursorHandle, width, height);
        }

        private static bool TryGetCursorSize(IntPtr cursorHandle, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (!GetIconInfo(cursorHandle, out ICONINFO iconInfo))
            {
                return false;
            }

            try
            {
                IntPtr bitmapHandle = iconInfo.hbmColor != IntPtr.Zero
                    ? iconInfo.hbmColor
                    : iconInfo.hbmMask;

                if (bitmapHandle == IntPtr.Zero)
                {
                    return false;
                }

                if (GetObject(bitmapHandle, Marshal.SizeOf<BITMAP>(), out BITMAP bitmap) == 0)
                {
                    return false;
                }

                width = Math.Abs(bitmap.bmWidth);
                height = Math.Abs(bitmap.bmHeight);

                if (iconInfo.hbmColor == IntPtr.Zero && height > 1)
                {
                    height /= 2;
                }

                return width > 0 && height > 0;
            }
            finally
            {
                if (iconInfo.hbmColor != IntPtr.Zero)
                {
                    DeleteObject(iconInfo.hbmColor);
                }

                if (iconInfo.hbmMask != IntPtr.Zero)
                {
                    DeleteObject(iconInfo.hbmMask);
                }
            }
        }

        private static SKBitmap? DrawCursorToBitmap(IntPtr cursorHandle, int width, int height)
        {
            IntPtr memoryDc = CreateCompatibleDC(IntPtr.Zero);
            if (memoryDc == IntPtr.Zero)
            {
                return null;
            }

            BITMAPINFO bitmapInfo = new()
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB,
                    biSizeImage = (uint)(width * height * 4)
                }
            };

            IntPtr dibSection = CreateDIBSection(memoryDc, ref bitmapInfo, DIB_RGB_COLORS, out IntPtr pixelBuffer, IntPtr.Zero, 0);
            if (dibSection == IntPtr.Zero || pixelBuffer == IntPtr.Zero)
            {
                DeleteDC(memoryDc);
                return null;
            }

            IntPtr originalObject = SelectObject(memoryDc, dibSection);

            try
            {
                unsafe
                {
                    new Span<byte>((void*)pixelBuffer, width * height * 4).Clear();
                }

                if (!DrawIconEx(memoryDc, 0, 0, cursorHandle, width, height, 0, IntPtr.Zero, DI_NORMAL))
                {
                    return null;
                }

                var imageInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var skBitmap = new SKBitmap(imageInfo);
                int totalBytes = height * skBitmap.RowBytes;

                unsafe
                {
                    Buffer.MemoryCopy((void*)pixelBuffer, (void*)skBitmap.GetPixels(), totalBytes, totalBytes);
                }

                return skBitmap;
            }
            finally
            {
                if (originalObject != IntPtr.Zero)
                {
                    SelectObject(memoryDc, originalObject);
                }

                DeleteObject(dibSection);
                DeleteDC(memoryDc);
            }
        }

        private static IntPtr TryLoadWinFormsCursorHandle(CursorType cursorType)
        {
            Type? cursorsType = WinFormsCursorsType.Value;
            if (cursorsType == null)
            {
                return IntPtr.Zero;
            }

            string? propertyName = Enum.GetName(cursorType);
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return IntPtr.Zero;
            }

            try
            {
                object? cursor = cursorsType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (cursor == null)
                {
                    return IntPtr.Zero;
                }

                object? handleValue = cursor.GetType().GetProperty("Handle", BindingFlags.Public | BindingFlags.Instance)?.GetValue(cursor);
                return handleValue is IntPtr handle ? handle : IntPtr.Zero;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static Type? TryGetWinFormsCursorsType()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                return Type.GetType("System.Windows.Forms.Cursors, System.Windows.Forms", throwOnError: false);
            }
            catch
            {
                return null;
            }
        }

        private static IntPtr TryLoadFallbackCursorHandle(CursorType cursorType)
        {
            return FallbackCursorIds.TryGetValue(cursorType, out int cursorId)
                ? LoadCursor(IntPtr.Zero, (IntPtr)cursorId)
                : IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool fIcon;
            public uint xHotspot;
            public uint yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr hdc);
    }
}