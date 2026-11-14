using ConsoleEngine.Ecs;

namespace PhysicsShowcase;

/// <summary>What the systems of the showcase share: the sensor zone and the counters and switches on the HUD.</summary>
public sealed class ShowcaseState
{
    /// <summary>The static sensor whose overlaps are counted.</summary>
    public Entity Zone { get; set; }

    /// <summary>Bodies inside the zone: sensor begin events minus end events.</summary>
    public int InZone { get; set; }

    /// <summary>Contact begin events since the scene started.</summary>
    public int Impacts { get; set; }

    /// <summary>Whether bodies are drawn interpolated between fixed steps.</summary>
    public bool Smoothing { get; set; } = true;
}
