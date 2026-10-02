using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Combat;

/// <summary>전투 중 유닛의 상태.</summary>
public sealed class Combatant
{
    private readonly int[] _tacticUses;

    internal Combatant(CombatantSetup setup, CombatSide side, CombatRules rules)
    {
        Id = setup.Id;
        Name = setup.Name;
        Class = setup.Class;
        Stats = setup.Stats;
        Row = setup.Row;
        Side = side;
        Tactics = setup.Tactics.OrderBy(t => t.Priority).ToArray();
        _tacticUses = new int[Tactics.Count];

        MaxHp = setup.Stats.Vital * rules.HpPerVital;
        MaxMp = setup.Stats.Intel * rules.MpPerIntel;
        Hp = MaxHp;
        Mp = MaxMp;
    }

    public string Id { get; }
    public string Name { get; }
    public ClassDefinition Class { get; }
    public Stats Stats { get; }
    public Row Row { get; }
    public CombatSide Side { get; }

    /// <summary>우선순위 순으로 정렬된 전술 (같은 우선순위는 입력 순서 유지).</summary>
    public IReadOnlyList<Tactic> Tactics { get; }

    public int MaxHp { get; }
    public int MaxMp { get; }
    public int Hp { get; internal set; }
    public int Mp { get; internal set; }

    public int Defense => Stats.Str;
    public int MagicDefense => Stats.Intel;

    public bool IsAlive => Hp > 0;

    /// <summary>이 유닛이 지금까지 받은 턴 수 (현재 턴 포함, 1부터).</summary>
    public int TurnCount { get; internal set; }

    internal long NextActionTime { get; set; }
    internal long TieBreak { get; set; }

    internal int GetUses(int tacticIndex) => _tacticUses[tacticIndex];
    internal void AddUse(int tacticIndex) => _tacticUses[tacticIndex]++;

    public override string ToString() => $"{Name}({Hp}/{MaxHp})";
}
