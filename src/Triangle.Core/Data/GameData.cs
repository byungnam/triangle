using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Effects;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Skills;

namespace Triangle.Core.Data;

/// <summary>검증을 마친 게임 데이터. <see cref="GameDataLoader"/>로 만든다.</summary>
public sealed class GameData
{
    internal GameData(
        IReadOnlyDictionary<string, MasteryDefinition> masteries,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, ActionDefinition> actions,
        IReadOnlyDictionary<string, EffectDefinition> effects,
        IReadOnlyDictionary<string, EncounterDefinition> encounters,
        IReadOnlyDictionary<string, ItemDefinition> items)
    {
        Items = items;
        Effects = effects;
        Masteries = masteries;
        Skills = skills;
        Actions = actions;
        Encounters = encounters;
        Catalog = new CombatCatalog(actions, skills) { Effects = effects };
    }

    /// <summary>장비 계열이자 숙련 트리.</summary>
    public IReadOnlyDictionary<string, MasteryDefinition> Masteries { get; }

    public IEnumerable<MasteryDefinition> MasteriesFor(EquipmentSlot slot) => Masteries.Values.Where(m => m.Slot == slot);

    /// <summary>장비 아이템. 계열은 <see cref="ItemDefinition.Mastery"/>.</summary>
    public IReadOnlyDictionary<string, ItemDefinition> Items { get; }

    /// <summary>그 슬롯에 끼는 아이템 (데이터 순서).</summary>
    public IEnumerable<ItemDefinition> ItemsFor(EquipmentSlot slot) => Items.Values.Where(i => Masteries[i.Mastery].Slot == slot);

    /// <summary>아이템의 장비 계열. null이면 null(맨손·맨몸).</summary>
    public string? MasteryOf(string? itemId) => itemId is null ? null : Items[itemId].Mastery;

    /// <summary>그 계열의 기본 아이템 (데이터에서 처음 나오는 것). 없으면 null.</summary>
    public ItemDefinition? BasicItemFor(string masteryId) => Items.Values.FirstOrDefault(i => i.Mastery == masteryId);

    /// <summary>숙련 트리의 패시브 스킬.</summary>
    public IReadOnlyDictionary<string, SkillDefinition> Skills { get; }

    /// <summary>버프·디버프·지속 피해·지속 회복.</summary>
    public IReadOnlyDictionary<string, EffectDefinition> Effects { get; }

    /// <summary>전술에서 쓰는 행동.</summary>
    public IReadOnlyDictionary<string, ActionDefinition> Actions { get; }

    public IReadOnlyDictionary<string, EncounterDefinition> Encounters { get; }

    /// <summary>전투 시뮬레이터에 넘기는 정의 묶음.</summary>
    public CombatCatalog Catalog { get; }

    /// <summary>적 팀 정의를 전투 입력으로 바꾼다.</summary>
    public IReadOnlyList<CombatantSetup> CreateEncounterTeam(string encounterId)
    {
        if (!Encounters.TryGetValue(encounterId, out var encounter))
        {
            throw new KeyNotFoundException($"Unknown encounter '{encounterId}'.");
        }

        return encounter.Units
            .Select(u => new CombatantSetup(u.Id, u.Name, u.Stats, u.Row, u.Weapon, u.Armor, u.Skills, u.Tactics))
            .ToList();
    }
}
