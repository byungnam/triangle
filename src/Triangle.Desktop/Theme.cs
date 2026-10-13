using Microsoft.Xna.Framework;

namespace Triangle.Desktop;

/// <summary>
/// 화면 색. 딥다크 중세 판타지(2026-10-03 테마)에 맞춘 어두운 갈색 바탕, 청동 테두리, 양피지색 글자 (2026-10-13).
/// </summary>
internal static class Theme
{
    public static readonly Color Background = new(15, 12, 10);
    public static readonly Color Panel = new(30, 25, 21);
    public static readonly Color PanelBorder = new(92, 72, 46);
    public static readonly Color Text = new(236, 226, 204);
    public static readonly Color TextDim = new(152, 140, 120);

    /// <summary>테두리 장식과 강조에 쓰는 금색.</summary>
    public static readonly Color Gold = new(206, 166, 96);

    public static readonly Color Ally = new(132, 174, 228);
    public static readonly Color Enemy = new(230, 106, 90);

    public static readonly Color Damage = new(240, 168, 96);
    public static readonly Color Heal = new(138, 208, 122);
    public static readonly Color Cover = new(226, 190, 110);
    public static readonly Color Death = new(160, 154, 146);

    public static readonly Color HpBar = new(112, 168, 76);
    public static readonly Color HpBarLow = new(200, 70, 56);
    public static readonly Color MpBar = new(80, 120, 200);
    public static readonly Color BarBack = new(11, 9, 8);

    public static readonly Color Button = new(50, 41, 33);
    public static readonly Color ButtonHover = new(74, 60, 45);
    public static readonly Color ButtonPressed = new(38, 31, 25);
    public static readonly Color Selected = new(96, 72, 40);
    public static readonly Color Accent = new(140, 44, 34);
    public static readonly Color AccentHover = new(172, 60, 46);

    /// <summary>전위·후위 꼬리표 바탕.</summary>
    public static readonly Color FrontRow = new(150, 74, 40);
    public static readonly Color BackRow = new(52, 84, 132);

    /// <summary>원소 색 (아이콘에 입힌다).</summary>
    public static readonly Color Fire = new(240, 122, 64);
    public static readonly Color Frost = new(130, 196, 240);
    public static readonly Color Lightning = new(196, 176, 255);
    public static readonly Color Holy = new(244, 218, 140);
}
