using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;

namespace EternalDigger;

/// <summary>
/// The keys that drive the miner this frame, read once from the keyboard. Separate from the rules in
/// <c>MinerControlSystem</c>, so tests can play frames without a keyboard.
/// </summary>
public readonly struct MinerInput
{
    /// <summary>-1 left, 1 right, 0 neither (or both).</summary>
    public int Horizontal { get; init; }

    public bool Down { get; init; }
    public bool Up { get; init; }

    /// <summary>Up / Down went down this frame: they move the selection at the shop counter.</summary>
    public bool UpPressed { get; init; }

    public bool DownPressed { get; init; }

    /// <summary>The ladder key went down this frame.</summary>
    public bool PlaceLadder { get; init; }

    /// <summary>The rescue key is held.</summary>
    public bool Rescue { get; init; }

    /// <summary>The use key (<c>E</c>) is held: underground, holding it plants dynamite.</summary>
    public bool Use { get; init; }

    /// <summary>The use key went down this frame: deliver, buy, call the lift.</summary>
    public bool UsePressed { get; init; }

    /// <summary>Arrows and WASD, <c>SPACE</c> for a ladder, <c>R</c> for the rescue, <c>E</c> to use.</summary>
    public static MinerInput FromKeyboard()
    {
        var input = Input.Instance;
        var horizontal = 0;
        if (input.GetKey(Key.LEFT).Held || input.GetKey(Key.A).Held) horizontal -= 1;
        if (input.GetKey(Key.RIGHT).Held || input.GetKey(Key.D).Held) horizontal += 1;

        return new MinerInput
        {
            Horizontal = horizontal,
            Down = input.GetKey(Key.DOWN).Held || input.GetKey(Key.S).Held,
            Up = input.GetKey(Key.UP).Held || input.GetKey(Key.W).Held,
            UpPressed = input.GetKey(Key.UP).Pressed || input.GetKey(Key.W).Pressed,
            DownPressed = input.GetKey(Key.DOWN).Pressed || input.GetKey(Key.S).Pressed,
            PlaceLadder = input.GetKey(Key.SPACE).Pressed,
            Rescue = input.GetKey(Key.R).Held,
            Use = input.GetKey(Key.E).Held,
            UsePressed = input.GetKey(Key.E).Pressed
        };
    }
}
