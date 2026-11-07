using System;
using System.IO;
using NUnit.Framework;

namespace Launcher.Tests;

public class GameLocatorTests
{
    private string _root;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "LauncherTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void FromLauncherOutput_InLauncherBuildOutput_FindsGameInSameOutputPath()
    {
        var launcherProject = Path.Combine(_root, "Examples", "Launcher");
        var launcherOutput = Path.Combine(launcherProject, "bin", "Release", "net10.0");
        Directory.CreateDirectory(launcherOutput);
        File.WriteAllText(Path.Combine(launcherProject, GameLocator.LauncherProjectFile), "");

        var locator = GameLocator.FromLauncherOutput(launcherOutput);
        var path = locator.GetExecutablePath(new GameEntry("Snake (ECS)", "Snake.Ecs", ""), isWindows: true);

        var expected = Path.Combine(_root, "Examples", "Snake.Ecs", "bin", "Release", "net10.0", "Snake.Ecs.exe");
        Assert.That(path, Is.EqualTo(Path.GetFullPath(expected)));
    }

    [Test]
    public void GetExecutablePath_OnMacOrLinux_HasNoExeExtension()
    {
        var locator = new GameLocator(Path.Combine(_root, "Examples"), Path.Combine("bin", "Release", "net10.0"));

        var path = locator.GetExecutablePath(new GameEntry("Snake (ECS)", "Snake.Ecs", ""), isWindows: false);

        var expected = Path.Combine(_root, "Examples", "Snake.Ecs", "bin", "Release", "net10.0", "Snake.Ecs");
        Assert.That(path, Is.EqualTo(Path.GetFullPath(expected)));
    }

    [Test]
    public void FromLauncherOutput_OutsideLauncherProject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => GameLocator.FromLauncherOutput(_root));
    }

    [Test]
    public void GameCatalog_EveryGame_HasBuildOrderReferenceInLauncherProject()
    {
        var launcherProject = FindLauncherProject();
        var projectXml = File.ReadAllText(launcherProject);

        foreach (var game in GameCatalog.Games)
        {
            var reference = $@"..\{game.ProjectName}\{game.ProjectName}.csproj";
            Assert.That(projectXml, Does.Contain(reference), game.Title);
            var gameProject = Path.Combine(
                Path.GetDirectoryName(launcherProject), "..", game.ProjectName, game.ProjectName + ".csproj");
            Assert.That(File.Exists(gameProject), Is.True, game.Title);
        }
    }

    private static string FindLauncherProject()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "Examples", "Launcher", GameLocator.LauncherProjectFile);
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(GameLocator.LauncherProjectFile);
    }
}
