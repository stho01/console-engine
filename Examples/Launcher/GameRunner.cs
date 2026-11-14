using System;
using System.Diagnostics;
using System.IO;

namespace Launcher;

/// <summary>
/// Starts a game and waits for it to exit: in the classic console host on Windows, in the launcher's own terminal on
/// macOS and Linux.
/// </summary>
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
    /// Starts the game and blocks until it exits (games with a logger all listen on port 11000, so only one runs at a
    /// time). Windows uses a separate console window, macOS and Linux the launcher's own terminal.
    /// </summary>
    public static void Run(string executablePath)
    {
        if (OperatingSystem.IsWindows())
            RunInConsoleHost(executablePath);
        else
            RunInCurrentTerminal(executablePath);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// macOS and Linux have no console-host problem: the game inherits the launcher's terminal (stdin, stdout and
    /// stderr), draws on its alternate screen and restores the terminal on exit. The launcher is blocked meanwhile.
    /// </summary>
    private static void RunInCurrentTerminal(string executablePath)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = Path.GetDirectoryName(executablePath),
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        process.WaitForExit();
    }

    /// <summary>
    /// Runs the exe through <c>conhost.exe</c>, so it gets a classic console window even when Windows Terminal is the
    /// default terminal: the engine's size and font calls only work there. The game runs under <c>cmd.exe</c> in the
    /// exe's folder, because games load assets and <c>ConsoleEngine.Logger.exe</c> by relative path. Blocks until the
    /// window closes.
    /// </summary>
    private static void RunInConsoleHost(string executablePath)
    {
        var startInfo = new ProcessStartInfo(ConsoleHostPath, BuildArguments(Path.GetFileName(executablePath)))
        {
            WorkingDirectory = Path.GetDirectoryName(executablePath),
            UseShellExecute = true
        };

        using var process = Process.Start(startInfo);
        process.WaitForExit();
    }

    /// <summary>
    /// The exe is started by file name from the working directory, because <c>conhost.exe</c> mangles quoted paths
    /// that contain spaces. A non-zero exit (a crash) runs <c>pause</c>, so the exception text stays readable.
    /// <c>conhost.exe</c> always exits with 0, so the launcher cannot see the game's exit code.
    /// </summary>
    internal static string BuildArguments(string executableFileName) => $"cmd.exe /c \".\\{executableFileName} || pause\"";
}
