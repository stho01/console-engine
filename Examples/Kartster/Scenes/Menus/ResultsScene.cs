using System;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using Kartster.Ui;

namespace Kartster.Scenes.Menus;

/// <summary>
/// The finishing order as a table: position, name, total time (or DNF), best lap; the player's row in yellow.
/// Enter: race again (select track), Esc: title.
/// </summary>
public sealed class ResultsScene(RaceResults results) : Scene<KartsterGame>, IUiLayer
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string Title = "RESULTS";
    public const string Footer = "ENTER RACE AGAIN   ESC TITLE";
    public const string NotFinished = "DNF";
    public const int RowScale = 2;
    public const int RowHeight = 14;
    public const int MaxRows = 8;
    public const int NameChars = 10;
    private const int TableTop = 46;

    private readonly char[] _text = new char[16];

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad() { }

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        var keys = MenuKeys.From(Input.Instance);
        if (keys.Confirm)
            Game.Scenes.Set(new SelectTrackScene());
        else if (keys.Back)
            Game.Scenes.Set(new TitleScene());
    }

    public override void OnRender() { }

    public void DrawUi(IPixelTarget target) => Draw(target, results, _text);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="text">Scratch space for the numbers, at least 16 chars.</param>
    public static void Draw(IPixelTarget target, in RaceResults results, Span<char> text)
    {
        MenuScreen.DrawBackdrop(target);
        var centerX = target.Width / 2;
        PixelFont.DrawCentered(target, centerX, MenuScreen.TitleY, Title, UiColors.Text, UiColors.TextShadow,
            MenuScreen.TitleScale);
        PixelFont.DrawCentered(target, centerX, 28, results.TrackName, UiColors.Sky, UiColors.TextShadow);

        // Columns at 2 × 4 pixels per char: POS 3 chars, NAME 10, TIME 7, BEST 7, two-char gaps.
        var advance = PixelFont.Advance * RowScale;
        var left = centerX - PixelFont.MeasureChars(3 + 2 + NameChars + 2 + 7 + 2 + 7, RowScale) / 2;
        var nameX = left + 5 * advance;
        var timeX = nameX + (NameChars + 2) * advance;
        var bestX = timeX + 9 * advance;
        PixelFont.DrawTextOutlined(target, left, TableTop - 10, "POS", UiColors.Dim, UiColors.TextShadow);
        PixelFont.DrawTextOutlined(target, nameX, TableTop - 10, "NAME", UiColors.Dim, UiColors.TextShadow);
        PixelFont.DrawTextOutlined(target, timeX, TableTop - 10, "TIME", UiColors.Dim, UiColors.TextShadow);
        PixelFont.DrawTextOutlined(target, bestX, TableTop - 10, "BEST", UiColors.Dim, UiColors.TextShadow);

        var rows = results.Rows == null ? 0 : Math.Min(results.Rows.Length, MaxRows);
        for (var i = 0; i < rows; i++)
        {
            ref readonly var row = ref results.Rows[i];
            var y = TableTop + i * RowHeight;
            var color = row.IsPlayer ? UiColors.Selected : UiColors.Text;
            var length = TextFormat.Int(text, row.Position);
            PixelFont.DrawTextOutlined(target, left, y, text[..length], color, UiColors.TextShadow, RowScale);

            var name = row.Name.AsSpan();
            PixelFont.DrawTextOutlined(target, nameX, y, name[..Math.Min(name.Length, NameChars)], color,
                UiColors.TextShadow, RowScale);

            if (row.Finished)
            {
                length = TextFormat.Time(text, row.TotalSeconds);
                PixelFont.DrawTextOutlined(target, timeX, y, text[..length], color, UiColors.TextShadow, RowScale);
                length = TextFormat.Time(text, row.BestLapSeconds);
                PixelFont.DrawTextOutlined(target, bestX, y, text[..length], UiColors.Sky, UiColors.TextShadow, RowScale);
            }
            else
            {
                PixelFont.DrawTextOutlined(target, timeX, y, NotFinished, UiColors.Dim, UiColors.TextShadow, RowScale);
            }
        }

        PixelFont.DrawCentered(target, centerX, target.Height - PixelFont.GlyphHeight - 4, Footer, UiColors.Dim,
            UiColors.TextShadow);
    }
}
