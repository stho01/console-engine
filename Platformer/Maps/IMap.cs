using System;
using Microsoft.Xna.Framework;

namespace Platformer.Maps
{
    public interface IMap
    {
        public Span<string> Tiles { get; }
    }
}