namespace Triangle.Core.Runs;

/// <summary>
/// 한 판(원정)의 규칙과 수치 (`data/run.json`, 로그라이트 재편 2026-10-19). 수치는 모두 임시다.
/// 판은 막 여러 개로 이뤄진다. 막마다 갈림길 지도(<see cref="RunMap"/>)를 따라 지점을 고르며 나아가고, 막의 끝은 보스다.
/// </summary>
public sealed record RunDefinition
{
    /// <summary>원정을 시작할 때 고르는 인원.</summary>
    public int PartySize { get; init; } = 4;

    /// <summary>멤버마다 시작 줄 상한.</summary>
    public int StartLines { get; init; } = 2;

    /// <summary>줄 상한의 최대치 ("줄 상한 +1" 카드로 늘릴 수 있는 끝).</summary>
    public int MaxLines { get; init; } = 6;

    /// <summary>한 사람이 낄 수 있는 장신구 수.</summary>
    public int MaxTrinkets { get; init; } = 2;

    public int StartGold { get; init; }

    /// <summary>
    /// 아군 최대 MP % (2026-10-24). 클래스의 지능으로 셈한 MP가 스킬 비용에 비해 넉넉해 정신 집중이 할 일이 없어서, 판의 아군만 줄인다.
    /// 적은 조합마다 MP 조건과 정신 집중으로 짜여 있어 그대로 둔다.
    /// </summary>
    public int AllyMpPercent { get; init; }

    public required IReadOnlyList<RunAct> Acts { get; init; }

    public RunMapRules Map { get; init; } = new();

    public RunRewardRules Reward { get; init; } = new();

    public RunShopRules Shop { get; init; } = new();

    public RunRestRules Rest { get; init; } = new();

    /// <summary>이벤트 지점에서 가중치(<see cref="RunEvent.Weight"/>)로 뽑는 이벤트.</summary>
    public IReadOnlyList<RunEvent> Events { get; init; } = [];

    public RunRenownRules Renown { get; init; } = new();

    /// <summary>원정 지도: 수식어와 보상 (2026-10-23, 승천을 바꿈).</summary>
    public RunChartRules Charts { get; init; } = new();
}

/// <summary>판이 끝나면 받는 명성 (해금에 쓴다): 이긴 전투마다, 그리고 판을 이기면 더.</summary>
public sealed record RunRenownRules
{
    public int Fight { get; init; } = 1;

    public int Elite { get; init; } = 3;

    public int Boss { get; init; } = 8;

    public int Victory { get; init; } = 10;
}

/// <summary>막 하나. 보통 전투는 지역의 적 조합을 가중치로 뽑는다.</summary>
/// <param name="Floors">보스 앞의 층 수.</param>
public sealed record RunAct(string ZoneId, int Floors)
{
    /// <summary>막 보스 후보. 판을 시작할 때 막마다 하나를 뽑아 공개한다. 비면 그 지역의 보스다.</summary>
    public IReadOnlyList<string> Bosses { get; init; } = [];

    /// <summary>정예 지점의 적 조합 후보 (같은 가중치). 비면 정예 지점이 나오지 않는다.</summary>
    public IReadOnlyList<string> Elites { get; init; } = [];

    /// <summary>이 막의 상점이 파는 무기·방어구의 티어. 장신구는 이 티어 이하.</summary>
    public int ShopTier { get; init; } = 1;

    /// <summary>이 막의 보통 전투 적의 세기. 적 조합 데이터를 막마다 따로 두지 않고 세기를 맞춘다.</summary>
    public RunStrength FightStrength { get; init; } = new();

    /// <summary>이 막의 정예 적의 세기.</summary>
    public RunStrength EliteStrength { get; init; } = new();

    /// <summary>이 막의 보스의 세기.</summary>
    public RunStrength BossStrength { get; init; } = new();

    public RunStrength Strength(RunNodeKind kind) => kind switch
    {
        RunNodeKind.Elite => EliteStrength,
        RunNodeKind.Boss => BossStrength,
        _ => FightStrength,
    };
}

/// <summary>적의 세기 배율(%). 적 조합에 보너스로 얹는다.</summary>
public sealed record RunStrength
{
    /// <summary>최대 HP 배율.</summary>
    public int Hp { get; init; } = 100;

    /// <summary>위력 배율: 피해, 회복, 보호막.</summary>
    public int Power { get; init; } = 100;
}

/// <summary>지도 모양과 지점 종류의 가중치. 첫 층은 늘 전투, 보스 바로 앞 층은 늘 휴식이다.</summary>
public sealed record RunMapRules
{
    /// <summary>한 층의 칸 수 (경로가 지나갈 수 있는 자리).</summary>
    public int Width { get; init; } = 6;

    /// <summary>첫 층에서 보스까지 그어 보는 경로 수. 지점과 길은 이 경로들의 합이다.</summary>
    public int Paths { get; init; } = 6;

    /// <summary>첫 층의 갈래 수 (서로 다른 출발 지점). 폭과 경로 수 이하.</summary>
    public int Starts { get; init; } = 3;

    /// <summary>정예와 휴식이 나올 수 있는 가장 이른 층 (0부터).</summary>
    public int SpecialFrom { get; init; } = 5;

    public int Fight { get; init; } = 50;

    public int Elite { get; init; } = 12;

