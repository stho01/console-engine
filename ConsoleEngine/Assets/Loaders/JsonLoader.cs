using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ConsoleEngine.Assets.Loaders;

/// <summary>
/// Reads a JSON file into a <typeparamref name="T"/> with <see cref="System.Text.Json"/>. Takes the
/// <see cref="JsonTypeInfo{T}"/> from a source-generated <c>JsonSerializerContext</c>, so it needs no reflection and
/// stays safe for trimming:
/// <code>
/// [JsonSerializable(typeof(LevelData))]
/// internal partial class GameJson : JsonSerializerContext;
///
/// Assets.RegisterLoader(new JsonLoader&lt;LevelData&gt;(GameJson.Default.LevelData));
/// </code>
/// Register one per type. Registered for <c>json</c> by default.
/// </summary>
public sealed class JsonLoader<T> : IAssetLoader<T>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly JsonTypeInfo<T> _typeInfo;

    //**********************************************************
    //** ctor
    //**********************************************************

    public JsonLoader(JsonTypeInfo<T> typeInfo) : this(typeInfo, "json") { }

    public JsonLoader(JsonTypeInfo<T> typeInfo, params string[] extensions)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        ArgumentNullException.ThrowIfNull(extensions);
        _typeInfo = typeInfo;
        Extensions = extensions;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public IReadOnlyList<string> Extensions { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Bad JSON throws <see cref="JsonException"/> with the line and position.</summary>
    public T Load(Stream stream, AssetLoadContext context) => JsonSerializer.Deserialize(stream, _typeInfo);
}
