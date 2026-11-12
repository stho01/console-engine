using NUnit.Framework;

namespace Launcher.Tests;

public class GameRunnerTests
{
    [Test]
    public void BuildArguments_GameExe_RunsItFromWorkingDirectoryAndPausesOnFailure()
    {
        var arguments = GameRunner.BuildArguments("Snake.Ecs.exe");

        Assert.That(arguments, Is.EqualTo("cmd.exe /c \".\\Snake.Ecs.exe || pause\""));
    }
}
