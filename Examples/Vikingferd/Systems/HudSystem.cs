using System;
using ConsoleEngine.Ecs;
using Vikingferd.Components;
using Vikingferd.Puzzle;
using Vikingferd.Rendering;

namespace Vikingferd.Systems;

/// <summary>
/// Header (title, level, hint), the three hero cards with a portrait (the controlled one highlighted) and the key
/// help, on black strips; when the attempt is over, a message box in the middle of the stage. Drawn over the stage.
/// </summary>
public sealed class HudSystem(Canvas canvas, Expedition expedition) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const ConsoleColor StripColor = ConsoleColor.Black;
    private const ConsoleColor TextColor = ConsoleColor.White;
    private const ConsoleColor HintColor = ConsoleColor.DarkGray;
    private const ConsoleColor BoxBackground = ConsoleColor.DarkBlue;
    private const ConsoleColor BoxBorder = ConsoleColor.Blue;
    private const int BoxWidth = 50;
    private const int BoxHeight = 7;
    private const int PortraitWidth = 4;

    private static readonly string[] HeroNumbers = ["1", "2", "3"];

    private readonly string _header = GameTexts.LevelHeader(expedition.LevelIndex);
    private readonly string _blankRow = new(' ', Layout.ScreenWidth);
    private readonly string _boxEdge = "╔" + new string('═', BoxWidth - 2) + "╗";
    private readonly string _boxBottom = "╚" + new string('═', BoxWidth - 2) + "╝";
    private readonly string _boxMiddle = "║" + new string(' ', BoxWidth - 2) + "║";

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        Strip(Layout.HeaderRow);
        Strip(Layout.HintRow);
        for (var row = Layout.PanelRow; row <= Layout.KeyHelpRow; row++)
            Strip(row);

        canvas.Text(0, Layout.HeaderRow, GameTexts.Title, ConsoleColor.Black, ConsoleColor.Yellow);
        canvas.Text(GameTexts.Title.Length + 2, Layout.HeaderRow, _header, TextColor, StripColor);
        canvas.Text(1, Layout.HintRow, expedition.Level.Hint, ConsoleColor.Gray, StripColor);
        DrawHeroCards();
        canvas.Text(Centered(GameTexts.KeyHelp), Layout.KeyHelpRow, GameTexts.KeyHelp, HintColor, StripColor);

        switch (expedition.Outcome)
        {
            case Outcome.Won when expedition.IsLastLevel:
                DrawBox(GameTexts.Finished, GameTexts.FinishedNext, ConsoleColor.Yellow);
                break;
            case Outcome.Won:
                DrawBox(GameTexts.Won, GameTexts.WonNext, ConsoleColor.Yellow);
                break;
            case Outcome.Failed:
                DrawBox(GameTexts.Failed(expedition.FailedHero, expedition.FailCause), GameTexts.Retry,
                    Looks.HeroColor(expedition.FailedHero));
                break;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Strip(int row) => canvas.Text(0, row, _blankRow, TextColor, StripColor);

    private void DrawHeroCards()
    {
        for (var i = 0; i < Heroes.Order.Length; i++)
        {
            var kind = Heroes.Order[i];
            var stats = Heroes.Of(kind);
            var color = Looks.HeroColor(kind);
            var active = kind == expedition.ActiveHero;
            var x = i * Layout.HeroCardWidth + 2;
            var textX = x + PortraitWidth + 2;

            var portrait = new Actor { Kind = kind, Facing = 1, OnGround = true };
            Looks.HeroSprite(portrait).Draw(canvas, x, Layout.PanelRow * 2);

            canvas.Text(textX, Layout.PanelRow, HeroNumbers[i], HintColor, StripColor);
            canvas.Text(textX + 2, Layout.PanelRow, stats.Name, active ? ConsoleColor.Black : color,
                active ? color : StripColor);
            if (active)
                canvas.Text(textX + 2 + stats.Name.Length, Layout.PanelRow, GameTexts.Active, color, StripColor);
            canvas.Text(textX + 2, Layout.PanelRow + 1, stats.Ability, active ? TextColor : HintColor, StripColor);
        }
    }

    private void DrawBox(string line, string action, ConsoleColor lineColor)
    {
        var x = (Layout.ScreenWidth - BoxWidth) / 2;
        var y = Layout.StageTop + (Layout.StageAreaHeight - BoxHeight) / 2;
        canvas.Text(x, y, _boxEdge, BoxBorder, BoxBackground);
        for (var row = 1; row < BoxHeight - 1; row++)
            canvas.Text(x, y + row, _boxMiddle, BoxBorder, BoxBackground);
        canvas.Text(x, y + BoxHeight - 1, _boxBottom, BoxBorder, BoxBackground);

        canvas.Text(Centered(line), y + 2, line, lineColor, BoxBackground);
        canvas.Text(Centered(action), y + 4, action, TextColor, BoxBackground);
    }

    private static int Centered(string text) => (Layout.ScreenWidth - text.Length) / 2;
}
