using ConsoleEngine.Ecs;
using Starguard.Components;

namespace Starguard.Systems;

/// <summary>
/// When the last alien is gone, starts the next wave: a fresh formation one row lower. Shields stay as damaged as
/// they are.
/// </summary>
public sealed class WaveSystem(Match match, Formation formation, Spawner spawner) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _aliens;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _aliens = World.Query(Query.With<Alien>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (match.IsOver || _aliens.Count > 0)
            return;

        match.NextWave();
        formation.Reset(spawner.CreateFormation(Commands, match.Wave));
    }
}
