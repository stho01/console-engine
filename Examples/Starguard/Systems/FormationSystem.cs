using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using Starguard.Components;

namespace Starguard.Systems;

/// <summary>
/// Marches every alien in lockstep (see <see cref="Formation"/>) and flips their animation frame on each step. If a
/// step leaves an alien at the cannon's row, the aliens have invaded and the match is over.
/// </summary>
public sealed class FormationSystem(Match match, Formation formation, Playfield playfield) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _aliens;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _aliens = World.Query(Query.With<Alien, Position, Hitbox>().With<SpriteRef>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (match.IsOver)
            return;

        var alive = _aliens.Count;
        if (alive == 0)
            return;

        formation.SinceLastStep += deltaSeconds;
        if (formation.SinceLastStep < formation.StepSeconds(alive))
            return;

        // Reset rather than subtract: after a long frame the formation takes one step, not several.
        formation.SinceLastStep = 0f;
        var step = NextStep();
        formation.Frame ^= 1;

        var bottom = float.MinValue;
        foreach (var chunk in _aliens.GetChunks())
        {
            var aliens = chunk.GetSpan<Alien>();
            var positions = chunk.GetSpan<Position>();
            var hitboxes = chunk.GetSpan<Hitbox>();
            var sprites = chunk.GetSpan<SpriteRef>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].Value += step;
                sprites[i].Id = aliens[i].FirstFrame + formation.Frame;
                bottom = MathF.Max(bottom, positions[i].Value.Y + hitboxes[i].Height);
            }
        }

        if (bottom > playfield.CannonTop)
            match.Invade();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Sideways in the current direction, or down and turn around if that would cross an edge.</summary>
    private Vector2 NextStep()
    {
        var left = float.MaxValue;
        var right = float.MinValue;
        foreach (var chunk in _aliens.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var hitboxes = chunk.GetSpan<Hitbox>();
            for (var i = 0; i < chunk.Count; i++)
            {
                left = MathF.Min(left, positions[i].Value.X);
                right = MathF.Max(right, positions[i].Value.X + hitboxes[i].Width);
            }
        }

        var sideways = formation.Direction * Formation.StepCells;
        if (left + sideways >= 0 && right + sideways <= playfield.Width)
            return new Vector2(sideways, 0);

        formation.Direction = -formation.Direction;
        return new Vector2(0, Formation.DropCells);
    }
}
