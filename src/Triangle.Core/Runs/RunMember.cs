using Triangle.Core.Tactics;

namespace Triangle.Core.Runs;

/// <summary>
/// 판 안의 멤버. 판이 끝나면 사라진다. 스탯과 줄은 클래스가 정한다.
/// 줄 상한(<see cref="Lines"/>)보다 많은 전술 줄은 맨 아래부터 꺼진다(봉인되면 생긴다). 맨 아래에는 늘 "항상 → 기본 공격"이 붙는다.
/// </summary>
public sealed class RunMember
{
    public RunMember(string id, string classId, int earned)
    {
        Id = id;
        ClassId = classId;
        Earned = earned;
    }

    /// <summary>판 안에서 겹치지 않는 ID (m1, m2, … 합류한 순서).</summary>
    public string Id { get; }

    public string ClassId { get; }

    /// <summary>얻은 줄 상한 (시작 + "줄 상한 +1" 카드).</summary>
    public int Earned { get; internal set; }

    /// <summary>봉인 수. 쓰러졌다 일어날 때마다 하나 는다. 휴식·상점·이벤트에서 푼다.</summary>
    public int Sealed { get; internal set; }

    /// <summary>줄 상한이 0인데 또 쓰러져 죽었다. 그 판에서 빠진다.</summary>
    public bool Dead { get; internal set; }

    /// <summary>쓸 수 있는 줄 수.</summary>
    public int Lines => Math.Max(0, Earned - Sealed);

    /// <summary>패시브 ID → 레벨.</summary>
    public Dictionary<string, int> Passives { get; init; } = [];

    /// <summary>플레이어가 짠 전술 줄 (우선순위 순). 줄 상한을 넘는 줄은 꺼져 있다.</summary>
    public List<Tactic> Tactics { get; init; } = [];

    /// <summary>켜져 있는 줄 (위에서부터 줄 상한만큼).</summary>
    public IReadOnlyList<Tactic> ActiveTactics => Tactics.Take(Lines).ToList();

    /// <summary>무기 (주무기, 이 클래스 계열만). 없으면 맨손.</summary>
    public string? Weapon { get; internal set; }

    /// <summary>방어구 (몸통). 없으면 맨몸.</summary>
    public string? Armor { get; internal set; }

    public List<string> Trinkets { get; init; } = [];

    public int Level(string passiveId) => Passives.GetValueOrDefault(passiveId);
}
