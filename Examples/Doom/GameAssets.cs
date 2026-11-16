using System.Collections.Generic;
using Doom.Graphics;
using Doom.Maps;
using Doom.Wad;

namespace Doom;

/// <summary>Everything loaded from the WAD once at start-up, shared by every scene.</summary>
public sealed class GameAssets
{
    //**********************************************************
    //** props
    //**********************************************************

    public string WadPath { get; init; }
    public WadFile Wad { get; init; }
    public Palette Palette { get; init; }
    public TextureLibrary Textures { get; init; }
    public SpriteLibrary Sprites { get; init; }
    public ConsoleShades Shades { get; init; }

    /// <summary>The maps in the WAD, in order.</summary>
    public List<string> Maps { get; init; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public static GameAssets Load(string path)
    {
        var wad = WadFile.Load(path);
        var palette = Palette.Load(wad);
        return new GameAssets
        {
            WadPath = path,
            Wad = wad,
            Palette = palette,
            Textures = TextureLibrary.Load(wad),
            Sprites = SpriteLibrary.Load(wad),
            Shades = new ConsoleShades(palette),
            Maps = MapNames.Find(wad)
        };
    }

    /// <summary>
    /// The sky texture for a map: <c>SKY1</c>-<c>SKY4</c> by Doom episode, by map number for Doom II. Falls back to
    /// <c>SKY1</c>, or -1 when the WAD has no sky.
    /// </summary>
    public int SkyFor(string map)
    {
        var name = "SKY1";
        if (map.Length == 4 && map[0] == 'E')
            name = "SKY" + map[1];
        else if (map.Length == 5 && int.TryParse(map[3..], out var number))
            name = number < 12 ? "SKY1" : number < 21 ? "SKY2" : "SKY3";

        var texture = Textures.FindTexture(name);
        return texture >= 0 ? texture : Textures.FindTexture("SKY1");
    }
}
