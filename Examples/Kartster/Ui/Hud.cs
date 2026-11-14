using System;

namespace Kartster.Ui;

/// <summary>
/// The race HUD, laid out like Kartster's on a 320×180 screen and anchored to the target's corners: "LAP 2/3" and the
/// clock bottom-left with the standings on top, a dial speedometer in km/h bottom-right, the power-up top-right
/// (nothing when empty), the countdown, "GO!" and "FINISHED" top-centre. All text has a dark outline so it reads over
/// the road. The top-left corner is the <see cref="Minimap"/>'s. One instance per game; it allocates nothing
/// per frame.
/// </summary>
public sealed class Hud
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Margin = 4;
    public const int LapScale = 2;
    public const int StandingRowHeight = 7;
    public const int StandingNameChars = 10;
    public const int DialRadius = 20;
    public const float DialMaxKmh = 140f;
    private const int DialTicks = 9;
    private const int RedlineTicks = 2;
    private const float DialSweep = 270f * MathF.PI / 180f;
    public const int PowerUpBoxSize = 20;

    private readonly char[] _text = new char[32];

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Draw(IPixelTarget target, in HudState state)
    {
        var lapTop = DrawLapAndTime(target, state);
        DrawStandings(target, state, lapTop);
        DrawSpeedometer(target, state.SpeedKmh);
        DrawPowerUp(target, state.PowerUp);
        DrawAnnouncement(target, state);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <returns>The top of the lap line, for the standings to sit on.</returns>
    private int DrawLapAndTime(IPixelTarget target, in HudState state)
    {
        var big = PixelFont.GlyphHeight * LapScale;
        var y = target.Height - Margin - big;
        var x = Margin;
        if (state.LapCount > 1)
        {
            var length = TextFormat.Copy(_text, "LAP ");
            length += TextFormat.Ratio(_text.AsSpan(length), Math.Clamp(state.Lap, 1, state.LapCount), state.LapCount);
            x = PixelFont.DrawTextOutlined(target, x, y, _text.AsSpan(0, length), UiColors.Text, UiColors.TextShadow,
                LapScale) + 2 * LapScale;
            var clock = TextFormat.Time(_text, state.RaceSeconds);
            PixelFont.DrawTextOutlined(target, x, y + big - PixelFont.GlyphHeight, _text.AsSpan(0, clock), UiColors.Sky,
                UiColors.TextShadow);
        }
        else
        {
            var clock = TextFormat.Time(_text, state.RaceSeconds);
            PixelFont.DrawTextOutlined(target, x, y, _text.AsSpan(0, clock), UiColors.Text, UiColors.TextShadow, LapScale);
        }

        return y;
    }

    private void DrawStandings(IPixelTarget target, in HudState state, int lapTop)
    {
        var rows = state.Standings == null ? 0 : Math.Min(state.StandingCount, state.Standings.Length);
        if (rows == 0)
        {
            if (state.KartCount <= 1)
                return;

            var length = TextFormat.Copy(_text, "POS ");
            length += TextFormat.Ratio(_text.AsSpan(length), state.Position, state.KartCount);
            PixelFont.DrawTextOutlined(target, Margin, lapTop - Margin - PixelFont.GlyphHeight,
                _text.AsSpan(0, length), UiColors.Text, UiColors.TextShadow);
            return;
        }

        var nameX = Margin + PixelFont.Advance * 2 + 2;
        var timeX = nameX + PixelFont.Advance * (StandingNameChars + 1);
        var y = lapTop - Margin - rows * StandingRowHeight + (StandingRowHeight - PixelFont.GlyphHeight);
        for (var i = 0; i < rows; i++, y += StandingRowHeight)
        {
            ref readonly var row = ref state.Standings[i];
            var color = row.IsPlayer ? UiColors.Selected : UiColors.Text;
            var length = TextFormat.Int(_text, row.Position);
            PixelFont.DrawTextOutlined(target, Margin, y, _text.AsSpan(0, length), color, UiColors.TextShadow);

            var name = row.Name.AsSpan();
            PixelFont.DrawTextOutlined(target, nameX, y, name[..Math.Min(name.Length, StandingNameChars)], color,
                UiColors.TextShadow);

            length = row.Position == 1 ? TextFormat.Time(_text, row.Seconds) : TextFormat.Gap(_text, row.Seconds);
            PixelFont.DrawTextOutlined(target, timeX, y, _text.AsSpan(0, length), color, UiColors.TextShadow);
        }
    }

    private void DrawSpeedometer(IPixelTarget target, float speedKmh)
    {
        var centerX = target.Width - Margin - DialRadius;
        var centerY = target.Height - Margin - DialRadius;

        for (var i = 0; i < DialTicks; i++)
        {
            var angle = -DialSweep / 2 + DialSweep * i / (DialTicks - 1);
            var color = i >= DialTicks - RedlineTicks ? UiColors.Redline : UiColors.Text;
            DrawSpoke(target, centerX, centerY, angle, DialRadius - 3, DialRadius, color);
        }

        var share = Math.Clamp(speedKmh / DialMaxKmh, 0f, 1f); // pins above the top speed, never wraps
        DrawSpoke(target, centerX, centerY, -DialSweep / 2 + DialSweep * share, DialRadius / 3, DialRadius - 4,
            UiColors.Selected);

        var length = TextFormat.Int(_text, Math.Max(0, (int)MathF.Round(speedKmh)));
        PixelFont.DrawCentered(target, centerX + 1, centerY - 3, _text.AsSpan(0, length), UiColors.Text,
            UiColors.TextShadow, 2);
        PixelFont.DrawCentered(target, centerX + 1, centerY + 9, "KM/H", UiColors.Sky, UiColors.TextShadow);
    }

    /// <summary>A line out from the centre; angle 0 is straight up, positive clockwise.</summary>
    private static void DrawSpoke(IPixelTarget target, int centerX, int centerY, float angle, int from, int to, uint rgb)
    {
        var (sin, cos) = MathF.SinCos(angle);
        Shapes.Line(target,
            centerX + (int)MathF.Round(sin * from), centerY - (int)MathF.Round(cos * from),
            centerX + (int)MathF.Round(sin * to), centerY - (int)MathF.Round(cos * to), rgb);
    }

    private static void DrawPowerUp(IPixelTarget target, string powerUp)
    {
        if (string.IsNullOrEmpty(powerUp))
            return;

        var x = target.Width - Margin - PowerUpBoxSize;
        Shapes.FillRect(target, x, Margin, PowerUpBoxSize, PowerUpBoxSize, UiColors.Panel);
        Shapes.Frame(target, x, Margin, PowerUpBoxSize, PowerUpBoxSize, UiColors.Sky);
        PixelFont.DrawCentered(target, x + PowerUpBoxSize / 2 + 1, Margin + 5, powerUp.AsSpan(0, 1), UiColors.Selected,
            UiColors.TextShadow, 2);
        var nameY = Margin + PowerUpBoxSize + 3;
        PixelFont.DrawTextOutlined(target, target.Width - Margin - PixelFont.MeasureText(powerUp), nameY, powerUp,
            UiColors.Text, UiColors.TextShadow);
    }

    private void DrawAnnouncement(IPixelTarget target, in HudState state)
    {
        var centerX = target.Width / 2;
        if (state.Countdown > 0)
        {
            var length = TextFormat.Int(_text, state.Countdown);
            PixelFont.DrawCentered(target, centerX, 30, _text.AsSpan(0, length), UiColors.Selected, UiColors.TextShadow, 8);
        }
        else if (state.ShowGo)
        {
            PixelFont.DrawCentered(target, centerX, 30, "GO!", UiColors.Selected, UiColors.TextShadow, 8);
        }
        else if (state.Finished)
        {
            PixelFont.DrawCentered(target, centerX, 30, "FINISHED", UiColors.Selected, UiColors.TextShadow, 3);
        }
    }
}
