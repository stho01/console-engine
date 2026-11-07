using System;
using System.IO;

namespace Launcher;

/// <summary>
/// Finds each game's exe in its own build output. Games are built next to the launcher with the same configuration
/// and target framework, so a game's exe is <c>Examples/&lt;Project&gt;/&lt;output path&gt;/&lt;Project&gt;.exe</c>,
/// where the output path (e.g. <c>bin/Debug/net10.0</c>) is the launcher's own.
/// </summary>
internal sealed class GameLocator
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string LauncherProjectFile = "Launcher.csproj";

    private readonly string _examplesDirectory;
    private readonly string _outputPath;

    //**********************************************************
    //** ctor
    //**********************************************************

    public GameLocator(string examplesDirectory, string outputPath)
    {
        _examplesDirectory = examplesDirectory;
        _outputPath = outputPath;
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Walks up from the launcher's output folder to the folder holding <see cref="LauncherProjectFile"/>. Its parent
    /// is <c>Examples/</c>, and the path between the two is the output path every game shares.
    /// </summary>
    /// <exception cref="InvalidOperationException">The launcher is not running from its build output.</exception>
    public static GameLocator FromLauncherOutput(string launcherOutputDirectory)
    {
        var projectDirectory = new DirectoryInfo(launcherOutputDirectory);
        while (projectDirectory != null && !File.Exists(Path.Combine(projectDirectory.FullName, LauncherProjectFile)))
            projectDirectory = projectDirectory.Parent;

        if (projectDirectory?.Parent == null)
            throw new InvalidOperationException(
                $"No {LauncherProjectFile} found above {launcherOutputDirectory}. " +
                "Run the launcher from its build output (dotnet run --project Examples/Launcher).");

        var outputPath = Path.GetRelativePath(projectDirectory.FullName, launcherOutputDirectory);
        return new GameLocator(projectDirectory.Parent.FullName, outputPath);
    }

    public string GetExecutablePath(GameEntry game) =>
        Path.GetFullPath(Path.Combine(_examplesDirectory, game.ProjectName, _outputPath, game.ProjectName + ".exe"));
}
