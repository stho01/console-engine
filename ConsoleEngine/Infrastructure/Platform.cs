using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.CrossPlatform;
using ConsoleEngine.Native;

namespace ConsoleEngine.Infrastructure;

/// <summary>
/// Picks the console and input backends for the current platform. Win32 on Windows, ANSI terminal everywhere else.
/// </summary>
internal static class Platform
{
    public static IConsoleHandler CreateConsoleHandler(int width, int height, FontInfo fontInfo) => OperatingSystem.IsWindows()
        ? new ConsoleHandler(width, height, fontInfo)
        : new CrossPlatformConsoleHandler(width, height);

    public static IInputHandler CreateInputHandler() => OperatingSystem.IsWindows()
        ? new InputHandler()
        : new CrossPlatformInputHandler();
}
