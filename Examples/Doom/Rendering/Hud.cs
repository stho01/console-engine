using System;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Rendering;
using Doom.Play;

namespace Doom.Rendering;

/// <summary>
/// The text status bar below the view: health, armor, ammo, weapons, keys, the map name and level stats, the
/// renderer (V) with the frame rate, and the latest message. Numbers are written into a reused buffer, so drawing it
/// allocates nothing.
/// </summary>
public sealed class Hud
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const ConsoleColor LabelColor = ConsoleColor.DarkGray;
    private const ConsoleColor ValueColor = ConsoleColor.White;
    private const ConsoleColor LowColor = ConsoleColor.Red;
    private const ConsoleColor MessageColor = ConsoleColor.Yellow;
    private const ConsoleColor BarColor = ConsoleColor.DarkRed;

    /// <summary>Columns from the right edge where the renderer name and frame rate start.</summary>
    private const int ViewWidth = 26;

    private static readonly AmmoType[] AmmoTypes = [AmmoType.Bullets, AmmoType.Shells, AmmoType.Rockets, AmmoType.Cells];
    private static readonly string[] AmmoLabels = [DoomTexts.HudBullets, DoomTexts.HudShells, DoomTexts.HudRockets,
        DoomTexts.HudCells];

    private readonly char[] _number = new char[12];

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Draw(RenderConsole console, Level level, string mapName, int top, ViewMode viewMode = ViewMode.Bsp)
    {
        var player = level.Player;
        for (var x = 0; x < console.Width; x++)
            console.Draw(x, top, '─', BarColor);

        var row = top + 1;
        var x0 = 1;
        x0 = Label(console, x0, row, DoomTexts.HudHealth);
        x0 = Number(console, x0, row, player.Health, player.Health <= 25 ? LowColor : ValueColor, "%");
        x0 = Label(console, x0 + 3, row, DoomTexts.HudArmor);
        x0 = Number(console, x0, row, player.Armor, player.ArmorClass == 2 ? ConsoleColor.Cyan : ValueColor, "%");
        x0 = Label(console, x0 + 3, row, DoomTexts.HudAmmo);
        var weapon = Weapons.Get(player.Weapon);
        if (weapon.Ammo == AmmoType.None)
            console.Draw(x0, row, "--", ValueColor);
        else
        {
            var ammo = player.Ammo[(int)weapon.Ammo];
            x0 = Number(console, x0, row, ammo, ammo == 0 ? LowColor : ValueColor, "");
        }

        console.Draw(x0 + 4, row, weapon.Name, ConsoleColor.Green);

        row++;
        x0 = 1;
        for (var i = 0; i < AmmoTypes.Length; i++)
        {
            x0 = Label(console, x0, row, AmmoLabels[i]);
            x0 = Number(console, x0, row, player.Ammo[(int)AmmoTypes[i]], ValueColor, "/");
            x0 = Number(console, x0 - 1, row, player.MaxAmmoOf(AmmoTypes[i]), LabelColor, "") + 3;
        }

        row++;
        x0 = Label(console, 1, row, DoomTexts.HudKeys);
        x0 = Key(console, x0, row, player, KeyCards.Blue, ConsoleColor.Blue);
        x0 = Key(console, x0, row, player, KeyCards.Yellow, ConsoleColor.Yellow);
        x0 = Key(console, x0, row, player, KeyCards.Red, ConsoleColor.Red) + 4;
        for (var i = 0; i < Weapons.All.Length; i++)
        {
            if (!player.OwnedWeapons[i])
                continue;

            var info = Weapons.All[i];
            var color = info.Type == player.Weapon ? ConsoleColor.Green : LabelColor;
            console.Draw(x0, row, (char)('0' + info.Slot), color);
            x0 += 2;
        }

        row++;
        console.Draw(1, row, mapName, ConsoleColor.Gray);
        var stats = level.Stats;
        x0 = 8;
        x0 = Label(console, x0, row, "K");
        x0 = Number(console, x0, row, stats.Kills, ValueColor, "/");
        x0 = Number(console, x0 - 1, row, stats.TotalKills, ValueColor, "") + 2;
        x0 = Label(console, x0, row, "I");
        x0 = Number(console, x0, row, stats.Items, ValueColor, "/");
        x0 = Number(console, x0 - 1, row, stats.TotalItems, ValueColor, "") + 2;
        x0 = Label(console, x0, row, "S");
        x0 = Number(console, x0, row, stats.Secrets, ValueColor, "/");
        Number(console, x0 - 1, row, stats.TotalSecrets, ValueColor, "");

        x0 = Label(console, console.Width - ViewWidth, row, DoomTexts.HudView);
        var view = viewMode == ViewMode.Mesh ? DoomTexts.HudViewMesh : DoomTexts.HudViewBsp;
        console.Draw(x0, row, view, ConsoleColor.Cyan);
        Number(console, x0 + view.Length + 2, row, GameTime.Fps, ValueColor, DoomTexts.HudFps);

        row++;
        if (player.Dead)
            console.Draw(1, row, DoomTexts.HudDead, LowColor);
        else if (level.MessageLeft > 0 && level.Message != null)
            console.Draw(1, row, level.Message, MessageColor);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static int Label(RenderConsole console, int x, int y, string text)
    {
        console.Draw(x, y, text, LabelColor);
        return x + text.Length + 1;
    }

    private static int Key(RenderConsole console, int x, int y, Player player, KeyCards key, ConsoleColor color)
    {
        var has = (player.Keys & key) != 0;
        console.Draw(x, y, has ? '■' : '·', has ? color : LabelColor);
        return x + 2;
    }

    /// <summary>Writes a number followed by <paramref name="suffix"/>; returns the column after it.</summary>
    private int Number(RenderConsole console, int x, int y, int value, ConsoleColor color, string suffix)
    {
        if (!value.TryFormat(_number, out var length))
            length = 0;

        for (var i = 0; i < length; i++)
            console.Draw(x + i, y, _number[i], color);
        console.Draw(x + length, y, suffix, LabelColor);
        return x + length + suffix.Length + 1;
    }
}
