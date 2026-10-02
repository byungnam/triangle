using Triangle.Core.Combat;
using Triangle.Core.Skills;
using Triangle.Core.Units;

namespace Triangle.Core.Data;

/// <summary>검증을 마친 게임 데이터. <see cref="GameDataLoader"/>로 만든다.</summary>
public sealed class GameData
{
    internal GameData(
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, EncounterDefinition> encounters)
    {
        Classes = classes;
        Skills = skills;
        Encounters = encounters;
    }

    public IReadOnlyDictionary<string, ClassDefinition> Classes { get; }
    public IReadOnlyDictionary<string, SkillDefinition> Skills { get; }
    public IReadOnlyDictionary<string, EncounterDefinition> Encounters { get; }

    /// <summary>적 팀 정의를 전투 입력으로 바꾼다.</summary>
    public IReadOnlyList<CombatantSetup> CreateEncounterTeam(string encounterId)
    {
        if (!Encounters.TryGetValue(encounterId, out var encounter))
        {
            throw new KeyNotFoundException($"Unknown encounter '{encounterId}'.");
        }

        return encounter.Units
            .Select(u => new CombatantSetup(u.Id, u.Name, Classes[u.ClassId], u.Stats, u.Row, u.Tactics))
            .ToList();
    }
}
