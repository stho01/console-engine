using ConsoleEngine.Ecs;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Saving;

namespace EternalDigger.Systems;

/// <summary>
/// Writes the save when gameplay asked for one this frame (<see cref="GameSaver.Request"/>), and every
/// <see cref="GameSaver.AutosaveInterval"/> seconds spent underground. Runs last in the Simulation group, so the
/// save holds the frame's final state. Costs one query per frame; file I/O only on a trigger.
/// </summary>
public sealed class SaveSystem(GameSaver saver) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _miners;
    private float _undergroundSinceSave;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _miners.GetChunks())
        {
            ref var miner = ref chunk.GetSpan<Miner>()[0];
            if (Mine.Depth(miner.Row) > 0)
                _undergroundSinceSave += deltaSeconds;

            var autosave = GameSaver.AutosaveInterval > 0f && _undergroundSinceSave >= GameSaver.AutosaveInterval;
            if (!saver.IsRequested && !autosave)
                return;

            saver.Save(miner);
            _undergroundSinceSave = 0f;
            return;
        }
    }
}
