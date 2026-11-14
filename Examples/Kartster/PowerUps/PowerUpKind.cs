namespace Kartster.PowerUps;

/// <summary>
/// The power-ups the port has: Kartster's <c>PowerUpKind</c> without the three that need height or only change a
/// player's screen (Trampoline, Bug Splat, Smudge) and the bike's Light Wall, which is in no set
/// (<see cref="PowerUpCatalog"/>).
/// </summary>
public enum PowerUpKind
{
    None,
    SpeedBoost,
    EngineTune,
    GripTune,
    Banana,
    Lapskaus,
    TripleLapskaus,
    HomingJoika,
    TripleHomingJoika,
    Invulnerable,
    Magnet,
    Anvil,
    Lightning,
    Vampire,
    Earthquake,
    Glitch,
    SmokeBomb,
    FakeFinish,
    FakePowerUp,
    Roadblock,
    ConcreteRoadblock,
    Locomotive,
}

/// <summary>Which way a use goes (Kartster's <c>PowerUpAim</c>): its own way, thrown ahead, dropped behind.</summary>
public enum PowerUpAim
{
    Default,
    Forward,
    Backward,
}
