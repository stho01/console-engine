using Microsoft.Xna.Framework;

namespace Platformer.Maps
{
    public interface IMap
    {
        public string[] Tiles { get; }
        public Rectangle[] BoundingBoxes { get; }
    }
}