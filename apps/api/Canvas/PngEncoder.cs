using System.IO.Compression;

namespace pixels_site.Api.Canvas;

public static class PngEncoder
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] EncodeRgb(IReadOnlyList<Rgb> pixels, int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Canvas dimensions must be positive.");

        if (pixels.Count != width * height)
            throw new ArgumentException("Pixel buffer size does not match canvas dimensions.", nameof(pixels));

        using var output = new MemoryStream();
        output.Write(PngSignature);

        var ihdr = new byte[13];
        WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 8;
        ihdr[9] = 2;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(output, "IHDR", ihdr);

        var scanlineSize = 1 + width * 3;
        var raw = new byte[height * scanlineSize];

        for (var y = 0; y < height; y++)
        {
            var rowStart = y * scanlineSize;
            raw[rowStart] = 0;

            for (var x = 0; x < width; x++)
            {
                var pixel = pixels[y * width + x];
                var offset = rowStart + 1 + x * 3;
                raw[offset] = pixel.R;
                raw[offset + 1] = pixel.G;
                raw[offset + 2] = pixel.B;
            }
        }

        byte[] compressed;
        using (var compressedStream = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressedStream, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zlib.Write(raw);
            }

            compressed = compressedStream.ToArray();
        }

        WriteChunk(output, "IDAT", compressed);
        WriteChunk(output, "IEND", []);

        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        if (typeBytes.Length != 4)
            throw new ArgumentException("PNG chunk type must be exactly 4 bytes.", nameof(type));

        Span<byte> lengthBuffer = stackalloc byte[4];
        WriteUInt32BigEndian(lengthBuffer, (uint)data.Length);
        output.Write(lengthBuffer);
        output.Write(typeBytes);
        output.Write(data);

        var crc = Crc32(typeBytes, data);
        Span<byte> crcBuffer = stackalloc byte[4];
        WriteUInt32BigEndian(crcBuffer, crc);
        output.Write(crcBuffer);
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        const uint polynomial = 0xEDB88320;
        var crc = 0xFFFFFFFFu;

        for (var i = 0; i < type.Length; i++)
        {
            crc ^= type[i];
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 1 ? (crc >> 1) ^ polynomial : crc >> 1;
        }

        for (var i = 0; i < data.Length; i++)
        {
            crc ^= data[i];
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 1 ? (crc >> 1) ^ polynomial : crc >> 1;
        }

        return ~crc;
    }

    private static void WriteUInt32BigEndian(Span<byte> target, uint value)
    {
        target[0] = (byte)(value >> 24);
        target[1] = (byte)(value >> 16);
        target[2] = (byte)(value >> 8);
        target[3] = (byte)value;
    }
}
