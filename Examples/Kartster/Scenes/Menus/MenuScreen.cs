using System;
using Kartster.Ui;

namespace Kartster.Scenes.Menus;

/// <summary>What a key press did to a <see cref="MenuScreen"/>.</summary>
public enum MenuAction
{
    None,
    Moved,
    Changed,
    Confirm,
    Back
}

/// <summary>
/// A menu page in Kartster's look: a title on top, a centred vertical list with the selected item on a bar in yellow,
/// and a hint line at the bottom. Up/Down move (wrapping), Left/Right step a changeable item's value (wrapping),
/// Enter confirms, Esc goes back. The logic takes <see cref="MenuKeys"/>, so it runs without a console. Drawing
/// allocates nothing.
/// </summary>
public sealed class MenuScreen(string title, MenuItem[] items, string footer)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int TitleScale = 3;
    public const int TitleY = 10;
    public const int ItemScale = 2;
    public const int ItemRowHeight = 14;
    public const int ListTop = 44;
    private const int BarPadding = 4;

    //**********************************************************
    //** props
    //**********************************************************

    public string Title { get; } = title;
    public MenuItem[] Items { get; } = items;
    public string Footer { get; } = footer;
    public int Selected { get; set; }

    /// <summary>Where the list is centred; null: the middle of the target.</summary>
    public int? ListCenterX { get; set; }

    /// <summary>False when the menu sits over a 3D picture.</summary>
    public bool DrawBackground { get; set; } = true;

    //**********************************************************
    //** public methods
    //**********************************************************

    public MenuAction Update(MenuKeys keys)
    {
        if (keys.Back)
            return MenuAction.Back;
        if (keys.Confirm)
            return MenuAction.Confirm;
        if (Items.Length == 0)
            return MenuAction.None;

        if (keys.Up || keys.Down)
        {
            Selected = (Selected + (keys.Down ? 1 : -1) + Items.Length) % Items.Length;
            return MenuAction.Moved;
        }

        var item = Items[Selected];
        if ((keys.Left || keys.Right) && item.IsChangeable)
        {
            item.ValueIndex = (item.ValueIndex + (keys.Right ? 1 : -1) + item.Values.Length) % item.Values.Length;
            return MenuAction.Changed;
        }

        return MenuAction.None;
    }

    public void Draw(IPixelTarget target)
    {
        if (DrawBackground)
            DrawBackdrop(target);

        PixelFont.DrawCentered(target, target.Width / 2, TitleY, Title, UiColors.Text, UiColors.TextShadow, TitleScale);
        if (!string.IsNullOrEmpty(Footer))
        {
            PixelFont.DrawCentered(target, target.Width / 2, target.Height - PixelFont.GlyphHeight - 4, Footer,
                UiColors.Dim, UiColors.TextShadow);
        }

        var centerX = ListCenterX ?? target.Width / 2;
        var barWidth = 0;
        for (var i = 0; i < Items.Length; i++)
            barWidth = Math.Max(barWidth, PixelFont.MeasureChars(CharCount(Items[i]), ItemScale));
        barWidth += 2 * BarPadding;

        var visible = Math.Max(1, (target.Height - ListTop - 16) / ItemRowHeight);
        var first = Math.Clamp(Selected - visible / 2, 0, Math.Max(0, Items.Length - visible));
        var last = Math.Min(Items.Length, first + visible);
        for (var i = first; i < last; i++)
            DrawItem(target, Items[i], i == Selected, centerX, ListTop + (i - first) * ItemRowHeight, barWidth);
    }

    /// <summary>Kartster's menu background: deep navy at the top to dark cobalt at the bottom.</summary>
    public static void DrawBackdrop(IPixelTarget target)
        => Shapes.VerticalGradient(target, 0, 0, target.Width, target.Height, UiColors.BackgroundDeep,
            UiColors.BackgroundLight);

    //**********************************************************
    //** internals
    //**********************************************************

    private const string ValueOpen = "  < ";
    private const string ValueClose = " >";

    private static int CharCount(MenuItem item)
        => item.Label.Length + (item.IsChangeable ? ValueOpen.Length + item.Value.Length + ValueClose.Length : 0);

    private static void DrawItem(IPixelTarget target, MenuItem item, bool selected, int centerX, int y, int barWidth)
    {
        if (selected)
        {
            Shapes.FillRect(target, centerX - barWidth / 2, y - 2, barWidth,
                PixelFont.GlyphHeight * ItemScale + 4, UiColors.Bar);
        }

        var color = selected ? UiColors.Selected : UiColors.Text;
        var x = centerX - PixelFont.MeasureChars(CharCount(item), ItemScale) / 2;
        x = PixelFont.DrawTextOutlined(target, x, y, item.Label, color, UiColors.TextShadow, ItemScale);
        if (!item.IsChangeable)
            return;

        var valueColor = selected ? UiColors.Selected : UiColors.Sky;
        x = PixelFont.DrawTextOutlined(target, x, y, ValueOpen, valueColor, UiColors.TextShadow, ItemScale);
        x = PixelFont.DrawTextOutlined(target, x, y, item.Value, valueColor, UiColors.TextShadow, ItemScale);
        PixelFont.DrawTextOutlined(target, x, y, ValueClose, valueColor, UiColors.TextShadow, ItemScale);
    }
}
