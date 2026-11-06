using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ConsoleEngine.CrossPlatform;

internal readonly record struct TerminalKeyEvent(int Key, KeyEventType Type, long Time);

/// <summary>
/// Reads keys from the terminal on a background thread, so every event gets the time it arrived and Ctrl+C keeps
/// working while the game loop is stuck. Switches the terminal to the kitty keyboard protocol when it supports it.
/// </summary>
internal sealed class TerminalKeyReader
{
    private const int SigInt = 2;
    private const int MaxSequenceLength = 32;

    // How long to wait for the rest of an escape sequence before taking a lone ESC as the Escape key.
    private static readonly TimeSpan EscapeTimeout = TimeSpan.FromMilliseconds(20);

    private readonly ConcurrentQueue<TerminalKeyEvent> _events = new();
    private readonly StringBuilder _sequence = new();
    private readonly bool _kittyAllowed;
    private Stream _stdout;
    private volatile bool _kittyActive;

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);

    public TerminalKeyReader(bool kittyAllowed)
    {
        _kittyAllowed = kittyAllowed;
    }

    /// <summary>The terminal reports real press, repeat and release events.</summary>
    public bool KittyActive => _kittyActive;

    public void Start()
    {
        if (Console.IsInputRedirected)
            return;

        if (_kittyAllowed && !Console.IsOutputRedirected)
        {
            _stdout = Console.OpenStandardOutput();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => DisableKitty();
            AppDomain.CurrentDomain.UnhandledException += (_, _) => DisableKitty();
            WriteControl(KittyKeyboard.Query);
        }

        new Thread(Read) { Name = "Terminal input", IsBackground = true }.Start();
    }

    public bool TryDequeue(out TerminalKeyEvent keyEvent) => _events.TryDequeue(out keyEvent);

    private void Read()
    {
        try
        {
            var sequenceStarted = 0L;
            while (true)
            {
                if (!Console.KeyAvailable)
                {
                    if (_sequence.Length == 1 && Stopwatch.GetElapsedTime(sequenceStarted) > EscapeTimeout)
                        FlushSequenceAsKeys();

                    Thread.Sleep(1);
                    continue;
                }

                var keyInfo = Console.ReadKey(intercept: true);
                var now = Stopwatch.GetTimestamp();

                // .NET passes escape sequences it doesn't know (like the kitty protocol's) through one character at a time.
                if (_sequence.Length == 0 && keyInfo.KeyChar == '\u001b')
                {
                    _sequence.Append('\u001b');
                    sequenceStarted = now;
                    continue;
                }

                if (_sequence.Length > 0)
                {
                    if (_sequence.Length == 1 && keyInfo.KeyChar != '[')
                    {
                        FlushSequenceAsKeys();
                    }
                    else
                    {
                        _sequence.Append(keyInfo.KeyChar);
                        if (keyInfo.KeyChar is >= '@' and <= '~' && _sequence.Length > 2)
                            HandleSequence(now);
                        else if (_sequence.Length > MaxSequenceLength)
                            _sequence.Clear();
                        continue;
                    }
                }

                // A key .NET recognized itself. With the kitty protocol on these are presses sent in the short form (CSI A).
                var vk = ToVirtualKey(keyInfo);
                if (vk > 0)
                    _events.Enqueue(new TerminalKeyEvent(vk, _kittyActive ? KeyEventType.Press : KeyEventType.PressOrRepeat, now));
            }
        }
        catch (InvalidOperationException)
        {
            // No console attached
        }
    }

    private void HandleSequence(long now)
    {
        var sequence = _sequence.ToString();
        _sequence.Clear();

        switch (KittyKeyboard.Parse(sequence, out var vk, out var type, out var ctrl))
        {
            // Only terminals that support the protocol answer the flags query. Every terminal answers the device
            // attributes query sent after it, which is just swallowed here.
            case KittyKeyboard.Reply.Flags:
                WriteControl(KittyKeyboard.Enable);
                _kittyActive = true;
                break;

            case KittyKeyboard.Reply.Key when ctrl && vk == 'C':
                // Ctrl+C is a key event in the kitty protocol instead of SIGINT, raise it like the terminal would.
                if (type == KeyEventType.Press)
                    kill(Environment.ProcessId, SigInt);
                break;

            case KittyKeyboard.Reply.Key:
                _events.Enqueue(new TerminalKeyEvent(vk, _kittyActive ? type : KeyEventType.PressOrRepeat, now));
                break;
        }
    }

    private void FlushSequenceAsKeys()
    {
        // Not an escape sequence after all: a lone Escape key press.
        _sequence.Clear();
        _events.Enqueue(new TerminalKeyEvent(0x1B, _kittyActive ? KeyEventType.Press : KeyEventType.PressOrRepeat, Stopwatch.GetTimestamp()));
    }

    private void DisableKitty()
    {
        if (_kittyActive)
            WriteControl(KittyKeyboard.Disable);
    }

    private void WriteControl(string sequence)
    {
        try
        {
            _stdout?.Write(Encoding.ASCII.GetBytes(sequence));
            _stdout?.Flush();
        }
        catch (IOException)
        {
            // stdout might already be gone during shutdown
        }
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
