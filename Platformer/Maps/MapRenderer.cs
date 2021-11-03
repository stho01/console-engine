using ConsoleEngine.Infrastructure;

namespace Platformer.Maps
{
    public class MapRenderer
    {
        private readonly RenderConsole _console;

        public MapRenderer(RenderConsole console)
        {
            _console = console;
        }

        public void Draw(IMap map) 
        {
            _console.Draw(0, 0, map.Tiles);
        }
    }
}