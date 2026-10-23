namespace Triangle.Core.Runs;

/// <summary>
/// 승천 한 단계가 더하는 어려움 (2026-10-23, `data/run.json`의 <c>ascensions</c>). 판을 처음 이기면 승천 1이 열리고, 승천 N을 이기면 N+1이 열린다.
/// 단계는 쌓인다: 승천 N으로 시작한 판에는 1~N 단계가 모두 걸린다(<see cref="Total"/>). 승천 0은 아무것도 없는 첫 판 난이도다.
/// </summary>
public sealed record RunAscension
{
    /// <summary>화면에 적는 이름: "정예가 강하다".</summary>
    public string Name { get; init; } = "";

    /// <summary>보통 전투 적의 HP·위력 + % (막이 정한 세기에 더한다).</summary>
    public int Fight { get; init; }

    /// <summary>정예 적의 HP·위력 + %.</summary>
    public int Elite { get; init; }

    /// <summary>보스의 HP·위력 + %.</summary>
    public int Boss { get; init; }

    /// <summary>전투에서 받는 골드 % (음수면 준다).</summary>
    public int Gold { get; init; }

    /// <summary>상점의 모든 값 + %.</summary>
    public int Shop { get; init; }

    /// <summary>상점 한 번에 할 수 있는 강화 수 (음수면 준다).</summary>
    public int Upgrades { get; init; }

    /// <summary>전투·이벤트 보상의 카드 수 (음수면 준다, 1장 아래로는 줄지 않는다).</summary>
    public int Cards { get; init; }

    /// <summary>바꾸는 것이 없다 (이름은 보지 않는다).</summary>
    public bool IsEmpty => this with { Name = "" } == new RunAscension();

    /// <summary>승천 <paramref name="level"/>에 걸리는 어려움 모두 (1~level 단계의 합). 0이면 비어 있다.</summary>
    public static RunAscension Total(RunDefinition rules, int level) =>
        rules.Ascensions.Take(level).Aggregate(new RunAscension(), (sum, a) => sum + a);

    /// <summary>화면과 도구에 적는 말: "정예 +10%", "전투 골드 -20%". 없는 것은 뺀다.</summary>
    public IEnumerable<string> Parts()
    {
        if (Fight != 0)
        {
            yield return $"보통 적 {Fight:+0;-0}%";
        }

        if (Elite != 0)
        {
            yield return $"정예 {Elite:+0;-0}%";
        }

        if (Boss != 0)
        {
            yield return $"보스 {Boss:+0;-0}%";
        }

        if (Gold != 0)
        {
            yield return $"전투 골드 {Gold:+0;-0}%";
        }

        if (Shop != 0)
        {
            yield return $"상점 값 {Shop:+0;-0}%";
        }

        if (Upgrades != 0)
        {
            yield return $"상점 강화 {Upgrades:+0;-0}번";
        }

        if (Cards != 0)
        {
            yield return $"보상 카드 {Cards:+0;-0}장";
        }
    }

    public static RunAscension operator +(RunAscension a, RunAscension b) => new()
    {
        Fight = a.Fight + b.Fight,
        Elite = a.Elite + b.Elite,
        Boss = a.Boss + b.Boss,
        Gold = a.Gold + b.Gold,
        Shop = a.Shop + b.Shop,
        Upgrades = a.Upgrades + b.Upgrades,
        Cards = a.Cards + b.Cards,
    };
}
