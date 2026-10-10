using Triangle.Core.Combat;

namespace Triangle.Core.Expeditions;

/// <summary>
/// 진행 중인 원정의 상태. 세이브에 그대로 들어간다. 규칙은 <see cref="ExpeditionRules"/>에 있다.
/// - 출전 멤버의 HP·MP는 전투 사이에 이어지고, 다음 출정에서만 가득 찬다. 쓰러진 멤버는 남은 전투에 나가지 못한다.
/// - 전리품(골드, 아이템)은 들고 다니다가 원정이 끝나면 확정된다. 전멸해도 이미 얻은 것은 남는다.
/// </summary>
public sealed class Expedition
{
    private readonly List<ExpeditionMember> _members;
    private readonly Dictionary<string, int> _carriedItems;

    public Expedition(
        string zoneId,
        int seed,
        int battleIndex,
        IEnumerable<ExpeditionMember> members,
        int carriedGold,
        IReadOnlyDictionary<string, int> carriedItems,
        BattleReport? lastBattle)
    {
        ZoneId = zoneId;
        Seed = seed;
        BattleIndex = battleIndex;
        _members = members.ToList();
        CarriedGold = carriedGold;
        _carriedItems = new Dictionary<string, int>(carriedItems.Where(p => p.Value > 0));
        LastBattle = lastBattle;
    }

    public string ZoneId { get; }

    /// <summary>원정 시드. 인카운터, 전투, 전리품의 시드는 이 값과 전투 번호에서 나온다.</summary>
    public int Seed { get; }

    /// <summary>지금까지 치른 전투 수 (= 다음 전투의 번호, 0부터).</summary>
    public int BattleIndex { get; internal set; }

    /// <summary>출전 멤버 (출정할 때의 명단 순서).</summary>
    public IReadOnlyList<ExpeditionMember> Members => _members;

    public int CarriedGold { get; internal set; }

    /// <summary>들고 있는 아이템 (ID별 개수).</summary>
    public IReadOnlyDictionary<string, int> CarriedItems => _carriedItems;

    /// <summary>직전 전투의 요약. 아직 싸우지 않았으면 null.</summary>
    public BattleReport? LastBattle { get; internal set; }

    public IEnumerable<ExpeditionMember> Standing => _members.Where(m => !m.Down);

    public ExpeditionMember? MemberState(string memberId) => _members.FirstOrDefault(m => m.Id == memberId);

    internal void Update(ExpeditionMember state)
    {
        var index = _members.FindIndex(m => m.Id == state.Id);
        _members[index] = state;
    }

    internal void Carry(string itemId, int count = 1) => _carriedItems[itemId] = _carriedItems.GetValueOrDefault(itemId) + count;
}

/// <summary>원정 중인 멤버의 상태.</summary>
/// <param name="Down">쓰러졌다 (남은 전투에 나가지 못한다).</param>
public sealed record ExpeditionMember(string Id, int Hp, int Mp, bool Down);

/// <summary>한 전투의 요약 (원정 화면에 보여준다).</summary>
public sealed record BattleReport
{
    /// <summary>몇 번째 전투인가 (1부터).</summary>
    public required int Number { get; init; }

    public required string EncounterId { get; init; }
    public required CombatOutcome Outcome { get; init; }

    /// <summary>얻은 숙련 경험치.</summary>
    public IReadOnlyList<XpReport> Xp { get; init; } = [];

    /// <summary>이 전투에서 들고 있는 전리품에 더한 골드 (클리어 보너스 포함).</summary>
    public int Gold { get; init; }

    /// <summary>이 전투에서 떨어진 아이템 ID.</summary>
    public IReadOnlyList<string> Drops { get; init; } = [];

    /// <summary>이 전투에서 쓰러진 멤버 이름.</summary>
    public IReadOnlyList<string> Downed { get; init; } = [];
}

/// <param name="LevelsGained">오른 숙련 레벨 수 (= 새로 생긴 포인트).</param>
public sealed record XpReport(string MemberId, string MemberName, string Mastery, int Xp, int LevelsGained);

public enum ExpeditionEnd
{
    /// <summary>무승부로 강제 귀환했다.</summary>
    ForcedReturn,

    /// <summary>마지막 전투까지 이겼다. 클리어 보너스를 더 받는다.</summary>
    Cleared,

    /// <summary>전멸했다. 이미 얻은 전리품은 남는다.</summary>
    Wiped,
}

/// <summary>끝난 원정의 결과 (마을에 보여준다).</summary>
/// <param name="Gold">확정된 골드.</param>
/// <param name="Items">확정된 아이템 ID별 개수.</param>
public sealed record ExpeditionSummary(string ZoneId, ExpeditionEnd End, int Battles, int Gold, IReadOnlyDictionary<string, int> Items);
