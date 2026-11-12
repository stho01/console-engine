namespace EternalDigger.Scenes;

/// <summary>What the player picked on the title screen.</summary>
public enum TitleChoice : byte
{
    None,
    Continue,
    NewMine
}

/// <summary>
/// The title screen's rules, without drawing or keys: <c>CONTINUE</c> (only with a loaded save) and <c>NEW MINE</c>,
/// picked with Up/Down and E. With a save, NEW MINE first asks to confirm (<c>NO</c> / <c>YES, START OVER</c>, NO
/// selected). The menu wraps around.
/// </summary>
public sealed class TitleMenu(bool hasSave)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string ContinueText = "CONTINUE";
    public const string NewMineText = "NEW MINE";
    public const string ConfirmQuestion = "Start over? The saved mine will be lost.";
    public const string NoText = "NO";
    public const string YesText = "YES, START OVER";

    private static readonly string[] MainWithSave = [ContinueText, NewMineText];
    private static readonly string[] MainWithoutSave = [NewMineText];
    private static readonly string[] Confirm = [NoText, YesText];

    //**********************************************************
    //** props
    //**********************************************************

    public bool HasSave { get; } = hasSave;

    /// <summary>NEW MINE was picked with a save: the menu shows <see cref="NoText"/> / <see cref="YesText"/>.</summary>
    public bool IsConfirming { get; private set; }

    public int Selected { get; private set; }

    /// <summary>The items shown now, top to bottom.</summary>
    public string[] Items => IsConfirming ? Confirm : HasSave ? MainWithSave : MainWithoutSave;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Up() => Selected = (Selected + Items.Length - 1) % Items.Length;

    public void Down() => Selected = (Selected + 1) % Items.Length;

    /// <summary>E on the selected item. Returns what to start, or <see cref="TitleChoice.None"/> to stay.</summary>
    public TitleChoice Select()
    {
        var item = Items[Selected];
        switch (item)
        {
            case ContinueText:
                return TitleChoice.Continue;
            case NewMineText when HasSave:
                IsConfirming = true;
                Selected = 0;
                return TitleChoice.None;
            case NewMineText:
            case YesText:
                return TitleChoice.NewMine;
            default:
                // NO: back to the main menu, on NEW MINE.
                IsConfirming = false;
                Selected = 1;
                return TitleChoice.None;
        }
    }
}
