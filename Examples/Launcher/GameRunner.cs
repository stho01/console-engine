using System;
using System.Diagnostics;
using System.IO;

namespace Launcher;

/// <summary>Starts a game in the classic console host and waits for it to exit.</summary>
internal static class GameRunner
{
    //**********************************************************
    //** fields
    //**********************************************************

    private static readonly string ConsoleHostPath = Path.Combine(Environment.SystemDirectory, "conhost.exe");

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Runs the exe through <c>conhost.exe</c>, so it gets a classic console window even when Windows Terminal is the
    /// default terminal: the engine's size and font calls only work there. The game runs under <c>cmd.exe</c> in the
    /// exe's folder, because games load assets and <c>ConsoleEngine.Logger.exe</c> by relative path. Blocks until the
    /// window closes: games with a logger all listen on port 11000, so only one runs at a time.
    /// </summary>
    public static void Run(string executablePath)
    {
        var startInfo = new ProcessStartInfo(ConsoleHostPath, BuildArguments(Path.GetFileName(executablePath)))
        {
            WorkingDirectory = Path.GetDirectoryName(executablePath),
            UseShellExecute = true
        };

        using var process = Process.Start(startInfo);
        process.WaitForExit();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// The exe is started by file name from the working directory, because <c>conhost.exe</c> mangles quoted paths
    /// that contain spaces. A non-zero exit (a crash) runs <c>pause</c>, so the exception text stays readable.
    /// <c>conhost.exe</c> always exits with 0, so the launcher cannot see the game's exit code.
    /// </summary>
    internal static string BuildArguments(string executableFileName) => $"cmd.exe /c \".\\{executableFileName} || pause\"";
}
