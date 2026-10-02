using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AgentWpf.Infrastructure.FlaUI;

/// <summary>
/// Encodes 32-bit BGRA pixels as an opaque RGBA PNG without external dependencies.
/// </summary>
public static class PngEncoder
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    /// Encodes top-down BGRA pixels.
    /// </summary>
    /// <param name="bgra">The pixels, 4 bytes each, rows top to bottom.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The PNG file content.</returns>
    public static byte[] Encode(ReadOnlySpan<byte> bgra, int width, int height)
    {
        Debug.Assert(width > 0 && height > 0, "Precondition: the image must not be empty.");
        Debug.Assert(bgra.Length == (long)width * height * 4, "Precondition: pixel buffer must match the size.");

        using var output = new MemoryStream();
        output.Write(Signature);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 6; // color type RGBA
        WriteChunk(output, "IHDR", header);
        WriteChunk(output, "IDAT", Compress(bgra, width, height));
        WriteChunk(output, "IEND", []);

        var png = output.ToArray();
        Debug.Assert(png.Length > Signature.Length, "Postcondition: the PNG must contain chunks.");
        return png;
    }

    private static byte[] Compress(ReadOnlySpan<byte> bgra, int width, int height)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[1 + (width * 4)];
            for (var y = 0; y < height; y++)
            {
                var source = bgra.Slice(y * width * 4, width * 4);
                for (var x = 0; x < width; x++)
                {
                    row[1 + (x * 4)] = source[(x * 4) + 2];
                    row[2 + (x * 4)] = source[(x * 4) + 1];
                    row[3 + (x * 4)] = source[x * 4];
                    row[4 + (x * 4)] = 0xFF;
                }

                zlib.Write(row);
            }
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc(typeBytes, data));
        output.Write(number);
    }

    private static uint Crc(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in type)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
