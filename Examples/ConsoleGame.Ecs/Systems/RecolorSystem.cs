using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;

namespace ConsoleGame.Ecs.Systems;

/// <summary>Every 500 ms, gives every glyph a random colour from <see cref="Colors"/>.</summary>
public sealed class RecolorSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float IntervalSeconds = 0.5f;

    private static readonly ConsoleColor[] Colors =
    [
        ConsoleColor.Cyan, ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.White, ConsoleColor.DarkGray
    ];

    private readonly Random _random = new();
    private Query _glyphs;
    private float _elapsed;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _glyphs = World.Query(Query.With<Glyph>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _elapsed += deltaSeconds;
        if (_elapsed < IntervalSeconds)
            return;

        // Reset rather than subtract, like GameTime.Interval in the original.
        _elapsed = 0f;
        var job = new RecolorJob { Random = _random };
        _glyphs.ForEach<RecolorJob, Glyph>(ref job);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private struct RecolorJob : IForEach<Glyph>
    {
        public Random Random;

        public void Execute(ref Glyph glyph) => glyph.Foreground = Colors[Random.Next(Colors.Length)];
    }
}
