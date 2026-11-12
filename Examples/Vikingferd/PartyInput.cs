using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;

namespace Vikingferd;

/// <summary>
/// The keys that drive the party this frame, read once from the keyboard. Separate from <c>PartyControlSystem</c>,
/// so tests can play without a keyboard. Restart, next level and quit are read by <see cref="VikingferdGame"/>.
/// </summary>
public readonly struct PartyInput
{
    /// <summary>-1 left, 1 right, 0 neither (or both).</summary>
    public int Horizontal { get; init; }

    public bool JumpPressed { get; init; }
    public bool JumpHeld { get; init; }
    public bool UsePressed { get; init; }

    /// <summary>TAB: control the next hero.</summary>
    public bool NextHero { get; init; }

    /// <summary>Keys 1-3: the hero's number (position in <c>Heroes.Order</c> + 1), or 0 for none.</summary>
    public int SelectHero { get; init; }

    /// <summary>Arrows or A D to run, SPACE / W / ↑ to jump, E to use, TAB or 1-3 to switch hero.</summary>
    public static PartyInput FromKeyboard()
    {
        var input = Input.Instance;
        var horizontal = 0;
        if (input.GetKey(Key.LEFT).Held || input.GetKey(Key.A).Held) horizontal -= 1;
        if (input.GetKey(Key.RIGHT).Held || input.GetKey(Key.D).Held) horizontal += 1;

        var select = 0;
        if (input.GetKey(Key.K1).Pressed) select = 1;
        else if (input.GetKey(Key.K2).Pressed) select = 2;
        else if (input.GetKey(Key.K3).Pressed) select = 3;

        return new PartyInput
        {
            Horizontal = horizontal,
            JumpPressed = input.GetKey(Key.SPACE).Pressed || input.GetKey(Key.W).Pressed ||
                          input.GetKey(Key.UP).Pressed,
            JumpHeld = input.GetKey(Key.SPACE).Held || input.GetKey(Key.W).Held || input.GetKey(Key.UP).Held,
            UsePressed = input.GetKey(Key.E).Pressed,
            NextHero = input.GetKey(Key.TAB).Pressed,
            SelectHero = select
        };
    }
}
