using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;

namespace Kartster.Scenes.Menus;

/// <summary>The menu keys pressed this frame. Scenes read them from <see cref="Input"/>; tests build them.</summary>
public readonly record struct MenuKeys(bool Up, bool Down, bool Left, bool Right, bool Confirm, bool Back)
{
    /// <summary>Arrows, Enter (confirm) and Esc (back), on the frame they go down.</summary>
    public static MenuKeys From(Input input) => new(
        input.GetKey(Key.UP).Pressed, input.GetKey(Key.DOWN).Pressed,
        input.GetKey(Key.LEFT).Pressed, input.GetKey(Key.RIGHT).Pressed,
        input.GetKey(Key.ENTER).Pressed, input.GetKey(Key.ESCAPE).Pressed);
}
