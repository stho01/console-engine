using System;

namespace ConsoleEngine.CrossPlatform;

internal enum KeyEventType
{
    /// <summary>A key read from a terminal without the kitty protocol: a press or an auto repeat, it can't tell which.</summary>
    PressOrRepeat,
    Press,
    Repeat,
    Release
}

/// <summary>
/// The kitty keyboard protocol (https://sw.kovidgoyal.net/kitty/keyboard-protocol/), which makes terminals that
/// support it (kitty, Alacritty, WezTerm, Ghostty, foot, iTerm2 ...) report real press, repeat and release events.
/// </summary>
internal static class KittyKeyboard
{
    private const string Esc = "\u001b";

    /// <summary>Asks for the current flags, followed by a primary device attributes query every terminal answers.</summary>
    public const string Query = Esc + "[?u" + Esc + "[c";

    /// <summary>
    /// Pushes disambiguate (1), report event types (2) and report all keys as escape codes (8). Without 8 keys that
    /// type text are still sent as plain text, without release events.
    /// </summary>
    public const string Enable = Esc + "[>11u";

    public const string Disable = Esc + "[<u";

    public enum Reply
    {
        None,
        Key,
        Flags,
        DeviceAttributes
    }

    /// <summary>
    /// Parses a complete CSI sequence (starting with ESC [) into a Windows virtual key code and event type, or
    /// recognizes the replies to <see cref="Query"/>.
    /// </summary>
    public static Reply Parse(string sequence, out int virtualKey, out KeyEventType type, out bool ctrl)
    {
        virtualKey = 0;
        type = KeyEventType.Press;
        ctrl = false;

        if (sequence.Length < 3 || sequence[0] != '\u001b' || sequence[1] != '[')
            return Reply.None;

        var final = sequence[^1];
        var body = sequence.AsSpan(2, sequence.Length - 3);

        if (body.Length > 0 && body[0] == '?')
            return final switch { 'u' => Reply.Flags, 'c' => Reply.DeviceAttributes, _ => Reply.None };

        // CSI code[:alternates] ; modifiers[:event] final
        var separator = body.IndexOf(';');
        var codeField = separator < 0 ? body : body[..separator];
        var modifierField = separator < 0 ? ReadOnlySpan<char>.Empty : body[(separator + 1)..];

        var colon = codeField.IndexOf(':');
        if (colon >= 0)
            codeField = codeField[..colon];

        var code = codeField.IsEmpty ? 1 : int.TryParse(codeField, out var parsed) ? parsed : -1;
        if (code < 0)
            return Reply.None;

        var textField = modifierField.IndexOf(';');
        if (textField >= 0)
            modifierField = modifierField[..textField];

        var modifiers = 1;
        colon = modifierField.IndexOf(':');
        if (colon >= 0)
        {
            type = modifierField[(colon + 1)..] switch
            {
                "2" => KeyEventType.Repeat,
                "3" => KeyEventType.Release,
                _ => KeyEventType.Press
            };
            modifierField = modifierField[..colon];
        }

        if (!modifierField.IsEmpty)
            int.TryParse(modifierField, out modifiers);

        ctrl = ((modifiers - 1) & 4) != 0;

        virtualKey = final switch
        {
            'u' => FromCodepoint(code),
            '~' => FromTilde(code),
            'A' => 0x26, // up
            'B' => 0x28, // down
            'C' => 0x27, // right
            'D' => 0x25, // left
            'H' => 0x24, // home
            'F' => 0x23, // end
            'P' => 0x70, // F1
            'Q' => 0x71, // F2
            'S' => 0x73, // F4
            _ => 0
        };

        return virtualKey == 0 ? Reply.None : Reply.Key;
    }

    private static int FromCodepoint(int code) => code switch
    {
        >= 'a' and <= 'z' => code - 'a' + 'A',
        >= '0' and <= '9' or ' ' => code,
        13 => 0x0D,  // enter
        9 => 0x09,   // tab
        27 => 0x1B,  // escape
        127 or 8 => 0x08, // backspace
        >= 57399 and <= 57408 => 0x60 + code - 57399, // keypad 0-9
        57409 => 0x6E, // keypad decimal
        57410 => 0x6F, // keypad divide
        57411 => 0x6A, // keypad multiply
        57412 => 0x6D, // keypad subtract
        57413 => 0x6B, // keypad add
        57414 => 0x0D, // keypad enter
        57358 => 0x14, // caps lock
        57441 => 0xA0, // left shift
        57442 => 0xA2, // left control
        57443 => 0xA4, // left alt
        57444 => 0x5B, // left super
        57447 => 0xA1, // right shift
        57448 => 0xA3, // right control
        57449 => 0xA5, // right alt
        57450 => 0x5C, // right super
        < 128 => MacKeyboard.CharToVirtualKey((char)code),
        _ => 0
    };

    private static int FromTilde(int code) => code switch
    {
        2 => 0x2D,  // insert
        3 => 0x2E,  // delete
        5 => 0x21,  // page up
        6 => 0x22,  // page down
        7 => 0x24,  // home
        8 => 0x23,  // end
        >= 11 and <= 15 => 0x70 + code - 11, // F1-F5
        >= 17 and <= 21 => 0x75 + code - 17, // F6-F10
        23 => 0x7A, // F11
        24 => 0x7B, // F12
        _ => 0
    };
}
