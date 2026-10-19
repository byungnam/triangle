using Microsoft.Xna.Framework;
using Triangle.Core.Actions;
using Triangle.Core.Data;
using Triangle.Core.Items;

namespace Triangle.Desktop.Rendering;

/// <summary>
/// 게임 대상 → 아이콘 이름(Icons/*.png)과 색. 아이콘은 흰색이고 여기서 정한 색을 입힌다.
/// 표에 없는 무기 종류는 계열 이름으로, 그것도 없으면 장비 아이콘으로 보인다.
/// </summary>
internal static class GameIcons
{
    private static readonly Dictionary<string, string> WeaponTypes = new()
    {
        ["한손검"] = "weapon.sword",
        ["양손검"] = "weapon.greatsword",
        ["한손창"] = "weapon.spear",
        ["양손창"] = "weapon.halberd",
        ["한손도끼"] = "weapon.hatchet",
        ["양손도끼"] = "weapon.greataxe",
        ["한손둔기"] = "weapon.mace",
        ["양손둔기"] = "weapon.warhammer",
        ["단궁"] = "weapon.shortbow",
        ["장궁"] = "weapon.longbow",
        ["석궁"] = "weapon.crossbow",
        ["쇠뇌"] = "weapon.arbalest",
        ["슬링"] = "weapon.sling",
        ["머스킷"] = "weapon.musket",
        ["핸드캐논"] = "weapon.handcannon",
        ["방패"] = "offhand.shield",
        ["마법책"] = "offhand.tome",
        ["성물"] = "offhand.relic",
    };

    /// <summary>계열 → 대표 무기 아이콘 (숙련 목록 등).</summary>
    private static readonly Dictionary<string, string> Masteries = new()
    {
        ["sword"] = "weapon.sword",
        ["spear"] = "weapon.spear",
        ["axe"] = "weapon.greataxe",
        ["mace"] = "weapon.mace",
        ["bow"] = "weapon.longbow",
        ["crossbow"] = "weapon.crossbow",
        ["sling"] = "weapon.sling",
        ["firearm"] = "weapon.musket",
        ["fire"] = "weapon.staff",
        ["frost"] = "weapon.staff",
        ["lightning"] = "weapon.staff",
        ["restoration"] = "weapon.holywand",
        ["renewal"] = "weapon.holywand",
        ["warding"] = "weapon.holywand",
        ["blessing"] = "weapon.holywand",
        ["shield"] = "offhand.shield",
        ["tome"] = "offhand.tome",
        ["relic"] = "offhand.relic",
        ["plate"] = "Body.plate",
        ["leather"] = "Body.leather",
        ["cloth"] = "Body.cloth",
    };

    private static readonly HashSet<string> Melee = ["sword", "spear", "axe", "mace"];
    private static readonly HashSet<string> Ranged = ["bow", "crossbow", "sling", "firearm"];
    private static readonly HashSet<string> Elements = ["fire", "frost", "lightning"];
    private static readonly HashSet<string> Healing = ["restoration", "renewal", "warding", "blessing"];

    /// <summary>계열의 색: 원소와 신성은 그 색, 나머지는 글자색.</summary>
    public static Color MasteryColor(string? mastery) => mastery switch
    {
        "fire" => Theme.Fire,
        "frost" => Theme.Frost,
        "lightning" => Theme.Lightning,
        _ when mastery is not null && Healing.Contains(mastery) => Theme.Holy,
        "relic" => Theme.Holy,
        _ => Theme.Text,
    };

    public static (string Icon, Color Color) Item(ItemDefinition item)
    {
        if (item.Slot == EquipmentSlot.Trinket)
        {
            return ("trinket", Theme.Gold);
        }

        var color = MasteryColor(item.Mastery);
        if (item.Slot.IsArmor() && item.Mastery is { } material)
        {
            return ($"{item.Slot}.{material}", color);
        }

        var type = item.Type ?? "";
        if (WeaponTypes.TryGetValue(type, out var icon))
        {
            return (icon, color);
        }

        if (type.EndsWith("지팡이"))
        {
            return ("weapon.staff", color);
        }

        if (type.EndsWith("봉"))
        {
            return (item.Mastery is { } m && Elements.Contains(m) ? "weapon.wand" : "weapon.holywand", color);
        }

        return (Mastery(item.Mastery), color);
    }

    public static string Mastery(string? mastery) => mastery is not null && Masteries.TryGetValue(mastery, out var icon) ? icon : "equipment";

    /// <summary>빈 부위 아이콘 (흐리게 그린다).</summary>
    public static string EmptySlot(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MainHand => "weapon.sword",
        EquipmentSlot.OffHand => "offhand.shield",
        EquipmentSlot.Head => "Head.plate",
        EquipmentSlot.Body => "Body.plate",
        EquipmentSlot.Feet => "Feet.plate",
        _ => "equipment",
    };

    /// <summary>주무기 계열로 고른 초상.</summary>
    public static (string Icon, Color Color) Portrait(string? weaponMastery) => weaponMastery switch
    {
        null => ("portrait.none", Theme.TextDim),
        _ when Melee.Contains(weaponMastery) => ("portrait.melee", Theme.Text),
        _ when Ranged.Contains(weaponMastery) => ("portrait.ranged", Theme.Text),
        _ when Elements.Contains(weaponMastery) => ("portrait.mage", MasteryColor(weaponMastery)),
        _ when Healing.Contains(weaponMastery) => ("portrait.healer", Theme.Holy),
        _ => ("portrait.none", Theme.Text),
    };

    /// <summary>행동 아이콘: 효과 종류, 원소 태그, 주는 무기(원거리)로 고른다.</summary>
    public static (string Icon, Color Color) Action(ActionDefinition action, GameData data)
    {
        var element = action.Tags.FirstOrDefault(Elements.Contains) ?? (action.Weapon is { } w && Elements.Contains(w) ? w : null);
        switch (action.Effect)
        {
            case ActionEffect.Heal:
                return ("action.heal", Theme.Heal);
            case ActionEffect.Shield:
                return ("action.shield", Theme.Holy);
            case ActionEffect.RestoreMp:
                return ("action.mana", Theme.Ally);
            case ActionEffect.Summon:
                return ("action.summon", MasteryColor(element));
            case ActionEffect.None:
                return action.Side == TargetSide.Enemy ? ("action.debuff", Theme.Enemy) : ("action.buff", Theme.Cover);
        }

        if (element is not null)
        {
            return ($"action.{element}", MasteryColor(element));
        }

        if (action.DamageType == DamageType.Magical)
        {
            return ("action.magic", Theme.Lightning);
        }

        var source = data.ItemsGranting(action.Id).Select(i => i.Mastery).FirstOrDefault();
        if (source == "firearm")
        {
            return ("action.shot", Theme.Damage);
        }

        if (source is not null && Ranged.Contains(source))
        {
            return ("action.ranged", Theme.Damage);
        }

        return (action.Scope == TargetScope.All ? "action.sweep" : "action.melee", Theme.Damage);
    }
}
