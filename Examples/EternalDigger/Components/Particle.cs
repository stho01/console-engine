namespace EternalDigger.Components;

/// <summary>Which look a particle has: its characters and colours over its life (see <c>ParticleStyleSystem</c>).</summary>
public enum ParticleStyle : byte
{
    Dirt,
    Stone,
    Granite,
    Copper,
    Silver,
    Gold,
    /// <summary>Grey puff that thins out (tile breaks, footsteps, landings).</summary>
    Dust,
    /// <summary>Bright metal spark from hitting ore.</summary>
    Spark,
    /// <summary>The twinkle above an ore vein.</summary>
    Glint,
    /// <summary>Slow underground mist in dug-out caves.</summary>
    Mist,
    Coin,
    /// <summary>The ring of stars when delivering or buying.</summary>
    Pop,
    /// <summary>Specks left below the miner on a top-speed climb.</summary>
    Trail,
    Obsidian,
    Slate,
    Quartzite,
    Gem,
    /// <summary>The one-frame full-tile flash of a blast.</summary>
    Flash,
    /// <summary>Hot rubble flung out by a blast.</summary>
    Blast,
    /// <summary>Grey smoke rising from a burning fuse.</summary>
    Smoke,
    /// <summary>A puff rising from a lamp low on oil.</summary>
    LampSmoke,
    /// <summary>The slow pulsing glint of a rare find.</summary>
    Pulse
}

/// <summary>
/// A short-lived effect cell. Uses the engine's <c>Position</c>, <c>Velocity</c> and <c>Glyph</c>; its glyph is
/// rewritten from <see cref="Style"/> and <see cref="Age"/> every frame. Destroyed when <see cref="Age"/> reaches
/// <see cref="Lifetime"/>.
/// </summary>
public struct Particle(ParticleStyle style, float lifetime)
{
    public float Age;
    public float Lifetime = lifetime;
    public ParticleStyle Style = style;

    /// <summary>A per-particle random byte to vary the characters within a style.</summary>
    public byte Variant;

    public readonly float Progress => Lifetime <= 0f ? 1f : Age / Lifetime;
}

/// <summary>Downward acceleration in cells/s², and how much speed is lost per second (0..1).</summary>
public struct Physics(float gravity, float drag)
{
    public float Gravity = gravity;
    public float Drag = drag;
}

/// <summary>Tag: the particle stops on solid tiles (debris lands on the tunnel floor, coins on the grass).</summary>
public struct TileCollider;

/// <summary>Tag: an ambient mist particle, counted to keep the mist population steady.</summary>
public struct Mist;

/// <summary>What a <see cref="FloatingText"/> says.</summary>
public enum FloatingTextKind : byte
{
    /// <summary><c>+150g</c> (yellow) or <c>-10g</c> (red): <see cref="FloatingText.Amount"/> in gold.</summary>
    Gold,
    /// <summary><c>ELEVATOR +10m</c>: a lift segment was built, <see cref="FloatingText.Amount"/> in metres.</summary>
    Elevator,
    /// <summary><c>TOO HARD</c> (red): the shovel hit obsidian. No amount.</summary>
    TooHard,
    /// <summary><c>FOSSIL!</c> or <c>RELIC!</c>: a rare find was dug up. <see cref="FloatingText.Amount"/> is its type.</summary>
    RareFind
}

/// <summary>
/// Floating text such as "+150g" or "-10g" that drifts up and fades. Uses <c>Position</c> and <c>Velocity</c>; the
/// text is built from <see cref="Kind"/> and <see cref="Amount"/> when drawn, without allocating.
/// </summary>
public struct FloatingText(FloatingTextKind kind, int amount, float lifetime)
{
    public FloatingTextKind Kind = kind;
    public int Amount = amount;
    public float Age;
    public float Lifetime = lifetime;
}
