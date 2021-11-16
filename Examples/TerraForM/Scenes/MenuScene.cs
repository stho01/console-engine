using System;
using System.Linq;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using TerraForM.Assets;

namespace TerraForM.Scenes
{
    public class MenuScene : Scene<TerraformGame>
    {
        public override void OnLoad()
        {
            if (!string.IsNullOrEmpty(Game.Playername))
            {
                var sprite = Sprites.Story;
                sprite.Draw(32, 22, string.Concat(Game.Playername.Take(30)), ConsoleColor.Red, ConsoleColor.Black);    
            }
        }

        public override void OnUnload() {}

        public override void OnUpdate()
        {
            if (Input.Instance.GetKey(Key.H).Pressed) 
            {
                if (Game.Scenes.Count == 1)
                    Game.Scenes.Set(new GameScene($"map{Game.CurrentMap}"));
                else
                    Game.Scenes.Pop();
            }
        }

        public override void OnRender()
        {
            Game.Console.Draw(3, 5, Sprites.Story);    
        }
    }
}