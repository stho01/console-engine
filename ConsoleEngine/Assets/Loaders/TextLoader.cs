using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConsoleEngine.Assets.Loaders;

/// <summary>
/// Reads a whole text file as one <see cref="string"/>. UTF-8, or whatever a byte order mark says. Registered for
/// <c>txt</c> by default; pass other extensions to read more (<c>new TextLoader("story")</c>).
/// </summary>
public sealed class TextLoader : IAssetLoader<string>
{
    public TextLoader() : this("txt") { }

    public TextLoader(params string[] extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        Extensions = extensions;
    }

    public IReadOnlyList<string> Extensions { get; }

    public string Load(Stream stream, AssetLoadContext context)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        return reader.ReadToEnd();
    }
}
