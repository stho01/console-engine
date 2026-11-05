using System;

namespace ConsoleEngine.Ecs;

/// <summary>
/// Handle to an entity in a <see cref="World"/>. 8 bytes: an id (slot in the world's entity table) and a version.
/// Destroying an entity bumps the slot's version, so old handles to a reused id stop being alive.
/// </summary>
public readonly struct Entity : IEquatable<Entity>
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Never alive: versions start at 1.</summary>
    public static readonly Entity Null = default;

    public readonly int Id;
    public readonly int Version;

    //**********************************************************
    //** ctor
    //**********************************************************

    internal Entity(int id, int version)
    {
        Id = id;
        Version = version;
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    public bool Equals(Entity other) => Id == other.Id && Version == other.Version;
    public override bool Equals(object obj) => obj is Entity other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Id, Version);
    public override string ToString() => $"Entity({Id}v{Version})";

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}
