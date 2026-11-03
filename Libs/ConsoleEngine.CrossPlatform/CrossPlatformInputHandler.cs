using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ConsoleEngine.Abstractions.Inputs;

namespace ConsoleEngine.CrossPlatform;

/// <summary>
/// Keyboard input for non Windows platforms. Key codes are Windows virtual key codes, same as <see cref="Key"/>.
///
/// On macOS the real keyboard state is read through CoreGraphics. Everywhere else (or if macOS refuses to
/// report key state) keys are read from the terminal, which only reports presses and auto repeats, never
/// releases. A key read from the terminal is then held for a short tap, and again once its auto repeat starts
/// until the repeat stops. Pressed and Released fire once per key press.
/// </summary>
public class CrossPlatformInputHandler : IInputHandler
{
    // A terminal key is held until no new press/repeat arrives within this window. The first window has to
    // outlast the OS' "delay until repeat" (macOS ~375ms, X11 660ms by default), the following ones only the
    // repeat interval. Override the first with CONSOLE_ENGINE_REPEAT_DELAY (milliseconds) for longer delays.
    private const int DefaultInitialHoldMs = 750;
    private static readonly long InitialHoldTicks = Stopwatch.Frequency * ReadInitialHoldMs() / 1000;
    private static readonly long RepeatHoldTicks = Stopwatch.Frequency * 150 / 1000;

    // How long a terminal key press counts as held before its auto repeat starts. Until the first repeat
    // arrives a tap and a hold look the same, so without this every tap would last the whole initial window.
    private static readonly long TapTicks = Stopwatch.Frequency * 120 / 1000;

    // No OS repeats sooner than this after a key press, so an event before it is the key being pressed again.
    private static readonly long MinRepeatDelayTicks = Stopwatch.Frequency * 200 / 1000;

    // How far apart a terminal key press and the native key down may be to count as the same key press.
    private static readonly long CalibrationTicks = Stopwatch.Frequency * 300 / 1000;
    private const int MaxCalibrationMisses = 2;

    private readonly KeyState[] _keyStates = new KeyState[256];
    private readonly bool[] _nativeDown = new bool[256];
    private readonly long[] _terminalHeldUntil = new long[256];
    private readonly long[] _terminalPressedAt = new long[256];
    private readonly bool[] _terminalRepeating = new bool[256];
    private readonly bool[] _terminalPressed = new bool[256];
    private readonly bool[] _terminalActive = new bool[256];
    private readonly long[] _nativeLastDown = new long[256];
    private readonly List<(int Key, long Time)> _pendingCalibration = new();
    private bool _nativeSupported;
    private bool _nativeTrusted;
    private int _calibrationMisses;
    private long _lastUpdate;

    public CrossPlatformInputHandler()
    {
        var mode = Environment.GetEnvironmentVariable("CONSOLE_ENGINE_INPUT");
        _nativeSupported = !string.Equals(mode, "terminal", StringComparison.OrdinalIgnoreCase) && MacKeyboard.IsSupported();

        for (var i = 0; i < _keyStates.Length; i++)
            _keyStates[i].Index = i;
    }

    public KeyState GetKey(int id)
    {
        return _keyStates[id];
    }

    public IEnumerable<int> GetPressedKeyCodes() => _keyStates.Where(k => k.Pressed).Select(k => k.Index);

    public void Update()
    {
        var now = Stopwatch.GetTimestamp();

        // Always drain the terminal, otherwise key presses pile up in stdin (and get echoed when the game exits).
        ReadTerminalKeys(now);
        _lastUpdate = now;

        if (_nativeSupported)
        {
            MacKeyboard.Poll(_nativeDown);
            for (var i = 0; i < _nativeDown.Length; i++)
                if (_nativeDown[i])
                    _nativeLastDown[i] = now;

            if (!_nativeTrusted)
                Calibrate(now);
        }

        if (_nativeTrusted)
            UpdateFromNative();
        else
            UpdateFromTerminal(now);
    }

