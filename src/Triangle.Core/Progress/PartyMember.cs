using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Masteries;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>
/// 플레이어 파티의 유닛 한 명.
/// - 전열과 전술 목록을 편집한다. 전술 우선순위는 항상 목록 순서대로 1, 2, 3… 이다.
/// - 무기 계열 하나, 방어구 계열 하나를 장착한다.
/// - 장착한 계열로 싸우면 그 숙련 경험치가 쌓이고, 숙련 레벨 1당 그 트리 포인트 1점이 생긴다.
///   포인트로 그 트리의 패시브 스킬을 배운다 (되돌릴 수 없다).
/// </summary>
public sealed class PartyMember
{
    private readonly List<Tactic> _tactics = [];
    private readonly Dictionary<string, int> _masteryXp;
    private readonly Dictionary<string, int> _skillLevels;

    public PartyMember(
        string id,
        string name,
        Stats stats,
        Row row,
        string? weapon,
        string? armor,
        IReadOnlyDictionary<string, int> masteryXp,
        IReadOnlyDictionary<string, int> skillLevels,
        IEnumerable<Tactic> tactics)
    {
        Id = id;
        Name = name;
        Stats = stats;
        Row = row;
        Weapon = weapon;
        Armor = armor;
        _masteryXp = new Dictionary<string, int>(masteryXp);
        _skillLevels = new Dictionary<string, int>(skillLevels.Where(p => p.Value > 0));
        foreach (var tactic in tactics.OrderBy(t => t.Priority))
        {
            _tactics.Add(tactic);
        }

        Renumber();
    }

    public string Id { get; }
    public string Name { get; }
    public Stats Stats { get; }
    public Row Row { get; set; }

    /// <summary>장착한 무기 계열 ID. 바꾸면 그 무기가 필요한 전술이 잠길 수 있다.</summary>
    public string? Weapon { get; set; }

    /// <summary>장착한 방어구 계열 ID.</summary>
    public string? Armor { get; set; }

    public IReadOnlyList<Tactic> Tactics => _tactics;

    /// <summary>숙련 ID별 누적 경험치.</summary>
    public IReadOnlyDictionary<string, int> MasteryXp => _masteryXp;

    /// <summary>배운 스킬의 레벨 (1–5).</summary>
    public IReadOnlyDictionary<string, int> SkillLevels => _skillLevels;

    public SkillSet Skills(GameData data) => new(_skillLevels, data.Skills);

    public int MasteryLevel(string masteryId) => MasteryProgression.LevelFor(_masteryXp.GetValueOrDefault(masteryId));

    /// <summary>그 트리에 쓴 포인트 = Σ(랭크 × 레벨).</summary>
    public int PointsSpent(string masteryId, GameData data) =>
        _skillLevels
            .Where(p => data.Skills.TryGetValue(p.Key, out var s) && s.Mastery == masteryId)
            .Sum(p => data.Skills[p.Key].Rank * p.Value);

    public int PointsAvailable(string masteryId, GameData data) => MasteryLevel(masteryId) - PointsSpent(masteryId, data);

    /// <summary>
    /// 경험치를 더한다. 오른 숙련 레벨 수(= 새로 생긴 포인트)를 돌려준다.
    /// </summary>
    public int AddMasteryXp(string masteryId, int xp)
    {
        var before = MasteryLevel(masteryId);
        _masteryXp[masteryId] = _masteryXp.GetValueOrDefault(masteryId) + Math.Max(0, xp);
        return MasteryLevel(masteryId) - before;
    }

    /// <summary>스킬을 다음 레벨로 올릴 수 없는 이유. null이면 올릴 수 있다.</summary>
    public LearnBlocker? WhyCannotLearn(string skillId, GameData data)
    {
        var skill = data.Skills[skillId];
        var level = _skillLevels.GetValueOrDefault(skillId);
        if (level >= SkillDefinition.MaxLevel)
        {
            return new LearnBlocker(LearnBlockerKind.MaxLevel, []);
        }

        var missing = Skills(data).Missing(skill.Prerequisites);
        if (missing.Count > 0)
        {
            return new LearnBlocker(LearnBlockerKind.Prerequisites, missing);
        }

        return PointsAvailable(skill.Mastery, data) < skill.Rank ? new LearnBlocker(LearnBlockerKind.NotEnoughPoints, []) : null;
    }

    /// <summary>포인트를 써서 스킬을 한 레벨 올린다. 올릴 수 없으면 false. 되돌릴 수 없다.</summary>
    public bool Learn(string skillId, GameData data)
    {
        if (WhyCannotLearn(skillId, data) is not null)
        {
            return false;
        }

        _skillLevels[skillId] = _skillLevels.GetValueOrDefault(skillId) + 1;
        return true;
    }

    /// <summary>
    /// 지금 장비와 스킬로 쓸 수 없는 행동이 든 전술의 위치.
    /// 무기를 바꾸었거나 게임 데이터가 바뀌었을 때 생긴다.
    /// </summary>
    public IReadOnlyList<int> LockedTacticIndexes(GameData data)
    {
        var skills = Skills(data);
        return _tactics
            .Select((t, i) => (t, i))
            .Where(x => !data.Actions.TryGetValue(x.t.ActionId, out var action) || !action.IsUsableBy(Weapon, skills))
            .Select(x => x.i)
            .ToList();
    }

    public void ToggleRow() => Row = Row == Row.Front ? Row.Back : Row.Front;

    /// <summary>목록 끝에 추가한다. 우선순위는 자동으로 정해진다.</summary>
    public void AddTactic(Condition condition, int value, string actionId)
    {
        _tactics.Add(new Tactic(_tactics.Count + 1, condition, value, actionId));
    }

    /// <summary>index 위치의 전술 내용을 바꾼다. 우선순위는 위치를 따른다.</summary>
    public void ReplaceTactic(int index, Condition condition, int value, string actionId)
    {
        _tactics[index] = new Tactic(index + 1, condition, value, actionId);
    }

    public void RemoveTactic(int index)
    {
        _tactics.RemoveAt(index);
        Renumber();
    }

    /// <summary>
    /// index 위치의 전술을 offset만큼 옮긴다(-1이면 한 칸 위로).
    /// 목록 밖으로 나가면 아무것도 하지 않고 false를 돌려준다.
    /// </summary>
    public bool MoveTactic(int index, int offset)
    {
        var target = index + offset;
        if (index < 0 || index >= _tactics.Count || target < 0 || target >= _tactics.Count)
        {
            return false;
        }

        (_tactics[index], _tactics[target]) = (_tactics[target], _tactics[index]);
        Renumber();
        return true;
    }

    public CombatantSetup ToCombatantSetup(GameData data) =>
        new(Id, Name, Stats, Row, Weapon, Armor, new Dictionary<string, int>(_skillLevels), _tactics.ToList());

    private void Renumber()
    {
        for (var i = 0; i < _tactics.Count; i++)
        {
            if (_tactics[i].Priority != i + 1)
            {
                _tactics[i] = _tactics[i] with { Priority = i + 1 };
            }
        }
    }
}
