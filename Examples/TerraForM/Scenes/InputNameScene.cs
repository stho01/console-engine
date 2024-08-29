using System;
using System.Linq;
using System.Text;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using TerraForM.Assets;

namespace TerraForM.Scenes;

public class InputNameScene : Scene<TerraformGame>
{
    private StringBuilder _nameBuilder;
        
    public override void OnLoad()
    {
        _nameBuilder = new();
    }

    public override void OnUnload()
    {
        _nameBuilder.Clear();
        _nameBuilder = null;
    }

    public override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.ENTER).Pressed)
        {
            Game.Playername = _nameBuilder.ToString();
            Game.Scenes.Set<MenuScene>();
        }
            
        var pressedSpace09AZkeys = Input.Instance.GetPressedKeyCodesSpace09AZ().Select(kc => (char)kc);
        if (pressedSpace09AZkeys?.Any() == true)
            _nameBuilder.Append(string.Concat(pressedSpace09AZkeys));

        if (Input.Instance.GetKey(Key.BACKSPACE).Pressed && _nameBuilder.Length > 0)
            _nameBuilder.Length--;
    }

    public override void OnRender()
    {
        var sprite = Sprites.InputName;
        Game.Console.Draw(3, 5, sprite);
            
        if (_nameBuilder.Length > 0)
        {
            Game.Console.Draw(
                sprite.Width / 2 - (_nameBuilder.Length/2), 
                32,  
                _nameBuilder.ToString(), 
                ConsoleColor.Red);
        }
    }
}