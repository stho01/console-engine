namespace Launcher;

/// <summary>User-visible launcher strings. <c>{0}</c> is the game title unless noted.</summary>
internal static class LauncherTexts
{
    public const string WindowTitle = "ConsoleEngine launcher";
    public const string Heading = "ConsoleEngine - game launcher";
    /// <summary><c>{0}</c> is the highest digit that starts a game.</summary>
    public const string KeyHelp = "Up/Down select   Enter start   1-{0} start directly   Esc quit";
    public const string NotBuiltTag = "(not built)";
    public const string Running = "Running {0}. Close its window to come back here.";
    public const string Closed = "{0} closed.";
    /// <summary><c>{1}</c> is the expected exe path.</summary>
    public const string NotBuilt = "{0} is not built: {1}. Run dotnet build.";
    /// <summary><c>{1}</c> is the exception message.</summary>
    public const string StartFailed = "Could not start {0}: {1}";
}
