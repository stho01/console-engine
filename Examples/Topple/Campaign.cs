namespace Topple;

/// <summary>Which level is being played and the score banked from the levels won before it.</summary>
public sealed class Campaign(int levelCount)
{
    //**********************************************************
    //** props
    //**********************************************************

    public int LevelCount { get; } = levelCount;
    public int LevelIndex { get; private set; }

    /// <summary>The scores of the levels won so far, without the current one.</summary>
    public int TotalScore { get; private set; }

    public bool IsLastLevel => LevelIndex == LevelCount - 1;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Banks a won level's score and moves to the next level. After the last level the campaign starts over from the
    /// first one with no score.
    /// </summary>
    public void CompleteLevel(int levelScore)
    {
        if (IsLastLevel)
        {
            LevelIndex = 0;
            TotalScore = 0;
            return;
        }

        TotalScore += levelScore;
        LevelIndex++;
    }
}
