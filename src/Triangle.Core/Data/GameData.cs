using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Skills;

namespace Triangle.Core.Data;

/// <summary>검증을 마친 게임 데이터. <see cref="GameDataLoader"/>로 만든다.</summary>
public sealed class GameData
{
    internal GameData(
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, ActionDefinition> actions,
        IReadOnlyDictionary<string, EncounterDefinition> encounters)
    {
        Skills = skills;
        Actions = actions;
        Encounters = encounters;
        Catalog = new CombatCatalog(actions, skills);
    }

    /// <summary>훈련하는 패시브 스킬.</summary>
    public IReadOnlyDictionary<string, SkillDefinition> Skills { get; }

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
            .Select(u => new CombatantSetup(u.Id, u.Name, u.Stats, u.Row, u.Skills, u.Tactics))
            .ToList();
    }
}
