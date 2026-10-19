using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Effects;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Runs;
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
        IReadOnlyDictionary<string, ClassDefinition>? classes = null)
    {
        Classes = classes ?? new Dictionary<string, ClassDefinition>();
        Items = items;
        Zones = zones;
        Effects = effects;
        Masteries = masteries;
        Skills = skills;
        Actions = actions;
        Encounters = encounters;
        Catalog = new CombatCatalog(actions, skills) { Effects = effects };
    }

    /// <summary>장비 계열 (무기 종류와 재질). 클래스 계열, 아이템과 패시브의 소속, 위력 태그에 쓴다.</summary>
    public IReadOnlyDictionary<string, MasteryDefinition> Masteries { get; }

    public IEnumerable<MasteryDefinition> MasteriesOf(MasteryKind kind) => Masteries.Values.Where(m => m.Kind == kind);

    public IReadOnlyDictionary<string, ItemDefinition> Items { get; }

    /// <summary>그 행동을 주는 아이템 (데이터 순서, 전술 측정의 기준 파티용).</summary>
    public IEnumerable<ItemDefinition> ItemsGranting(string actionId) =>
        Items.Values.Where(i => i.Actions.Contains(actionId));

    /// <summary>지역 (데이터 순서). 판의 막이 지역을 고른다.</summary>
    public IReadOnlyDictionary<string, ZoneDefinition> Zones { get; }

    /// <summary>한 판(원정)의 규칙 (로그라이트 재편, `run.json`). 데이터 폴더에서 읽으면 늘 있다.</summary>
    public RunDefinition? Run { get; init; }

    /// <summary>해금 (로그라이트 재편, `unlocks.json`, 데이터 순서).</summary>
    public IReadOnlyDictionary<string, UnlockDefinition> Unlocks { get; init; } = new Dictionary<string, UnlockDefinition>();

    /// <summary>클래스 (로그라이트 재편, 데이터 순서). 원정을 시작할 때 이것으로 파티를 고른다.</summary>
    public IReadOnlyDictionary<string, ClassDefinition> Classes { get; }

    /// <summary>패시브 스킬 (계열별).</summary>
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
            .Select(u =>
            {
                var setup = new Loadout(u.Equipment.Select(e => (e.Key, Items[e.Value]))).Apply(
                    new CombatantSetup(u.Id, u.Name, u.Stats, u.Row, null, null, u.Skills, u.Tactics) { StartingEffects = u.Effects },
                    Actions.Values,
                    _ => 0);
                return u.Actions is null ? setup : setup with { GrantedActions = u.Actions.ToHashSet() };
            })
            .ToList();
    }
}
