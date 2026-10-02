using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using AgentWpf.Infrastructure.FlaUI;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Infrastructure.FlaUI.Tests;

[TestFixture]
public sealed class PngEncoderTests
{
    [Test]
    public void Writes_signature_and_header_with_size()
    {
        var png = PngEncoder.Encode(new byte[2 * 3 * 4], 2, 3);

        png[..8].ShouldBe(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        System.Text.Encoding.ASCII.GetString(png, 12, 4).ShouldBe("IHDR");
        BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)).ShouldBe(2);
        BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)).ShouldBe(3);
        System.Text.Encoding.ASCII.GetString(png, png.Length - 8, 4).ShouldBe("IEND");
    }

    [Test]
    public void Converts_bgra_to_opaque_rgba_rows_with_filter_byte()
    {
        byte[] bgra = [10, 20, 30, 0];

        var png = PngEncoder.Encode(bgra, 1, 1);

        var idatLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33));
        System.Text.Encoding.ASCII.GetString(png, 37, 4).ShouldBe("IDAT");
        using var zlib = new ZLibStream(new MemoryStream(png, 41, idatLength), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        raw.ToArray().ShouldBe(new byte[] { 0, 30, 20, 10, 255 });
    }

    [Test]
    public void Header_crc_matches_reference_value()
    {
        var png = PngEncoder.Encode(new byte[4], 1, 1);

        // CRC of "IHDR" + 00000001 00000001 08 06 00 00 00, as produced by any PNG writer.
        BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(29)).ShouldBe(0x1F15C489u);
    }
}
