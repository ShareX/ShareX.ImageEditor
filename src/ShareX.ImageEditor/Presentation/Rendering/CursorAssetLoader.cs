using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Buffers.Binary;

namespace ShareX.ImageEditor.Presentation.Rendering
{
    internal static class CursorAssetLoader
    {
        private static readonly Uri CrosshairCursorUri = new("avares://ShareX.ImageEditor/Assets/Crosshair.cur");
        private static readonly Cursor FallbackCrossCursor = new(StandardCursorType.Cross);
        private static readonly Lazy<LoadedCursor?> CrosshairCursor = new(TryLoadCrosshairCursor);

        public static Cursor GetCrosshairCursor()
        {
            return CrosshairCursor.Value?.Cursor ?? FallbackCrossCursor;
        }

        private static LoadedCursor? TryLoadCrosshairCursor()
        {
            try
            {
                using Stream cursorStream = AssetLoader.Open(CrosshairCursorUri);
                return LoadCursor(cursorStream);
            }
            catch
            {
                return null;
            }
        }

        private static LoadedCursor LoadCursor(Stream cursorStream)
        {
            using var memoryStream = new MemoryStream();
            cursorStream.CopyTo(memoryStream);

            byte[] data = memoryStream.ToArray();
            CursorDirectoryEntry entry = ReadCursorDirectoryEntry(data);
            PixelPoint hotSpot = new(entry.HotspotX, entry.HotspotY);

            Bitmap bitmap = IsPng(data, entry.ImageOffset)
                ? LoadPngBitmap(data, entry.ImageOffset, entry.ImageLength)
                : LoadBitmapInfoCursor(data, entry.ImageOffset);

            return new LoadedCursor(bitmap, new Cursor(bitmap, hotSpot));
        }

        private static CursorDirectoryEntry ReadCursorDirectoryEntry(byte[] data)
        {
            if (data.Length < 22)
            {
                throw new InvalidDataException("Cursor asset is too small.");
            }

            ushort type = ReadUInt16(data, 2);
            ushort count = ReadUInt16(data, 4);

            if (type != 2)
            {
                throw new NotSupportedException("Only .cur cursor assets are supported.");
            }

            if (count == 0)
            {
                throw new InvalidDataException("Cursor asset does not contain any images.");
            }

            int width = data[6] == 0 ? 256 : data[6];
            int height = data[7] == 0 ? 256 : data[7];
            int hotSpotX = ReadUInt16(data, 10);
            int hotSpotY = ReadUInt16(data, 12);
            int imageLength = checked((int)ReadUInt32(data, 14));
            int imageOffset = checked((int)ReadUInt32(data, 18));

            if (imageOffset < 0 || imageLength <= 0 || imageOffset + imageLength > data.Length)
            {
                throw new InvalidDataException("Cursor image payload is out of range.");
            }

            return new CursorDirectoryEntry(width, height, hotSpotX, hotSpotY, imageLength, imageOffset);
        }

        private static bool IsPng(byte[] data, int imageOffset)
        {
            ReadOnlySpan<byte> pngSignature = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

            return data.AsSpan(imageOffset).StartsWith(pngSignature);
        }

        private static Bitmap LoadPngBitmap(byte[] data, int imageOffset, int imageLength)
        {
            using var bitmapStream = new MemoryStream(data, imageOffset, imageLength, writable: false);
            return new Bitmap(bitmapStream);
        }

        private static Bitmap LoadBitmapInfoCursor(byte[] data, int imageOffset)
        {
            int headerSize = ReadInt32(data, imageOffset);
            int rawWidth = ReadInt32(data, imageOffset + 4);
            int rawHeight = ReadInt32(data, imageOffset + 8);
            int bitsPerPixel = ReadUInt16(data, imageOffset + 14);
            uint compression = ReadUInt32(data, imageOffset + 16);
            uint colorsUsed = ReadUInt32(data, imageOffset + 32);

            if (headerSize < 40)
            {
                throw new NotSupportedException("Unsupported cursor bitmap header.");
            }

            if (compression != 0)
            {
                throw new NotSupportedException("Compressed cursor bitmaps are not supported.");
            }

            int width = Math.Abs(rawWidth);
            int height = Math.Abs(rawHeight) / 2;
            bool bottomUp = rawHeight > 0;

            return bitsPerPixel switch
            {
                1 => LoadMonochromeCursorBitmap(data, imageOffset, headerSize, width, height, bottomUp, colorsUsed),
                32 => LoadArgbCursorBitmap(data, imageOffset, headerSize, width, height, bottomUp),
                _ => throw new NotSupportedException($"Unsupported cursor bit depth: {bitsPerPixel}.")
            };
        }

        private static Bitmap LoadMonochromeCursorBitmap(byte[] data, int imageOffset, int headerSize, int width, int height, bool bottomUp, uint colorsUsed)
        {
            int paletteCount = colorsUsed > 0 ? checked((int)colorsUsed) : 2;
            int paletteOffset = imageOffset + headerSize;
            int xorStride = AlignToDword((width + 7) / 8);
            int xorOffset = paletteOffset + (paletteCount * 4);
            int andStride = AlignToDword((width + 7) / 8);
            int andOffset = xorOffset + (xorStride * height);

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);

