using System.Collections;

namespace Triangle.Core.Tactics;

/// <summary>
/// 편집할 수 있는 전술 목록 한 벌. 우선순위는 항상 목록 순서대로 1, 2, 3… 이다.
/// </summary>
public sealed class TacticList : IReadOnlyList<Tactic>
{
    private readonly List<Tactic> _tactics = [];

    public TacticList(IEnumerable<Tactic> tactics)
    {
        _tactics.AddRange(tactics.OrderBy(t => t.Priority));
        Renumber();
    }

    public int Count => _tactics.Count;

    public Tactic this[int index] => _tactics[index];

    public IEnumerator<Tactic> GetEnumerator() => _tactics.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>목록 끝에 추가한다. 우선순위는 자동으로 정해진다.</summary>
    public void Add(Condition condition, int value, string actionId) =>
        _tactics.Add(new Tactic(_tactics.Count + 1, condition, value, actionId));

    /// <summary>index 위치의 전술 내용을 바꾼다. 우선순위는 위치를 따른다.</summary>
    public void Replace(int index, Condition condition, int value, string actionId) =>
        _tactics[index] = new Tactic(index + 1, condition, value, actionId);

    public void Remove(int index)
    {
        _tactics.RemoveAt(index);
        Renumber();
    }

    /// <summary>
    /// index 위치의 전술을 offset만큼 옮긴다(-1이면 한 칸 위로).
    /// 목록 밖으로 나가면 아무것도 하지 않고 false를 돌려준다.
    /// </summary>
    public bool Move(int index, int offset)
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
