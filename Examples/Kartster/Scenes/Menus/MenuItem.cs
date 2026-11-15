namespace Kartster.Scenes.Menus;

/// <summary>
/// One row of a <see cref="MenuScreen"/>: a label, and optionally values that left and right step through (drawn
/// "LABEL &lt; VALUE &gt;").
/// </summary>
public sealed class MenuItem(string label, string[] values = null, int valueIndex = 0)
{
    public string Label { get; } = label;

    /// <summary>Null for a plain item.</summary>
    public string[] Values { get; } = values;

    public int ValueIndex { get; set; } = valueIndex;

    public bool IsChangeable => Values is { Length: > 0 };

    public string Value => IsChangeable ? Values[ValueIndex] : null;
}
