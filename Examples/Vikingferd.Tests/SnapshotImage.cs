using System;
using System.IO;
using System.IO.Compression;
using Vikingferd.Rendering;

namespace Vikingferd.Tests;

/// <summary>
/// Writes a <see cref="Canvas"/> as a PNG, 8 x 16 image pixels per console cell, in Windows' default Campbell colours.
/// Half blocks are exact; other glyphs are only sketched (a dot, a triangle, a block for letters), which is enough to
/// judge the pixel art without a console.
/// </summary>
internal static class SnapshotImage
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int CellWidth = 8;
    private const int CellHeight = 16;

    private static readonly uint[] Palette =
    [
        0x0C0C0C, 0x0037DA, 0x13A10E, 0x3A96DD, 0xC50F1F, 0x881798, 0xC19C00, 0xCCCCCC,
        0x767676, 0x3B78FF, 0x16C60C, 0x61D6D6, 0xE74856, 0xB4009E, 0xF9F1A5, 0xF2F2F2
    ];

    //**********************************************************
    //** public methods
    //**********************************************************

    public static void Save(Canvas canvas, string path)
    {
        var width = canvas.Width * CellWidth;
        var height = canvas.Height * CellHeight;
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < canvas.Height; y++)
        for (var x = 0; x < canvas.Width; x++)
        {
            canvas.Cell(x, y, out var character, out var foreground, out var background);
            for (var py = 0; py < CellHeight; py++)
            for (var px = 0; px < CellWidth; px++)
            {
                var color = IsInk(character, px, py) ? foreground : background;
                var i = ((y * CellHeight + py) * width + x * CellWidth + px) * 3;
                var value = Palette[(int)color];
                rgb[i] = (byte)(value >> 16);
                rgb[i + 1] = (byte)(value >> 8);
                rgb[i + 2] = (byte)value;
            }
        }

        File.WriteAllBytes(path, EncodePng(width, height, rgb));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static bool IsInk(char character, int px, int py) => character switch
    {
        ' ' => false,
        '▀' => py < CellHeight / 2,
        '▄' => py >= CellHeight / 2,
        '█' => true,
        '·' => px is 3 or 4 && py is 7 or 8,
        '∙' => px is >= 3 and <= 4 && py is >= 6 and <= 9,
        '*' => (px == 3 || px == 4 || py == 7 || py == 8) && px is >= 1 and <= 6 && py is >= 4 and <= 11,
        '▲' => py >= 6 && Math.Abs(px - 3.5f) <= (py - 6) * .45f,
        '▼' => py <= 11 && py >= 4 && Math.Abs(px - 3.5f) <= (11 - py) * .5f,
        '═' => py is 6 or 9,
        '║' => px is 2 or 5,
        '╔' or '╗' or '╚' or '╝' => py is 6 or 9 || px is 2 or 5,
        _ => px is >= 1 and <= 6 && py is >= 4 and <= 12 && (px + py) % 3 != 0
    };

    private static byte[] EncodePng(int width, int height, byte[] rgb)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        WriteInt(header, 0, width);
        WriteInt(header, 4, height);
        header[8] = 8; // bit depth
        header[9] = 2; // RGB
        WriteChunk(output, "IHDR", header);

        using var raw = new MemoryStream();
        using (var zlib = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0); // no filter
                zlib.Write(rgb, y * width * 3, width * 3);
            }
        }

        WriteChunk(output, "IDAT", raw.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        output.Write(length);

        var typeAndData = new byte[4 + data.Length];
        for (var i = 0; i < 4; i++)
            typeAndData[i] = (byte)type[i];
        data.CopyTo(typeAndData, 4);
        output.Write(typeAndData);

        var crc = new byte[4];
        WriteInt(crc, 0, (int)Crc32(typeAndData));
        output.Write(crc);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }
}
