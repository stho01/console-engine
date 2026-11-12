namespace Vikingferd.Puzzle;

/// <summary>What fills one tile of a <see cref="Stage"/>. The map characters are listed on <see cref="Levels"/>.</summary>
public enum TileKind : byte
{
    Air,

    /// <summary>Solid, forever.</summary>
    Rock,

    /// <summary>Solid until Gorm smashes it (E while facing it); then air.</summary>
    Cracked,

    /// <summary>Not solid. A hero touching the lower half is knocked out.</summary>
    Spikes,

    /// <summary>Not solid. The level is won when all three heroes stand on exit tiles.</summary>
    Exit,

    /// <summary>Solid while its channel is off, open while it is on.</summary>
    Door,

    /// <summary>The opposite of a door: open (not solid) while its channel is off, solid while it is on.</summary>
    Bridge,

    /// <summary>Not solid. Turns its channel on while a hero or crate stands on it.</summary>
    Plate,

    /// <summary>Not solid. A hero overlapping it presses E to switch its channel on or off.</summary>
    Lever
}
