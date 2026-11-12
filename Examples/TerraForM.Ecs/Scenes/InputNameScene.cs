using System;
using System.Text;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Infrastructure.Scenery;
using TerraForM.Assets;

namespace TerraForM.Ecs.Scenes;

/// <summary>
/// First screen: type a name (space, digits and upper-case letters), <c>BACKSPACE</c> deletes, <c>ENTER</c> accepts and
/// opens the menu. A plain scene, as in the original.
/// </summary>
public class InputNameScene : Scene<TerraformEcsGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int NameRow = 32;

    private readonly StringBuilder _name = new();
    private Sprite _screen;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad()
    {
        _name.Clear();
        _screen = Assets.Load<Sprite>(SpritePaths.InputName);
    }

    public override void OnUnload() => _name.Clear();

    public override void OnUpdate()
    {
        var input = Input.Instance;
        if (input.GetKey(Key.ENTER).Pressed)
        {
            Game.PlayerName = _name.ToString();
            Game.Scenes.Set<MenuScene>();
        }

        foreach (var keyCode in input.GetPressedKeyCodesSpace09AZ())
            _name.Append((char)keyCode);

        if (input.GetKey(Key.BACKSPACE).Pressed && _name.Length > 0)
            _name.Length--;
    }

    public override void OnRender()
    {
        var sprite = _screen;
        Game.Console.Draw(3, 5, sprite);

        var x = sprite.Width / 2 - _name.Length / 2;
        for (var i = 0; i < _name.Length; i++)
            Game.Console.Draw(x + i, NameRow, _name[i], ConsoleColor.Red);
    }
}
