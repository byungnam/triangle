using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Effects;
using Triangle.Core.Expeditions;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
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
        IReadOnlyDictionary<string, ItemDefinition> items,
        IReadOnlyDictionary<string, ZoneDefinition> zones,
        IReadOnlyDictionary<string, RecruitTemplate> recruits,
        IReadOnlyDictionary<string, RecipeDefinition>? recipes = null)
    {
        Recipes = recipes ?? new Dictionary<string, RecipeDefinition>();
        Items = items;
        Zones = zones;
        Recruits = recruits;
        Effects = effects;
        Masteries = masteries;
        Skills = skills;
        Actions = actions;
        Encounters = encounters;
        Catalog = new CombatCatalog(actions, skills) { Effects = effects };
    }

    /// <summary>장비 계열이자 숙련 트리.</summary>
    public IReadOnlyDictionary<string, MasteryDefinition> Masteries { get; }

    public IEnumerable<MasteryDefinition> MasteriesOf(MasteryKind kind) => Masteries.Values.Where(m => m.Kind == kind);

    /// <summary>아이템 (장비와 재료).</summary>
    public IReadOnlyDictionary<string, ItemDefinition> Items { get; }

    /// <summary>그 부위에 끼는 아이템 (데이터 순서).</summary>
    public IEnumerable<ItemDefinition> ItemsFor(EquipmentSlot slot) => Items.Values.Where(i => i.Slot == slot);

    /// <summary>아이템의 계열. null이면 null.</summary>
    public string? MasteryOf(string? itemId) => itemId is null ? null : Items[itemId].Mastery;

    /// <summary>티어 범위의 장비 (재료 제외, 데이터 순서).</summary>
    public IReadOnlyList<ItemDefinition> EquipmentInTiers(int minTier, int maxTier) =>
        Items.Values.Where(i => i.IsEquipment && i.Tier >= minTier && i.Tier <= maxTier).ToList();

    /// <summary>그 행동을 행동 칸 후보로 가진 아이템 (데이터 순서).</summary>
    public IEnumerable<ItemDefinition> ItemsGranting(string actionId) =>
        Items.Values.Where(i => i.Abilities.Any(a => a.Options.Contains(actionId)));

    /// <summary>제작법 (결과 아이템 ID → 제작법, 데이터 순서).</summary>
    public IReadOnlyDictionary<string, RecipeDefinition> Recipes { get; }

    /// <summary>전투지역 (데이터 순서).</summary>
    public IReadOnlyDictionary<string, ZoneDefinition> Zones { get; }

    /// <summary>모집 후보 템플릿.</summary>
    public IReadOnlyDictionary<string, RecruitTemplate> Recruits { get; }

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
            .Select(u => new CombatantSetup(u.Id, u.Name, u.Stats, u.Row, u.Weapon, u.Armor, u.Skills, u.Tactics)
            {
                PowerMultiplierPercent = u.PowerMultiplierPercent,
            })
            .ToList();
    }
}
