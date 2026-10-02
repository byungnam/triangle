using Microsoft.Xna.Framework;

namespace Triangle.Desktop;

internal static class Theme
{
    public static readonly Color Background = new(20, 22, 28);
    public static readonly Color Panel = new(30, 34, 43);
    public static readonly Color PanelBorder = new(48, 54, 66);
    public static readonly Color Text = new(230, 230, 230);
    public static readonly Color TextDim = new(138, 143, 152);

    public static readonly Color Ally = new(111, 168, 255);
    public static readonly Color Enemy = new(255, 122, 107);

    public static readonly Color Damage = new(255, 179, 107);
    public static readonly Color Heal = new(127, 220, 138);
    public static readonly Color Cover = new(224, 196, 108);
    public static readonly Color Death = new(170, 170, 170);

    public static readonly Color HpBar = new(92, 184, 92);
    public static readonly Color HpBarLow = new(217, 83, 79);
    public static readonly Color MpBar = new(86, 128, 214);
    public static readonly Color BarBack = new(15, 17, 21);

    public static readonly Color Button = new(44, 50, 62);
    public static readonly Color ButtonHover = new(58, 66, 82);
    public static readonly Color ButtonPressed = new(36, 41, 51);
    public static readonly Color Selected = new(52, 74, 112);
    public static readonly Color Accent = new(74, 128, 214);
    public static readonly Color AccentHover = new(94, 148, 234);
}
