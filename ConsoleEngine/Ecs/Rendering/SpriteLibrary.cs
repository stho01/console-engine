using System;
using System.Collections.Generic;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Ecs.Rendering;

/// <summary>
/// Sprites shared by many entities, referenced by <see cref="Components.SpriteRef.Id"/>. Components can't hold class
/// references, so they store the index this returns. Ids are handed out in order from 0. Fill it once at startup:
/// adding allocates.
/// </summary>
public sealed class SpriteLibrary
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly List<Sprite> _sprites = new();

    //**********************************************************
    //** props
    //**********************************************************

    public int Count => _sprites.Count;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Adds a sprite and returns its id: the number of sprites added before it.</summary>
    public int Add(Sprite sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        _sprites.Add(sprite);
        return _sprites.Count - 1;
    }

    public Sprite Get(int id) => _sprites[id];
}
