using System;
using System.Runtime.InteropServices;
using ConsoleEngine.Abstractions.Inputs;

namespace ConsoleEngine.CrossPlatform;

/// <summary>
/// Reads the real up/down state of the keyboard on macOS through CoreGraphics, the macOS equivalent of
/// Win32 GetAsyncKeyState. Terminals only report key presses (plus auto repeat), never releases, so this
/// is what makes Held/Released behave like on Windows.
/// </summary>
internal static class MacKeyboard
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const int CombinedSessionState = 0; // kCGEventSourceStateCombinedSessionState

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool CGEventSourceKeyState(int stateId, ushort key);

    /// <summary>Windows virtual key code (index) to macOS virtual key codes (kVK_*).</summary>
    private static readonly ushort[][] KeyMap = BuildKeyMap();

    public static bool IsSupported()
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        try
        {
            CGEventSourceKeyState(CombinedSessionState, 0);
            return true;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static bool IsMapped(int vk) => vk >= 0 && vk < KeyMap.Length && KeyMap[vk].Length > 0;

    /// <summary>Writes the current down state of every mapped key into <paramref name="down"/>, indexed by Windows virtual key code.</summary>
    /// <returns>true if any key is down.</returns>
    public static bool Poll(bool[] down)
    {
        var any = false;
        for (var vk = 0; vk < KeyMap.Length && vk < down.Length; vk++)
        {
            var isDown = false;
            foreach (var macKey in KeyMap[vk])
            {
                if (CGEventSourceKeyState(CombinedSessionState, macKey))
                {
                    isDown = true;
                    break;
                }
            }

            down[vk] = isDown;
            any |= isDown;
        }

        return any;
    }

    private static ushort[][] BuildKeyMap()
    {
        var map = new ushort[256][];
        for (var i = 0; i < map.Length; i++)
            map[i] = Array.Empty<ushort>();

        void Map(int vk, params ushort[] macKeys) => map[vk] = macKeys;

        // Letters (ANSI positions)
        Map((int)Key.A, 0x00); Map((int)Key.S, 0x01); Map((int)Key.D, 0x02); Map((int)Key.F, 0x03);
        Map((int)Key.H, 0x04); Map((int)Key.G, 0x05); Map((int)Key.Z, 0x06); Map((int)Key.X, 0x07);
        Map((int)Key.C, 0x08); Map((int)Key.V, 0x09); Map((int)Key.B, 0x0B); Map((int)Key.Q, 0x0C);
        Map((int)Key.W, 0x0D); Map((int)Key.E, 0x0E); Map((int)Key.R, 0x0F); Map((int)Key.Y, 0x10);
        Map((int)Key.T, 0x11); Map((int)Key.O, 0x1F); Map((int)Key.U, 0x20); Map((int)Key.I, 0x22);
        Map((int)Key.P, 0x23); Map((int)Key.L, 0x25); Map((int)Key.J, 0x26); Map((int)Key.K, 0x28);
        Map((int)Key.N, 0x2D); Map((int)Key.M, 0x2E);

        // Digits
        Map((int)Key.K1, 0x12); Map((int)Key.K2, 0x13); Map((int)Key.K3, 0x14); Map((int)Key.K4, 0x15);
        Map((int)Key.K5, 0x17); Map((int)Key.K6, 0x16); Map((int)Key.K7, 0x1A); Map((int)Key.K8, 0x1C);
        Map((int)Key.K9, 0x19); Map((int)Key.K0, 0x1D);

        // Function keys
        Map((int)Key.F1, 0x7A); Map((int)Key.F2, 0x78); Map((int)Key.F3, 0x63); Map((int)Key.F4, 0x76);
        Map((int)Key.F5, 0x60); Map((int)Key.F6, 0x61); Map((int)Key.F7, 0x62); Map((int)Key.F8, 0x64);
        Map((int)Key.F9, 0x65); Map((int)Key.F10, 0x6D); Map((int)Key.F11, 0x67); Map((int)Key.F12, 0x6F);

        // Navigation / editing
        Map((int)Key.ENTER, 0x24, 0x4C); // return, keypad enter
        Map((int)Key.ESCAPE, 0x35);
        Map((int)Key.SPACE, 0x31);
        Map((int)Key.BACKSPACE, 0x33);
        Map((int)Key.DELETE, 0x75);
        Map((int)Key.INSERT, 0x72); // help key sits where insert is on full size keyboards
        Map((int)Key.HOME, 0x73);
        Map((int)Key.END, 0x77);
        Map((int)Key.PRIOR, 0x74);
        Map((int)Key.NEXT, 0x79);
        Map((int)Key.LEFT, 0x7B);
        Map((int)Key.RIGHT, 0x7C);
        Map((int)Key.DOWN, 0x7D);
        Map((int)Key.UP, 0x7E);
        Map(0x09, 0x30); // VK_TAB

        // Modifiers
        Map(0x10, 0x38, 0x3C); // VK_SHIFT
        Map(0x11, 0x3B, 0x3E); // VK_CONTROL
        Map(0x12, 0x3A, 0x3D); // VK_MENU (option)
        Map(0x14, 0x39);       // VK_CAPITAL
        Map(0x5B, 0x37);       // VK_LWIN (command)
        Map(0x5C, 0x36);       // VK_RWIN (right command)
        Map(0xA0, 0x38); Map(0xA1, 0x3C); // VK_LSHIFT, VK_RSHIFT
        Map(0xA2, 0x3B); Map(0xA3, 0x3E); // VK_LCONTROL, VK_RCONTROL
        Map(0xA4, 0x3A); Map(0xA5, 0x3D); // VK_LMENU, VK_RMENU

        // Numpad
        Map((int)Key.NUMPAD0, 0x52); Map((int)Key.NUMPAD1, 0x53); Map((int)Key.NUMPAD2, 0x54);
        Map((int)Key.NUMPAD3, 0x55); Map((int)Key.NUMPAD4, 0x56); Map((int)Key.NUMPAD5, 0x57);
        Map((int)Key.NUMPAD6, 0x58); Map((int)Key.NUMPAD7, 0x59); Map((int)Key.NUMPAD8, 0x5B);
        Map((int)Key.NUMPAD9, 0x5C);
        Map((int)Key.MULTIPLY, 0x43); Map((int)Key.ADD, 0x45); Map((int)Key.SUBTRACT, 0x4E);
        Map((int)Key.DECIMAL, 0x41); Map((int)Key.DIVIDE, 0x4B);

        // OEM / punctuation (US layout positions)
        Map(0xBA, 0x29); // ;
        Map(0xBB, 0x18); // =
        Map(0xBC, 0x2B); // ,
        Map(0xBD, 0x1B); // -
        Map(0xBE, 0x2F); // .
        Map(0xBF, 0x2C); // /
        Map(0xC0, 0x32); // `
        Map(0xDB, 0x21); // [
        Map(0xDC, 0x2A); // \
        Map(0xDD, 0x1E); // ]
        Map(0xDE, 0x27); // '

        return map;
    }
}
