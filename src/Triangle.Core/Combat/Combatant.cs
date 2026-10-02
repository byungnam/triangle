using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Combat;

/// <summary>전투 중 유닛의 상태.</summary>
public sealed class Combatant
{
    private readonly int[] _tacticUses;

    internal Combatant(CombatantSetup setup, CombatSide side, CombatRules rules, SkillSet skills)
    {
        Id = setup.Id;
        Name = setup.Name;
        Skills = skills;
        Weapon = setup.Weapon;
        Armor = setup.Armor;
        Stats = setup.Stats;
        Row = setup.Row;
        Side = side;
        Tactics = setup.Tactics.OrderBy(t => t.Priority).ToArray();
        _tacticUses = new int[Tactics.Count];

        MaxHp = rules.MaxHp(setup.Stats, skills);
        MaxMp = rules.MaxMp(setup.Stats, skills);
        StartHp = Math.Clamp(setup.StartHp ?? MaxHp, Math.Min(1, MaxHp), MaxHp);
        StartMp = Math.Clamp(setup.StartMp ?? MaxMp, 0, MaxMp);
        Hp = StartHp;
        Mp = StartMp;
    }

    public string Id { get; }
    public string Name { get; }
    public SkillSet Skills { get; }
    public string? Weapon { get; }
    public string? Armor { get; }
    public Stats Stats { get; }
    public Row Row { get; }
    public CombatSide Side { get; }

    /// <summary>우선순위 순으로 정렬된 전술 (같은 우선순위는 입력 순서 유지).</summary>
    public IReadOnlyList<Tactic> Tactics { get; }

    public int MaxHp { get; }
    public int MaxMp { get; }
    /// <summary>전투를 시작한 HP·MP (원정에서는 이전 전투에서 이어진 값).</summary>
    public int StartHp { get; }
    public int StartMp { get; }

    public int Hp { get; internal set; }
    public int Mp { get; internal set; }

    public int Defense => Stats.Str;
    public int MagicDefense => Stats.Intel;

    public bool IsAlive => Hp > 0;

    /// <summary>이 유닛이 지금까지 받은 턴 수 (현재 턴 포함, 1부터).</summary>
    public int TurnCount { get; internal set; }

    /// <summary>걸려 있는 효과 (적용된 순서).</summary>
    internal List<ActiveEffect> Effects { get; } = [];

    public IEnumerable<(string EffectId, int Remaining)> ActiveEffects => Effects.Select(e => (e.Definition.Id, e.Remaining));

    internal int EffectModifier(Triangle.Core.Effects.EffectModifierKind kind) =>
        Effects.Sum(e => e.Definition.Modifiers.Where(m => m.Kind == kind).Sum(m => m.Percent));

    internal long NextActionTime { get; set; }
    internal long TieBreak { get; set; }

    internal int GetUses(int tacticIndex) => _tacticUses[tacticIndex];
    internal void AddUse(int tacticIndex) => _tacticUses[tacticIndex]++;

    public override string ToString() => $"{Name}({Hp}/{MaxHp})";
}
