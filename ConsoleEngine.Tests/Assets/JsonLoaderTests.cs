using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleEngine.Assets;
using ConsoleEngine.Assets.Loaders;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Assets;

public sealed class LevelInfo
{
    public string Name { get; set; }
    public int Power { get; set; }
    public string[] Rows { get; set; }
}

[JsonSerializable(typeof(LevelInfo))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal partial class TestJsonContext : JsonSerializerContext;

public class JsonLoaderTests
{
    private static AssetManager Assets(string path, string json)
    {
        var assets = new AssetManager(new MemoryAssetSource().Add(path, json));
        assets.RegisterLoader(new JsonLoader<LevelInfo>(TestJsonContext.Default.LevelInfo));
        return assets;
    }

    [Test]
    public void Load_Json_DeserializesWithSourceGeneratedTypeInfo()
    {
        var level = Assets("Levels/one.json", """{ "name": "One", "power": 3500, "rows": ["S.", ".F"] }""")
            .Load<LevelInfo>("Levels/one.json");

        Assert.Multiple(() =>
        {
            Assert.That(level.Name, Is.EqualTo("One"));
            Assert.That(level.Power, Is.EqualTo(3500));
            Assert.That(level.Rows, Is.EqualTo(new[] { "S.", ".F" }));
        });
    }

    [Test]
    public void Load_BadJson_WrapsJsonException()
    {
        var assets = Assets("Levels/bad.json", """{ "name": """);

        var e = Assert.Throws<AssetLoadException>(() => assets.Load<LevelInfo>("Levels/bad.json"));

        Assert.That(e.InnerException, Is.InstanceOf<JsonException>());
    }

    [Test]
    public void Load_JsonNull_ThrowsReturnedNull()
    {
        var assets = Assets("Levels/null.json", "null");

        Assert.That(() => assets.Load<LevelInfo>("Levels/null.json"), Throws.TypeOf<AssetLoadException>()
            .With.Message.EqualTo("JsonLoader`1 returned null for asset 'Levels/null.json'."));
    }

    [Test]
    public void Load_OtherExtension_WhenRegistered()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("Levels/two.level", """{ "name": "Two" }"""));
        assets.RegisterLoader(new JsonLoader<LevelInfo>(TestJsonContext.Default.LevelInfo, "level"));

        Assert.That(assets.Load<LevelInfo>("Levels/two.level").Name, Is.EqualTo("Two"));
    }
}
