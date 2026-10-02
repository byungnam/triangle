using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>
/// 플레이어 파티의 유닛 한 명. 전열과 전술 목록을 편집할 수 있다.
/// 전술의 우선순위는 항상 목록 순서대로 1, 2, 3… 이다.
/// </summary>
public sealed class PartyMember
{
    private readonly List<Tactic> _tactics = [];

    public PartyMember(string id, string name, string classId, Stats stats, Row row, IEnumerable<Tactic> tactics)
    {
        Id = id;
        Name = name;
        ClassId = classId;
        Stats = stats;
        Row = row;
        foreach (var tactic in tactics.OrderBy(t => t.Priority))
        {
            _tactics.Add(tactic);
        }

        Renumber();
    }

    public string Id { get; }
    public string Name { get; }
    public string ClassId { get; }
    public Stats Stats { get; }
    public Row Row { get; set; }

    public IReadOnlyList<Tactic> Tactics => _tactics;

    public void ToggleRow() => Row = Row == Row.Front ? Row.Back : Row.Front;

    /// <summary>목록 끝에 추가한다. 우선순위는 자동으로 정해진다.</summary>
    public void AddTactic(Condition condition, int value, string skillId)
    {
        _tactics.Add(new Tactic(_tactics.Count + 1, condition, value, skillId));
    }

    /// <summary>index 위치의 전술 내용을 바꾼다. 우선순위는 위치를 따른다.</summary>
    public void ReplaceTactic(int index, Condition condition, int value, string skillId)
    {
        _tactics[index] = new Tactic(index + 1, condition, value, skillId);
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
        new(Id, Name, data.Classes[ClassId], Stats, Row, _tactics.ToList());

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