            var background = ReadPaletteColor(data, paletteOffset);
            var foreground = ReadPaletteColor(data, paletteOffset + 4);

            using var framebuffer = bitmap.Lock();

            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    int sourceRow = GetSourceRow(y, height, bottomUp);
                    int xorRowOffset = xorOffset + (sourceRow * xorStride);
                    int andRowOffset = andOffset + (sourceRow * andStride);
                    byte* destinationRow = (byte*)framebuffer.Address + (y * framebuffer.RowBytes);

                    for (int x = 0; x < width; x++)
                    {
                        bool xorBit = ReadMaskBit(data, xorRowOffset, x);
                        bool andBit = ReadMaskBit(data, andRowOffset, x);
                        int pixelOffset = x * 4;

                        if (andBit && !xorBit)
                        {
                            destinationRow[pixelOffset + 0] = 0;
                            destinationRow[pixelOffset + 1] = 0;
                            destinationRow[pixelOffset + 2] = 0;
                            destinationRow[pixelOffset + 3] = 0;
                            continue;
                        }

                        RgbaColor color = xorBit ? foreground : background;

                        if (andBit && xorBit)
                        {
                            // Monochrome cursors can request screen inversion here.
                            // Avalonia cursors are static bitmaps, so use a visible fallback color instead.
                            color = new RgbaColor(255, 255, 255, 255);
                        }

                        destinationRow[pixelOffset + 0] = color.B;
                        destinationRow[pixelOffset + 1] = color.G;
                        destinationRow[pixelOffset + 2] = color.R;
                        destinationRow[pixelOffset + 3] = color.A;
                    }
                }
            }

            return bitmap;
        }

        private static Bitmap LoadArgbCursorBitmap(byte[] data, int imageOffset, int headerSize, int width, int height, bool bottomUp)
        {
            int xorOffset = imageOffset + headerSize;
            int xorStride = AlignToDword(width * 4);
            int andStride = AlignToDword((width + 7) / 8);
            int andOffset = xorOffset + (xorStride * height);
            bool hasExplicitAlpha = HasExplicitAlpha(data, xorOffset, xorStride, width, height);

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);

            using var framebuffer = bitmap.Lock();

            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    int sourceRow = GetSourceRow(y, height, bottomUp);
                    int xorRowOffset = xorOffset + (sourceRow * xorStride);
                    int andRowOffset = andOffset + (sourceRow * andStride);
                    byte* destinationRow = (byte*)framebuffer.Address + (y * framebuffer.RowBytes);

                    for (int x = 0; x < width; x++)
                    {
                        int sourcePixelOffset = xorRowOffset + (x * 4);
                        int destinationPixelOffset = x * 4;

                        destinationRow[destinationPixelOffset + 0] = data[sourcePixelOffset + 0];
                        destinationRow[destinationPixelOffset + 1] = data[sourcePixelOffset + 1];
                        destinationRow[destinationPixelOffset + 2] = data[sourcePixelOffset + 2];

                        byte alpha = data[sourcePixelOffset + 3];
                        if (!hasExplicitAlpha)
                        {
                            alpha = ReadMaskBit(data, andRowOffset, x) ? (byte)0 : (byte)255;
                        }

                        destinationRow[destinationPixelOffset + 3] = alpha;
                    }
                }
            }

            return bitmap;
        }

        private static bool HasExplicitAlpha(byte[] data, int xorOffset, int xorStride, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int rowOffset = xorOffset + (y * xorStride);
                for (int x = 0; x < width; x++)
                {
                    if (data[rowOffset + (x * 4) + 3] != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int GetSourceRow(int destinationRow, int height, bool bottomUp)
        {
            return bottomUp ? (height - 1 - destinationRow) : destinationRow;
        }

        private static bool ReadMaskBit(byte[] data, int rowOffset, int x)
        {
            int byteOffset = rowOffset + (x / 8);
            int bitShift = 7 - (x % 8);

            return (data[byteOffset] & (1 << bitShift)) != 0;
        }

        private static int AlignToDword(int value)
        {
            return (value + 3) & ~3;
        }

        private static RgbaColor ReadPaletteColor(byte[] data, int offset)
        {
            return new RgbaColor(data[offset + 2], data[offset + 1], data[offset + 0], 255);
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        }

        private sealed class LoadedCursor
        {
            public LoadedCursor(Bitmap bitmap, Cursor cursor)
            {
                Bitmap = bitmap;
                Cursor = cursor;
            }

            public Bitmap Bitmap { get; }
            public Cursor Cursor { get; }
        }

        private readonly struct CursorDirectoryEntry
        {
            public CursorDirectoryEntry(int width, int height, int hotSpotX, int hotSpotY, int imageLength, int imageOffset)
            {
                Width = width;
                Height = height;
                HotspotX = hotSpotX;
                HotspotY = hotSpotY;
                ImageLength = imageLength;
                ImageOffset = imageOffset;
            }

            public int Width { get; }
            public int Height { get; }
            public int HotspotX { get; }
            public int HotspotY { get; }
            public int ImageLength { get; }
            public int ImageOffset { get; }
        }

        private readonly struct RgbaColor
        {
            public RgbaColor(byte r, byte g, byte b, byte a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            public byte R { get; }
            public byte G { get; }
            public byte B { get; }
            public byte A { get; }
        }
    }
}
