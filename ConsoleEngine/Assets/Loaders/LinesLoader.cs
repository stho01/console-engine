using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConsoleEngine.Assets.Loaders;

/// <summary>
/// Reads a text file as one <see cref="string"/> per line, like <see cref="File.ReadAllLines(string)"/>: <c>\n</c>,
/// <c>\r\n</c> and <c>\r</c> all end a line, and a final line break adds no empty line. Registered for <c>txt</c> by
/// default; pass other extensions to read more (<c>new LinesLoader("map")</c>).
/// </summary>
public sealed class LinesLoader : IAssetLoader<string[]>
{
    public LinesLoader() : this("txt") { }

    public LinesLoader(params string[] extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        Extensions = extensions;
    }

    public IReadOnlyList<string> Extensions { get; }

    public string[] Load(Stream stream, AssetLoadContext context)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            lines.Add(line);

        return lines.ToArray();
    }
}
