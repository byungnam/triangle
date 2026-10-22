namespace Triangle.Core.Runs;

/// <summary>
/// 해금 하나 (`data/unlocks.json`, 로그라이트 재편 2026-10-19). 판을 끝내고 받은 명성을 내고 연다. 판을 넘어 남는 것은 해금과 기록뿐이다.
/// 어떤 해금에 적힌 클래스·카드·아이템은 그 해금을 열기 전까지 판에 나오지 않는다(파티 고르기, 보상, 상점, 이벤트).
/// </summary>
public sealed record UnlockDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>여는 데 드는 명성.</summary>
    public int Cost { get; init; }

    /// <summary>여는 클래스 ID.</summary>
    public IReadOnlyList<string> Classes { get; init; } = [];

    /// <summary>여는 카드 (행동 ID, 그 클래스의 카드 풀에 있는 것).</summary>
    public IReadOnlyList<string> Cards { get; init; } = [];

    /// <summary>여는 아이템 (상점과 이벤트에 나온다).</summary>
    public IReadOnlyList<string> Items { get; init; } = [];

    /// <summary>열면 판을 시작할 때 이만큼 골드를 더 받는다.</summary>
    public int StartGold { get; init; }

    /// <summary>먼저 열어야 하는 해금 (단계가 있는 해금: 군자금 I → II → III). 없으면 바로 연다.</summary>
    public string? Requires { get; init; }

    /// <summary>열면 판 안에서 강해지는 것 (2026-10-22). 열린 해금의 것을 모두 더한다.</summary>
    public RunBoons Boons { get; init; } = new();

    /// <summary>이 해금이 잠가 두는 ID (클래스, 카드, 아이템).</summary>
    public IEnumerable<string> Locks => Classes.Concat(Cards).Concat(Items);
}

/// <summary>
/// 해금이 판에 주는 힘 (2026-10-22). 첫 판은 아주 어렵고(똑똑한 봇 클리어 5% 이하), 판을 거듭해 명성으로 이것을 쌓으면 깰 만해진다.
/// </summary>
public sealed record RunBoons
{
    /// <summary>멤버의 시작 줄 상한 + (합류한 멤버도). 줄 상한의 최대는 넘지 않는다.</summary>
    public int StartLines { get; init; }

    /// <summary>휴식에서 봉인 풀기를 고르면 멤버마다 더 푸는 봉인 수.</summary>
    public int RestUnseal { get; init; }

    /// <summary>상점의 모든 값(물건, 강화, 줄 상한, 봉인 풀기) 할인 %.</summary>
    public int ShopDiscountPercent { get; init; }

    /// <summary>파티 모두의 최대 HP + %.</summary>
    public int HpPercent { get; init; }

    /// <summary>파티 모두의 위력(피해·회복·보호막) + %.</summary>
    public int PowerPercent { get; init; }

    public bool IsEmpty => this == new RunBoons();

    /// <summary>화면과 도구에 적는 말: "시작 줄 상한 +1", "상점 값 -15%" 등. 없는 것은 뺀다.</summary>
    public IEnumerable<string> Parts()
    {
        if (StartLines > 0)
        {
            yield return $"시작 줄 상한 +{StartLines}";
        }

        if (RestUnseal > 0)
        {
            yield return $"휴식 봉인 풀기 +{RestUnseal}";
        }

        if (ShopDiscountPercent > 0)
        {
            yield return $"상점 값 -{ShopDiscountPercent}%";
        }

        if (HpPercent > 0)
        {
            yield return $"최대 HP +{HpPercent}%";
        }

        if (PowerPercent > 0)
        {
            yield return $"위력 +{PowerPercent}%";
        }
    }

    public static RunBoons operator +(RunBoons a, RunBoons b) => new()
    {
        StartLines = a.StartLines + b.StartLines,
        RestUnseal = a.RestUnseal + b.RestUnseal,
        ShopDiscountPercent = a.ShopDiscountPercent + b.ShopDiscountPercent,
        HpPercent = a.HpPercent + b.HpPercent,
        PowerPercent = a.PowerPercent + b.PowerPercent,
    };
}
