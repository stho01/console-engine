namespace Vikingferd.Puzzle;

/// <summary>
/// One puzzle: a title, a one-line hint shown under the header, and the map (one string per tile row, all the same
/// width; the characters are listed on <see cref="Levels"/>).
/// </summary>
public sealed record Level(string Title, string Hint, string[] Rows);
