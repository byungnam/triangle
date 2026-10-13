using Microsoft.Xna.Framework;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Skills;

namespace Triangle.Desktop.Rendering;

/// <summary>아이템과 장비의 화면 표시.</summary>
internal static class ItemText
{
    public static string SlotLabel(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MainHand => "주무기",
        EquipmentSlot.OffHand => "보조",
        EquipmentSlot.Head => "머리",
        EquipmentSlot.Body => "몸통",
        EquipmentSlot.Feet => "신발",
        _ => "재료",
    };

    /// <summary>"방어 +10%, 최대 HP +5%"처럼 아이템 보너스.</summary>
    public static string Bonuses(ItemDefinition item, GameData data) => Bonuses(item.Bonuses, data);

    public static string Bonuses(IEnumerable<ItemBonus> bonuses, GameData data) =>
        string.Join(", ", bonuses.Select(b => SkillText.BonusLabel(b.Kind, b.Percent, b.Tag, data)));

    /// <summary>"T2 강철 검"처럼 티어를 붙인 이름. 재료는 이름만.</summary>
    public static string ShortName(ItemDefinition item) => item.IsEquipment ? $"T{item.Tier} {item.Name}" : item.Name;

    /// <summary>"T1 판금 갑옷 (판금, 두손)"처럼 티어, 이름, 계열.</summary>
    public static string Title(ItemDefinition item, GameData data)
    {
        var tags = new List<string>();
        if (item.Mastery is not null)
        {
            tags.Add(data.Masteries[item.Mastery].Name);
        }

        if (item.TwoHanded)
        {
            tags.Add("두손");
        }

        var suffix = tags.Count > 0 ? $" ({string.Join(", ", tags)})" : "";
        return item.IsEquipment ? $"T{item.Tier} {item.Name}{suffix}" : item.Name;
    }

    /// <summary>"요구: 활 숙련 Lv3"처럼 착용 조건 (없으면 빈 문자열).</summary>
    public static string Requirements(ItemDefinition item, GameData data) =>
        item.Requirements.Count == 0
            ? ""
            : "요구: " + string.Join(", ", item.Requirements.Select(r => $"{data.Skills[r.SkillId].Name} Lv{r.Level}"));

    /// <summary>"낡은 검 / 나무 방패 / 판금 투구 / ..."처럼 부위 순서로 낀 아이템. 없으면 "장비 없음".</summary>
    public static string Gear(IReadOnlyDictionary<EquipmentSlot, string> equipment, GameData data)
    {
        var names = EquipmentSlots.All.Where(equipment.ContainsKey).Select(s => data.Items[equipment[s]].Name).ToList();
        return names.Count == 0 ? "장비 없음" : string.Join(" / ", names);
    }

    /// <summary>티어 색: T1 회색, T2 초록, T3 파랑, T4 금색.</summary>
    public static Color TierColor(int tier) => tier switch
    {
        <= 1 => Theme.Text,
        2 => Theme.Heal,
        3 => Theme.Ally,
        _ => Theme.Cover,
    };

    /// <summary>"한손검 · 검 · 한손"처럼 종류, 계열, 손.</summary>
    public static string Kind(ItemDefinition item, GameData data)
    {
        var parts = new List<string>();
        if (item.Type is { } type)
        {
            parts.Add(type);
        }

        if (item.Mastery is { } mastery && data.Masteries.TryGetValue(mastery, out var m) && m.Name != item.Type)
        {
            parts.Add($"{m.Name} 계열");
        }

        if (item.Slot == EquipmentSlot.MainHand)
        {
            parts.Add(item.TwoHanded ? "두손" : "한손");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// 아이템 설명 카드: 이름, 종류, 보너스(숙련 반영), 무기 위력 배율, 요구 패시브, 주는 행동, 가격.
    /// <paramref name="masteryLevel"/>은 보는 사람의 그 계열 숙련 레벨 (null이면 기본 보너스).
    /// </summary>
    public static IReadOnlyList<TextLine> Details(ItemDefinition item, GameData data, int? masteryLevel = null, SkillSet? skills = null, bool withActions = true)
    {
        var lines = new List<TextLine>
        {
            new($"T{item.Tier} {item.Name}", TierColor(item.Tier), 19, Bold: true),
            new($"{SlotLabel(item.Slot)} · {Kind(item, data)}", Theme.TextDim, 14),
        };

        if (item.Bonuses.Count > 0)
        {
            var bonuses = item.BonusesAt(masteryLevel ?? 0);
            var power = masteryLevel is > 0 ? $" (숙련 Lv{masteryLevel}: +{masteryLevel * ItemDefinition.PowerPercentPerMasteryLevel}%)" : "";
            lines.Add(new($"보너스: {Bonuses(bonuses, data)}{power}", Theme.Text));
        }

        if (item.PowerMultiplierPercent != 100)
        {
            var mastery = item.Mastery is { } id && data.Masteries.TryGetValue(id, out var m) ? m.Name : "";
            lines.Add(new($"무기 위력 ×{item.PowerMultiplierPercent / 100.0:0.##} ({mastery} 행동의 피해·회복·소환)", Theme.Damage));
        }

        if (item.EffectDurationBonus > 0)
        {
            lines.Add(new($"거는 효과 지속 +{item.EffectDurationBonus}턴", Theme.Damage));
        }

        if (item.TwoHanded)
        {
            lines.Add(new("두손 무기: 보조 칸을 쓸 수 없다", Theme.TextDim));
        }

        if (item.Requirements.Count > 0)
        {
            var met = skills?.Meets(item.Requirements);
            lines.Add(new($"{Requirements(item, data)}{(met == false ? " (모자람)" : "")}", met == false ? Theme.Enemy : Theme.TextDim));
        }

        if (withActions && item.Actions.Count > 0)
        {
            lines.Add(new($"행동 {item.Actions.Count}개: {string.Join(", ", item.Actions.Select(a => data.Actions[a].Name))}", Theme.Ally));
        }

        if (item.Price > 0)
        {
            lines.Add(new($"가격 {item.Price} 골드", Theme.TextDim, 14));
        }

        return lines;
    }

    /// <summary>툴팁용: 설명 카드를 여러 줄 글로.</summary>
    public static string Tooltip(IEnumerable<TextLine> lines) => string.Join("\n", lines.Select(l => l.Text));
}
