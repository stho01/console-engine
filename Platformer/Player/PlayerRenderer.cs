using ConsoleEngine.Infrastructure;

namespace Platformer.Player
{
    public class PlayerRenderer
    {
        private readonly RenderConsole _console;

        private static readonly Sprite _playerSprite = Sprite.FromStringArray(new[]{
            " () ",
            "/##\\",
            " ## ",
            " /\\ "
        });
        
        public PlayerRenderer(RenderConsole console)
        {
            _console = console;
        }
        
        public void Draw(PlayerModel model)
        {
            _console.Draw(
                (int)model.Position.X,
                (int)model.Position.Y,
                _playerSprite
            );
        }
    }
}