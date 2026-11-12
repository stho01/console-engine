using ConsoleEngine.Assets;
using ConsoleEngine.Infrastructure.Rendering;
using NUnit.Framework;
using TerraForM.Assets;

namespace TerraForM.Ecs.Tests;

public class SpritePathsTests
{
    // The sprites are copied next to this test assembly through the TerraForM.Ecs project reference, so this reads
    // them where the game does. Sizes are those of the old Sprites class the files were extracted from.
    [TestCase(SpritePaths.GameOver, 36, 16)]
    [TestCase(SpritePaths.TopMenu, 65, 9)]
    [TestCase(SpritePaths.InputName, 66, 34)]
    [TestCase(SpritePaths.Story, 66, 34)]
    public void Load_ShippedSprite_HasItsSize(string path, int width, int height)
    {
        var sprite = new AssetManager(DirectoryAssetSource.CreateDefault()).Load<Sprite>(path);

        Assert.Multiple(() =>
        {
            Assert.That(sprite.Width, Is.EqualTo(width));
            Assert.That(sprite.Height, Is.EqualTo(height));
        });
    }

    [Test]
    public void Story_NamePlaceholder_IsWhereMenuSceneDrawsTheName()
    {
        var story = new AssetManager(DirectoryAssetSource.CreateDefault()).Load<Sprite>(SpritePaths.Story);

        Assert.That(new string([story.Data[32, 22].Char, story.Data[33, 22].Char, story.Data[34, 22].Char]),
            Is.EqualTo("###"));
    }
}
