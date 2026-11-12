using System;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;
using Vikingferd.Components;
using Vikingferd.Puzzle;
using Vikingferd.Rendering;

namespace Vikingferd.Systems;

/// <summary>
/// Draws crates, then heroes (so a hero in front of a crate is visible). A hero's pixel art stands on the bottom of
/// its box, centred, in a pose for standing, walking, jumping or gliding, mirrored when facing left. A bobbing
/// <c>▼</c> marks the hero the player controls; a knocked-out hero turns grey with stars circling overhead.
/// </summary>
public sealed class ActorRenderSystem(Canvas canvas, Expedition expedition) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int TilePixels = Layout.TilePixels;

    private readonly Point _origin = Layout.StagePixelOrigin(expedition.Stage.Columns, expedition.Stage.Rows);
    private Query _actors;
    private float _time;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _actors = World.Query(Query.With<Actor>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        DrawAll(heroes: false);
        DrawAll(heroes: true);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawAll(bool heroes)
    {
        foreach (var chunk in _actors.GetChunks())
        {
            var actors = chunk.GetSpan<Actor>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (Heroes.IsHero(actors[i].Kind) != heroes)
                    continue;

                if (heroes)
                    DrawHero(actors[i]);
                else
                    Looks.Crate.Draw(canvas, ToPixel(actors[i].Min.X) + _origin.X,
                        ToPixel(actors[i].Min.Y) + _origin.Y);
            }
        }
    }

    private void DrawHero(in Actor hero)
    {
        var centerX = _origin.X + ToPixel(hero.Min.X + hero.Size.X / 2f);
        var bottom = _origin.Y + ToPixel(hero.Min.Y + hero.Size.Y);
        var x = centerX - Looks.HeroWidth / 2;
        var y = bottom - Looks.HeroHeight;
        var knockedOut = expedition.Outcome == Outcome.Failed && expedition.FailedHero == hero.Kind;

        Looks.HeroSprite(hero).Draw(canvas, x, y, mirror: hero.Facing < 0, dim: knockedOut);

        var headRow = y >> 1;
        if (knockedOut)
        {
            var swing = (int)MathF.Round(MathF.Cos(_time * 6f) * 2f);
            canvas.Glyph(centerX + swing, headRow - 1, Looks.DizzyStar, ConsoleColor.Yellow);
            canvas.Glyph(centerX - swing - 1, headRow - 1, Looks.DizzyStar, ConsoleColor.White);
        }
        else if (hero.Kind == expedition.ActiveHero && expedition.Outcome == Outcome.Playing)
        {
            var bob = MathF.Sin(_time * 5f) > 0f ? 1 : 0;
            canvas.Glyph(centerX - 1, headRow - 2 + bob, Looks.ActiveMarker, Looks.HeroColor(hero.Kind));
        }
    }

    private static int ToPixel(float tiles) => (int)MathF.Round(tiles * TilePixels);
}
