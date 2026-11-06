using ConsoleEngine.Ecs.Rendering;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Components;

/// <summary>
/// A <see cref="SpriteLibrary"/> sprite drawn by <see cref="Systems.SpriteRenderSystem"/> with its top-left cell at
/// the entity's <see cref="Position"/> plus <see cref="Offset"/>. Change <see cref="Id"/> to swap the picture.
/// </summary>
public struct SpriteRef(int id, Point offset = default)
{
    public int Id = id;

    /// <summary>Cells added to the position when drawing, e.g. to draw a sprite around its collision box.</summary>
    public Point Offset = offset;
}
