using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>
/// 플레이어 파티의 유닛 한 명. 전열과 전술 목록을 편집할 수 있다.
/// 전술의 우선순위는 항상 목록 순서대로 1, 2, 3… 이다.
/// 스킬은 훈련으로 쌓은 SP로 저장하고, 레벨은 SP와 스킬 랭크로 계산한다.
/// </summary>
public sealed class PartyMember
{
    private readonly List<Tactic> _tactics = [];
    private readonly Dictionary<string, int> _skillPoints;
    private readonly List<TrainingQueueEntry> _trainingQueue;

    public PartyMember(
        string id,
        string name,
        Stats stats,
        Row row,
        IReadOnlyDictionary<string, int> skillPoints,
        IEnumerable<Tactic> tactics,
        IEnumerable<TrainingQueueEntry>? trainingQueue = null,
        int unallocatedSp = 0)
    {
        Id = id;
        Name = name;
        Stats = stats;
        Row = row;
        _skillPoints = new Dictionary<string, int>(skillPoints);
        _trainingQueue = trainingQueue?.ToList() ?? [];
        UnallocatedSp = unallocatedSp;
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

    public IReadOnlyList<Tactic> Tactics => _tactics;

    /// <summary>스킬 ID별 누적 SP.</summary>
    public IReadOnlyDictionary<string, int> SkillPoints => _skillPoints;

    /// <summary>훈련 큐. 받은 SP는 맨 앞부터 쌓인다. 조작은 <see cref="Training"/>으로 한다.</summary>
    public IReadOnlyList<TrainingQueueEntry> TrainingQueue => _trainingQueue;

    /// <summary>큐가 비어 있어 아직 스킬에 넣지 않은 SP (스탯 배율 적용 전).</summary>
    public int UnallocatedSp { get; internal set; }

    internal List<TrainingQueueEntry> MutableTrainingQueue => _trainingQueue;

    internal void AddSkillPoints(string skillId, int sp) => _skillPoints[skillId] = _skillPoints.GetValueOrDefault(skillId) + sp;

    /// <summary>1레벨 이상인 스킬의 레벨.</summary>
    public IReadOnlyDictionary<string, int> SkillLevels(GameData data) =>
        _skillPoints
            .Where(p => data.Skills.ContainsKey(p.Key))
            .Select(p => (p.Key, Level: SkillProgression.LevelFor(data.Skills[p.Key].Rank, p.Value)))
            .Where(x => x.Level > 0)
            .ToDictionary(x => x.Key, x => x.Level);

    public SkillSet Skills(GameData data) => new(SkillLevels(data), data.Skills);

    /// <summary>요구 스킬을 못 채운 행동이 든 전술의 위치 (게임 데이터가 바뀌었을 때 생길 수 있다).</summary>
    public IReadOnlyList<int> LockedTacticIndexes(GameData data)
    {
        var skills = Skills(data);
        return _tactics
            .Select((t, i) => (t, i))
            .Where(x => !data.Actions.TryGetValue(x.t.ActionId, out var action) || !skills.CanUse(action))
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
        new(Id, Name, Stats, Row, SkillLevels(data), _tactics.ToList());

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
