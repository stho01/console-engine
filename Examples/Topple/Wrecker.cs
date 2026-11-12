using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;
using Topple.Components;
using Topple.Effects;

namespace Topple;

/// <summary>Breaks blocks: scores them once, destroys them and shows it (<see cref="EffectSpawner"/>).</summary>
public sealed class Wrecker(Round round, EffectSpawner effects)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float StoneShake = .25f;
    private const float OtherShake = .1f;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Breaks the block, unless it is already broken.</summary>
    /// <param name="sinks">It fell into water: a splash instead of splinters.</param>
    public void Wreck(CommandBuffer commands, Entity entity, ref Block block, Vector2 position, bool sinks = false)
    {
        if (block.IsWrecked)
            return;

        block.IsWrecked = true;
        block.Health = 0f;
        round.AddWreck(block.Material);
        commands.Destroy(entity);
        if (sinks)
        {
            effects.Splash(commands, position);
            return;
        }

        var shake = block.Material == Material.Stone ? StoneShake : OtherShake;
        effects.Shatter(commands, position, Materials.Get(block.Material).Color, shake);
    }
}
