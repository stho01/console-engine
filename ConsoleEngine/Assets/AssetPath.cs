using System;
using System.Collections.Generic;

namespace ConsoleEngine.Assets;

/// <summary>
/// The id of an asset: a path relative to the asset root, independent of where the bytes are stored. Always normalized:
/// <c>/</c> as separator, no empty, <c>.</c> or <c>..</c> segments, never rooted. Compared ordinally, so case matters
/// on every platform. <see cref="Root"/> (the default value) is the asset root itself.
/// </summary>
public readonly struct AssetPath : IEquatable<AssetPath>
{
    //**********************************************************
    //** fields
    //**********************************************************

    public static readonly AssetPath Root = default;

    private readonly string _value;

    //**********************************************************
    //** ctor
    //**********************************************************

    private AssetPath(string value)
    {
        _value = value;
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The normalized path, for example <c>Maps/map1.txt</c>. Empty for <see cref="Root"/>.</summary>
    public string Value => _value ?? string.Empty;

    public bool IsRoot => string.IsNullOrEmpty(_value);

    /// <summary>The last segment, for example <c>map1.txt</c>.</summary>
    public string FileName => Value[(Value.LastIndexOf('/') + 1)..];

    /// <summary>The extension in lower case without the dot, for example <c>txt</c>. Empty if there is none.</summary>
    public string Extension
    {
        get
        {
            var fileName = FileName;
            var dot = fileName.LastIndexOf('.');
            return dot < 0 ? string.Empty : fileName[(dot + 1)..].ToLowerInvariant();
        }
    }

    /// <summary>The folder that holds this path, for example <c>Maps</c>. <see cref="Root"/> for a top-level file.</summary>
    public AssetPath Directory
    {
        get
        {
            var slash = Value.LastIndexOf('/');
            return slash < 0 ? Root : new AssetPath(Value[..slash]);
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Normalizes <paramref name="path"/>: <c>\</c> becomes <c>/</c>, <c>.</c> and empty segments are dropped and
    /// <c>..</c> removes the segment before it.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The path is empty, rooted, contains <c>:</c> or leaves the asset root.
    /// </exception>
    public static AssetPath Parse(string path) => Root.Resolve(path);

    /// <summary>
    /// Resolves <paramref name="relativePath"/> against this path taken as a folder. <c>Maps</c> resolving
    /// <c>../Tiles/rock.txt</c> gives <c>Tiles/rock.txt</c>.
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="Parse"/>.</exception>
    public AssetPath Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("An asset path can't be empty.", nameof(relativePath));

        var path = relativePath.Replace('\\', '/');
        if (path[0] == '/' || path.Contains(':'))
            throw new ArgumentException(
                $"Asset path '{relativePath}' must be relative to the asset root.", nameof(relativePath));

        var segments = new List<string>();
        if (!IsRoot)
            segments.AddRange(Value.Split('/'));

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
                continue;

            if (segment == "..")
            {
                if (segments.Count == 0)
                    throw new ArgumentException(
                        $"Asset path '{relativePath}' leaves the asset root.", nameof(relativePath));

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
            throw new ArgumentException($"Asset path '{relativePath}' names no file.", nameof(relativePath));

        return new AssetPath(string.Join('/', segments));
    }

    /// <summary>
    /// <c>.TXT</c>, <c>txt</c> and <c> Txt </c> all become <c>txt</c>, the form <see cref="Extension"/> has.
    /// </summary>
    internal static string NormalizeExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return extension.Trim().TrimStart('.').ToLowerInvariant();
    }

    public bool Equals(AssetPath other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is AssetPath other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(AssetPath left, AssetPath right) => left.Equals(right);

    public static bool operator !=(AssetPath left, AssetPath right) => !left.Equals(right);

    /// <summary>Same as <see cref="Parse"/>, so <c>Assets.Load&lt;Map&gt;("Maps/map1.txt")</c> works.</summary>
    public static implicit operator AssetPath(string path) => Parse(path);
}
