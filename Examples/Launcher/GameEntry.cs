namespace Launcher;

/// <summary>A game the launcher can start.</summary>
/// <param name="Title">Name shown in the menu.</param>
/// <param name="ProjectName">Folder under <c>Examples/</c>. Also the exe's assembly name.</param>
/// <param name="Controls">Short key help shown next to the title.</param>
internal sealed record GameEntry(string Title, string ProjectName, string Controls);
