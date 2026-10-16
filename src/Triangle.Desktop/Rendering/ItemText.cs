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

    public static string RarityName(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => "마법",
        ItemRarity.Rare => "희귀",
        ItemRarity.Legendary => "전설",
        _ => "일반",
    };

    /// <summary>등급 색: 일반 흐림, 마법 보라, 희귀 노랑, 전설 주황 (티어 색과 겹치지 않게).</summary>
    public static Color RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => Theme.Lightning,
        ItemRarity.Rare => Theme.Holy,
        ItemRarity.Legendary => Theme.Fire,
        _ => Theme.TextDim,
    };

    /// <summary>옵션 하나, 예: "검 위력 +7%". 모르는 옵션은 ID.</summary>
    public static string Option(ItemOption option, ItemDefinition item, GameData data) =>
        data.Affixes.TryGetValue(option.AffixId, out var affix)
            ? SkillText.BonusLabel(affix.Kind, option.Percent, affix.TagFor(item), data)
            : option.AffixId;

    /// <summary>"검 위력 +7%, 방어 +6%"처럼 옵션 모두 (없으면 빈 문자열).</summary>
    public static string Options(ItemInstance gear, GameData data) =>
        data.Items.TryGetValue(gear.ItemId, out var item) ? string.Join(", ", gear.Affixes.Select(o => Option(o, item, data))) : "";

    /// <summary>"희귀 T2 강철 검 Lv3"처럼 등급(일반은 빼고), 티어, 이름, 아이템 레벨.</summary>
    public static string PieceName(ItemInstance gear, GameData data)
    {
        var item = data.Items[gear.ItemId];
        var rarity = gear.Rarity == ItemRarity.Common ? "" : RarityName(gear.Rarity) + " ";
        return $"{rarity}{ShortName(item)} Lv{gear.Level}";
    }

    /// <summary>"희귀 T2 강철 검 Lv3 (검 위력 +7%, 방어 +6%)"처럼 이름과 옵션.</summary>
    public static string PieceSummary(ItemInstance gear, GameData data) =>
        gear.Affixes.Count == 0 ? PieceName(gear, data) : $"{PieceName(gear, data)} ({Options(gear, data)})";

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
    /// <param name="gear">장비 한 점이면 등급, 아이템 레벨, 무작위 옵션도 보여 준다.</param>
    public static IReadOnlyList<TextLine> Details(
        ItemDefinition item, GameData data, int? masteryLevel = null, SkillSet? skills = null, bool withActions = true, ItemInstance? gear = null)
    {
        var lines = new List<TextLine>
        {
            new($"T{item.Tier} {item.Name}", TierColor(item.Tier), 19, Bold: true),
            new($"{SlotLabel(item.Slot)} · {Kind(item, data)}", Theme.TextDim, 14),
        };

        if (gear is not null)
        {
            lines.Add(new($"{RarityName(gear.Rarity)} · 아이템 레벨 {gear.Level}", RarityColor(gear.Rarity), 15, Bold: gear.Rarity != ItemRarity.Common));
            foreach (var option in gear.Affixes)
            {
                lines.Add(new($"옵션: {Option(option, item, data)}", RarityColor(gear.Rarity)));
            }
        }

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
