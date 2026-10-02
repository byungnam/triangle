using Triangle.Core.Data;
using Triangle.Core.Items;

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
}