    private void UpdateFromNative()
    {
        for (var i = 0; i < _keyStates.Length; i++)
        {
            var wasHeld = _keyStates[i].Held;
            var isDown = _nativeDown[i];

            _keyStates[i].Pressed = isDown && !wasHeld;
            _keyStates[i].Released = !isDown && wasHeld;
            _keyStates[i].Held = isDown;
        }
    }

    private void UpdateFromTerminal(long now)
    {
        for (var i = 0; i < _keyStates.Length; i++)
        {
            // A key press is active until its hold window runs out, but only held during the tap and while repeating.
            var active = _terminalHeldUntil[i] > now;
            var held = active && (_terminalRepeating[i] || now - _terminalPressedAt[i] < TapTicks);

            _keyStates[i].Pressed = _terminalPressed[i];
            _keyStates[i].Released = _terminalActive[i] && !active;
            _keyStates[i].Held = held;
            _terminalActive[i] = active;
        }
    }

    /// <summary>
    /// Without the Input Monitoring permission macOS still reports modifier keys, but every other key as up.
    /// So the native key state is only trusted once it agrees with a key press read from the terminal, and
    /// dropped if it keeps missing them. Until then the terminal is used.
    /// </summary>
    private void Calibrate(long now)
    {
        for (var i = 0; i < _terminalPressed.Length; i++)
            if (_terminalPressed[i] && MacKeyboard.IsMapped(i))
                _pendingCalibration.Add((i, now));

        for (var i = _pendingCalibration.Count - 1; i >= 0; i--)
        {
            var (key, time) = _pendingCalibration[i];
            if (Math.Abs(_nativeLastDown[key] - time) < CalibrationTicks)
            {
                _nativeTrusted = true;
                _pendingCalibration.Clear();
                return;
            }

            if (now - time > CalibrationTicks)
            {
                _pendingCalibration.RemoveAt(i);
                if (++_calibrationMisses >= MaxCalibrationMisses)
                {
                    _nativeSupported = false;
                    _pendingCalibration.Clear();
                    return;
                }
            }
        }
    }

    private void ReadTerminalKeys(long now)
    {
        Array.Clear(_terminalPressed);

        if (Console.IsInputRedirected)
            return;

        try
        {
            while (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                var vk = ToVirtualKey(keyInfo);
                if (vk <= 0 || vk >= _terminalHeldUntil.Length)
                    continue;

                // Every event drained this frame shares one timestamp and may have arrived any time since the
                // last update, so the key counts as held if its window was still open back then.
                var held = _terminalHeldUntil[vk] > _lastUpdate;
                var isRepeat = held && (_terminalRepeating[vk] || now - _terminalPressedAt[vk] >= MinRepeatDelayTicks);
                if (isRepeat)
                {
                    _terminalRepeating[vk] = true;
                }
                else
                {
                    _terminalPressedAt[vk] = now;
                    _terminalRepeating[vk] = false;
                    _terminalPressed[vk] = true;
                }

                _terminalHeldUntil[vk] = now + (isRepeat ? RepeatHoldTicks : InitialHoldTicks);
            }
        }
        catch (InvalidOperationException)
        {
            // No console attached
        }
    }

    private static long ReadInitialHoldMs()
    {
        var setting = Environment.GetEnvironmentVariable("CONSOLE_ENGINE_REPEAT_DELAY");
        // Leave some headroom above the repeat delay for slow frames.
        return int.TryParse(setting, out var delay) && delay > 0 ? delay + 100 : DefaultInitialHoldMs;
    }

    private static int ToVirtualKey(ConsoleKeyInfo keyInfo)
    {
        // ConsoleKey values are Windows virtual key codes.
        if (keyInfo.Key != 0)
            return (int)keyInfo.Key;

        var c = char.ToUpperInvariant(keyInfo.KeyChar);
        return c is ' ' or >= '0' and <= '9' or >= 'A' and <= 'Z' ? c : 0;
    }
}
