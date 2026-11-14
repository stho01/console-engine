using System;
using System.Globalization;

namespace Kartster.Ui;

/// <summary>
/// Writes numbers into a caller-owned <see cref="Span{T}"/> of chars, so the HUD builds its text without allocating.
/// Each method returns how many chars it wrote; draw <c>buffer[..length]</c>. A buffer that is too short throws.
/// </summary>
public static class TextFormat
{
    /// <summary>"42", "-7".</summary>
    public static int Int(Span<char> destination, int value)
    {
        if (!value.TryFormat(destination, out var written, default, CultureInfo.InvariantCulture))
            throw new ArgumentException("The buffer is too short.", nameof(destination));
        return written;
    }

    /// <summary>A clock, minutes:seconds.tenths: "1:23.4", "0:05.0". Tenths are cut, not rounded; below 0 is 0.</summary>
    public static int Time(Span<char> destination, float seconds)
    {
        var tenths = seconds > 0 ? (int)(seconds * 10) : 0;
        var length = Int(destination, tenths / 600);
        var wholeSeconds = tenths / 10 % 60;
        destination[length++] = ':';
        destination[length++] = (char)('0' + wholeSeconds / 10);
        destination[length++] = (char)('0' + wholeSeconds % 10);
        destination[length++] = '.';
        destination[length++] = (char)('0' + tenths % 10);
        return length;
    }

    /// <summary>A gap behind the leader: "+1:23.4".</summary>
    public static int Gap(Span<char> destination, float seconds)
    {
        destination[0] = '+';
        return 1 + Time(destination[1..], seconds);
    }

    /// <summary>"2/3".</summary>
    public static int Ratio(Span<char> destination, int value, int total)
    {
        var length = Int(destination, value);
        destination[length++] = '/';
        return length + Int(destination[length..], total);
    }

    /// <summary>Copies <paramref name="text"/> to the start of <paramref name="destination"/>.</summary>
    public static int Copy(Span<char> destination, ReadOnlySpan<char> text)
    {
        text.CopyTo(destination);
        return text.Length;
    }
}
