namespace Kartster.PowerUps;

/// <summary>
/// The power-ups the port has: the subset of Kartster's <c>PowerUpKind</c> that acts on karts on a flat road (boosts,
/// tunes, bananas, cans). The rest of Kartster's normal set is left out of the draw (<see cref="PowerUpCatalog"/>).
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
}

/// <summary>Which way a use goes (Kartster's <c>PowerUpAim</c>): its own way, thrown ahead, dropped behind.</summary>
public enum PowerUpAim
{
    Default,
    Forward,
    Backward,
}
