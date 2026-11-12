using Vikingferd.Components;
using Vikingferd.Puzzle;

namespace Vikingferd;

/// <summary>Every text the game shows, built once.</summary>
public static class GameTexts
{
    public const string Title = " VIKINGFERD ";
    public const string KeyHelp = "←→/A D run   SPACE/W jump   E use   TAB/1-3 switch viking   R restart   ESC levels";
    public const string Won = "All three vikings made it!";
    public const string WonNext = "ENTER  next level";
    public const string Finished = "The journey is complete. Skål!";
    public const string FinishedNext = "ENTER  back to the levels";
    public const string Retry = "R  try again";
    public const string Active = " ◄ you";

    private static readonly string[] FellTexts = BuildFailTexts("fell too far and is out cold.");
    private static readonly string[] SpikeTexts = BuildFailTexts("stepped on the spikes.");

    /// <summary>Header text of a level, e.g. "Level 2/5  Rockslide".</summary>
    public static string LevelHeader(int levelIndex) =>
        $"Level {levelIndex + 1}/{Levels.All.Length}  {Levels.All[levelIndex].Title}";

    public static string Failed(ActorKind hero, FailCause cause) =>
        cause == FailCause.Spikes ? SpikeTexts[Heroes.IndexOf(hero)] : FellTexts[Heroes.IndexOf(hero)];

    private static string[] BuildFailTexts(string what)
    {
        var texts = new string[Heroes.Order.Length];
        for (var i = 0; i < texts.Length; i++)
            texts[i] = $"{Heroes.Of(Heroes.Order[i]).Name} {what}";
        return texts;
    }
}
