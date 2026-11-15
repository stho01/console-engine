using System;
using ConsoleEngine.Assets;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Assets;

public class AssetPathTests
{
    [TestCase("Maps/map1.txt", "Maps/map1.txt")]
    [TestCase(@"Maps\map1.txt", "Maps/map1.txt")]
    [TestCase("Maps//./map1.txt", "Maps/map1.txt")]
    [TestCase("Maps/../Tiles/rock.txt", "Tiles/rock.txt")]
    [TestCase("map1.txt", "map1.txt")]
    public void Parse_ValidPath_IsNormalized(string path, string expected)
    {
        Assert.That(AssetPath.Parse(path).Value, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("/Maps/map1.txt")]
    [TestCase(@"\Maps\map1.txt")]
    [TestCase(@"C:\Maps\map1.txt")]
    [TestCase("../map1.txt")]
    [TestCase("Maps/../../map1.txt")]
    [TestCase("Maps/..")]
    [TestCase("file:map1.txt")]
    public void Parse_InvalidPath_Throws(string path)
    {
        Assert.That(() => AssetPath.Parse(path), Throws.ArgumentException);
    }

    [Test]
    public void Parse_Null_Throws()
    {
        Assert.That(() => AssetPath.Parse(null), Throws.ArgumentException);
    }

    [Test]
    public void Parts_NestedPath_SplitIntoDirectoryFileNameAndLowerCaseExtension()
    {
        var path = AssetPath.Parse("Maps/Desert/Level1.MAP.TXT");

        Assert.Multiple(() =>
        {
            Assert.That(path.Directory.Value, Is.EqualTo("Maps/Desert"));
            Assert.That(path.FileName, Is.EqualTo("Level1.MAP.TXT"));
            Assert.That(path.Extension, Is.EqualTo("txt"));
        });
    }

    [Test]
    public void Directory_TopLevelFile_IsRoot()
    {
        Assert.That(AssetPath.Parse("map1.txt").Directory.IsRoot, Is.True);
    }

    [Test]
    public void Extension_NoDot_IsEmpty()
    {
        Assert.That(AssetPath.Parse("Maps/README").Extension, Is.Empty);
    }

    [Test]
    public void Resolve_RelativeToDirectory_CombinesAndNormalizes()
    {
        var maps = AssetPath.Parse("Maps/map1.txt").Directory;

        Assert.That(maps.Resolve("../Tiles/rock.txt").Value, Is.EqualTo("Tiles/rock.txt"));
    }

    [Test]
    public void Equals_DifferentCase_IsNotEqual()
    {
        Assert.That(AssetPath.Parse("Maps/map1.txt"), Is.Not.EqualTo(AssetPath.Parse("maps/map1.txt")));
    }

    [Test]
    public void Equals_DifferentSpellingSamePath_IsEqualWithSameHashCode()
    {
        var a = AssetPath.Parse(@"Maps\map1.txt");
        var b = AssetPath.Parse("Maps/./map1.txt");

        Assert.Multiple(() =>
        {
            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void ImplicitConversion_FromString_Parses()
    {
        AssetPath path = @"Maps\map1.txt";

        Assert.That(path.Value, Is.EqualTo("Maps/map1.txt"));
    }

    [Test]
    public void Root_Default_IsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AssetPath.Root.IsRoot, Is.True);
            Assert.That(AssetPath.Root.Value, Is.Empty);
            Assert.That(default(AssetPath), Is.EqualTo(AssetPath.Root));
        });
    }
}
