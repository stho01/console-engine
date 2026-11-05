using Microsoft.Xna.Framework;
using TerraForM.Ecs.Assets;
using TerraForM.Ecs.Components;

namespace TerraForM.Ecs;

/// <summary>Collision boxes. Positions are truncated to whole cells, as in the original.</summary>
public static class Bounds
{
    public static Rectangle OfTile(Vector2 position, in MapTile tile) =>
        new((int)position.X, (int)position.Y, tile.Size, tile.Size);

    /// <summary>
    /// The rover's box is its current sprite's size: 4×6 facing north or south, 6×4 east or west.
    /// <paramref name="spriteId"/> is a <c>SpriteRef.Id</c>: a <see cref="SpriteId"/> cast to int.
    /// </summary>
    public static Rectangle OfRover(Vector2 position, int spriteId) =>
        new(position.ToPoint(), SpriteSheet.Get((SpriteId)spriteId).Size);
}