    public int Shop { get; init; } = 10;

    public int Rest { get; init; } = 10;

    public int Event { get; init; } = 18;
}

/// <summary>
/// 전투 보상. 이기면 골드를 받는다. 정예와 보스는 후보 카드 중 하나도 고른다(건너뛸 수도 있다). 보통 전투는 골드만 준다
/// (2026-10-19 사용자 결정: 전투마다 카드를 고르면 너무 빨리 강해진다).
/// </summary>
public sealed record RunRewardRules
{
    /// <summary>정예·보스·이벤트 카드의 후보 수.</summary>
    public int Cards { get; init; } = 3;

    /// <summary>보통 전투를 이기면 나오는 후보 카드 수. 0이면 골드만 받는다.</summary>
    public int FightCards { get; init; }

    /// <summary>정예를 이기면 후보가 이만큼 더 나온다.</summary>
    public int EliteExtraCards { get; init; } = 2;

    /// <summary>후보 카드 종류의 가중치. 줄 상한 +1은 희귀하다(2026-10-19 사용자 결정, 그 전에는 6 : 3 : 1).</summary>
    public int Active { get; init; } = 12;

    public int Passive { get; init; } = 7;

    public int Line { get; init; } = 1;

    /// <summary>강화 카드(고른 사본 하나에 강화 한 가지)의 가중치 (2026-10-23, 보상을 줄 바꾸기 중심으로).</summary>
    public int Upgrade { get; init; }

    public int GoldMin { get; init; } = 15;

    public int GoldMax { get; init; } = 25;

    public int EliteGoldMin { get; init; } = 35;

    public int EliteGoldMax { get; init; } = 50;

    public int BossGold { get; init; } = 80;
}

/// <summary>상점. 아이템은 그 아이템의 가격(<see cref="Items.ItemDefinition.Price"/>)으로 판다.</summary>
public sealed record RunShopRules
{
    /// <summary>파는 아이템 수 (무기·방어구·장신구에서 뽑는다).</summary>
    public int Items { get; init; } = 4;

    /// <summary>파는 스킬 사본 수 (살아 있는 멤버의 클래스 카드에서 뽑는다).</summary>
    public int Skills { get; init; } = 3;

    public int SkillPrice { get; init; } = 50;

    /// <summary>합류시킬 수 있는 클래스 수와 값.</summary>
    public int Recruits { get; init; } = 1;

    public int RecruitPrice { get; init; } = 200;

    /// <summary>봉인 하나를 푸는 값.</summary>
    public int UnsealPrice { get; init; } = 60;

    /// <summary>상점 한 번에 할 수 있는 강화 수 (플레이어가 사본과 종류를 고른다, 2026-10-23).</summary>
    public int Upgrades { get; init; }

    /// <summary>강화 값: 기본 값에 그 사본에 이미 건 강화 하나마다 <see cref="UpgradeStep"/>을 더한다.</summary>
    public int UpgradePrice { get; init; } = 80;

    public int UpgradeStep { get; init; } = 40;

    /// <summary>파는 줄 상한 +1 수 (0이나 1). 늘릴 멤버가 없으면 나오지 않는다.</summary>
    public int Lines { get; init; }

    /// <summary>줄 상한 값: 기본 값에 이 판에서 상점에서 산 줄 상한 하나마다 <see cref="LineStep"/>을 더한다.</summary>
    public int LinePrice { get; init; } = 150;

    public int LineStep { get; init; } = 50;
}

/// <summary>휴식: 봉인 풀기(모든 멤버) 또는 단련(패시브 후보 중 하나).</summary>
public sealed record RunRestRules
{
    /// <summary>봉인 풀기를 고르면 멤버마다 푸는 봉인 수.</summary>
    public int Unseal { get; init; } = 1;

    /// <summary>단련을 고르면 나오는 패시브 후보 수.</summary>
    public int TrainCards { get; init; } = 3;
}

/// <summary>이벤트: 글과 선택지. 선택지의 효과는 정해진 몇 가지를 조합한다.</summary>
public sealed record RunEvent
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Text { get; init; }

    /// <summary>이벤트 지점에서 뽑히는 가중치 (기본 10). 동료 합류처럼 드문 이벤트는 작게 둔다.</summary>
    public int Weight { get; init; } = 10;

    public required IReadOnlyList<RunEventOption> Options { get; init; }
}

public sealed record RunEventOption
{
    public required string Text { get; init; }

    /// <summary>골드 변화. 음수는 값이고, 가진 골드가 모자라면 고를 수 없다.</summary>
    public int Gold { get; init; }

    /// <summary>합류하는 클래스 ID. "any"면 아무 클래스. 파티가 가득 차면 고를 수 없다.</summary>
    public string? Join { get; init; }

    /// <summary>살아 있는 멤버 하나(무작위)가 받는 봉인 수. 줄 상한보다 많으면 상한만큼만.</summary>
    public int Seal { get; init; }

    /// <summary>모든 멤버의 봉인을 이만큼 푼다.</summary>
    public int Unseal { get; init; }

    /// <summary>참이면 전투 보상처럼 후보 카드 중 하나를 고른다.</summary>
    public bool Card { get; init; }

    /// <summary>참이면 이 막의 상점 티어 이하 장신구 하나를 가방에 받는다.</summary>
    public bool Trinket { get; init; }
}
