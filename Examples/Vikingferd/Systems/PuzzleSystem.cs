using System;
using ConsoleEngine.Ecs;
using Vikingferd.Components;
using Vikingferd.Puzzle;

namespace Vikingferd.Systems;

/// <summary>
/// One fixed step of <see cref="PuzzleRules"/>. The rules need every actor at once (heroes push crates and stand on
/// each other), so the actors are copied into a reused buffer, stepped, and copied back in the same order. Stops
/// once the attempt is won or failed, so the scene freezes under the message.
/// </summary>
public sealed class PuzzleSystem(Expedition expedition) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _actors;
    private Actor[] _buffer = new Actor[16];

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _actors = World.Query(Query.With<Actor>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (expedition.Outcome != Outcome.Playing)
            return;

        var count = 0;
        foreach (var chunk in _actors.GetChunks())
        {
            if (count + chunk.Count > _buffer.Length)
                Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, count + chunk.Count));

            chunk.GetSpan<Actor>().CopyTo(_buffer.AsSpan(count));
            count += chunk.Count;
        }

        expedition.Apply(PuzzleRules.Step(expedition.Stage, _buffer.AsSpan(0, count), deltaSeconds,
            expedition.Events));

        var offset = 0;
        foreach (var chunk in _actors.GetChunks())
        {
            _buffer.AsSpan(offset, chunk.Count).CopyTo(chunk.GetSpan<Actor>());
            offset += chunk.Count;
        }
    }
}
