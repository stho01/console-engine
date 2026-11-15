using System;
using Doom.Graphics;
using Doom.Play;

namespace Doom.Rendering;

/// <summary>
/// Draws the player's weapon (and its muzzle flash) over the view, from the weapon sprites in the WAD. Weapon
/// pictures carry their screen spot in their offsets, for a 320 × 200 screen; they are scaled to the view.
/// </summary>
public sealed class WeaponRenderer(FrameBuffer frame, Palette palette, SpriteLibrary sprites)
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Doom's weapon spot, in 320 × 200 pixels.</summary>
    private const float WeaponX = 1;

    private const float WeaponY = 32;
    private const int ReadyFrameTics = 8;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Draw(Player player, int tic, int lightLevel, int fixedLight)
    {
        if (player.Dead)
            return;

        var info = Weapons.Get(player.Weapon);
        var sprite = sprites.Find(info.Sprite);
        if (sprite == null)
            return;

        var bobX = player.Bob * 2 * MathF.Cos(player.BobPhase);
        var bobY = player.Bob * 2 * MathF.Abs(MathF.Sin(player.BobPhase));
        var firing = player.FireTics > 0 && info.FireFrames != null;
        var letter = firing
            ? Pick(info.FireFrames, 1 - player.FireTics / (float)Math.Max(1, info.RefireTics))
            : info.ReadyFrames[tic / ReadyFrameTics % info.ReadyFrames.Length];
        if (firing)
        {
            bobX = 0;
            bobY = 0;
        }

        var light = fixedLight >= 0 ? fixedLight : Math.Clamp((255 - lightLevel) >> 3, 0, Palette.LightLevels - 1);
        DrawPicture(sprite.Get(letter), bobX, bobY + player.WeaponOffset, light);

        if (player.FlashTics <= 0 || info.FlashSprite == null)
            return;

        var flash = sprites.Find(info.FlashSprite);
        if (flash == null)
            return;

        var flashLength = Math.Max(4, info.RefireTics / 2);
        var flashLetter = Pick(info.FlashFrames, 1 - player.FlashTics / (float)flashLength);
        DrawPicture(flash.Get(flashLetter), bobX, bobY + player.WeaponOffset, 0);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static char Pick(string frames, float progress)
    {
        var index = Math.Clamp((int)(progress * frames.Length), 0, frames.Length - 1);
        return frames[index];
    }

    private void DrawPicture(SpriteFrame spriteFrame, float offsetX, float offsetY, int light)
    {
        var picture = spriteFrame?.Get(0, out _);
        if (picture == null)
            return;

        var scale = frame.Width / 320f;
        var left = (WeaponX - picture.LeftOffset + offsetX) * scale;
        var top = (WeaponY - picture.TopOffset + offsetY) * scale;
        var x0 = Math.Max(0, (int)MathF.Ceiling(left - 0.5f));
        var x1 = Math.Min(frame.Width, (int)MathF.Ceiling(left + picture.Width * scale - 0.5f));
        var y0 = Math.Max(0, (int)MathF.Ceiling(top - 0.5f));
        var y1 = Math.Min(frame.Height, (int)MathF.Ceiling(top + picture.Height * scale - 0.5f));
        var colorMaps = palette.ColorMaps;
        var lightOffset = light * Palette.ColorCount;
        for (var x = x0; x < x1; x++)
        {
            var column = Math.Clamp((int)((x + 0.5f - left) / scale), 0, picture.Width - 1) * picture.Height;
            for (var y = y0; y < y1; y++)
            {
                var row = Math.Clamp((int)((y + 0.5f - top) / scale), 0, picture.Height - 1);
                if (!picture.Opaque[column + row])
                    continue;

                frame.Pixels[y * frame.Width + x] = colorMaps[lightOffset + picture.Pixels[column + row]];
            }
        }
    }
}
