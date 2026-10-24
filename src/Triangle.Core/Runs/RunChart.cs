using Triangle.Core.Data;

namespace Triangle.Core.Runs;

/// <summary>
/// 판에 거는 조건의 합 (2026-10-23, 원정 지도). 지도의 수식어(<see cref="RunModifier"/>)는 판을 어렵게, 지도의 보상(<see cref="RunChartReward"/>)은
/// 쉽게 한다. 값은 모두 더하고, %는 기본에 더하는 %다. 기본값(0, 빈 목록)은 아무것도 바꾸지 않는다.
/// </summary>
public sealed record RunTerms
{
    /// <summary>모든 적의 최대 HP %.</summary>
    public int EnemyHp { get; init; }

    /// <summary>모든 적의 위력 %.</summary>
    public int EnemyPower { get; init; }

    /// <summary>정예의 HP·위력 % (모든 적에 거는 것에 더한다).</summary>
    public int Elite { get; init; }

    /// <summary>보스의 HP·위력 %.</summary>
    public int Boss { get; init; }

    /// <summary>모든 적의 방어(물리·마법) %.</summary>
    public int EnemyDefense { get; init; }

    /// <summary>모든 적의 행동 뒤 대기 감소 %.</summary>
    public int EnemySpeed { get; init; }

    /// <summary>모든 적의 가시 %: 받은 피해의 이만큼을 때린 유닛에게 돌려준다.</summary>
    public int EnemyThorns { get; init; }

    /// <summary>모든 적의 흡혈 %: 준 피해의 이만큼 회복한다.</summary>
    public int EnemyLifesteal { get; init; }

    /// <summary>보통 전투의 적 조합에 더하는 유닛 수 (조합의 첫 유닛을 절반 세기로 복제한다).</summary>
    public int ExtraEnemies { get; init; }

    /// <summary>막 지도에 정예 지점이 나오는 가중치 %.</summary>
    public int Elites { get; init; }

    /// <summary>모든 적이 전투를 시작할 때 지닌 효과 (타고난 효과).</summary>
    public IReadOnlyList<string> EnemyEffects { get; init; } = [];

    /// <summary>전열에서 시작하는 적이 지닌 효과.</summary>
    public IReadOnlyList<string> FrontEffects { get; init; } = [];

    /// <summary>아군 최대 HP %.</summary>
    public int AllyHp { get; init; }

    /// <summary>아군 위력 %.</summary>
    public int AllyPower { get; init; }

    /// <summary>아군 최대 MP %.</summary>
    public int AllyMp { get; init; }

    /// <summary>아군 행동 뒤 대기 감소 % (음수면 느려진다).</summary>
    public int AllySpeed { get; init; }

    /// <summary>아군 회복량 %.</summary>
    public int Healing { get; init; }

    /// <summary>아군이 받는 피해 %.</summary>
    public int AllyDamageTaken { get; init; }

    /// <summary>쓰러지면 더 받는 봉인 수.</summary>
    public int SealOnFall { get; init; }

    /// <summary>휴식에서 푸는 봉인 수.</summary>
    public int RestUnseal { get; init; }

    /// <summary>시작 줄 상한 (합류한 멤버도, 1 아래로는 줄지 않는다).</summary>
    public int StartLines { get; init; }

    /// <summary>전투에서 받는 골드 %.</summary>
    public int Gold { get; init; }

    /// <summary>상점의 모든 값 %.</summary>
    public int Shop { get; init; }

    /// <summary>상점 한 번에 할 수 있는 강화 수.</summary>
    public int Upgrades { get; init; }

    /// <summary>전투·이벤트 보상의 카드 수 (1장 아래로는 줄지 않는다).</summary>
    public int Cards { get; init; }

    /// <summary>시작 골드.</summary>
    public int StartGold { get; init; }

    /// <summary>상점 물건의 등급 (막이 정한 등급에 더한다).</summary>
    public int ShopTier { get; init; }

    /// <summary>판이 끝날 때 받는 명성 %.</summary>
    public int Renown { get; init; }

    /// <summary>
    /// 수치 조건과, 양수일 때 판이 어려워지면 1, 쉬워지면 -1. 수식어는 어렵게만, 보상은 쉽게만 하는지 검사하고 화면 글을 만드는 데 쓴다.
    /// </summary>
    private IEnumerable<(int Value, int Harder, string Format)> Numbers()
    {
        yield return (EnemyHp, 1, "적 HP {0}%");
        yield return (EnemyPower, 1, "적 위력 {0}%");
        yield return (Elite, 1, "정예 HP·위력 {0}%");
        yield return (Boss, 1, "보스 HP·위력 {0}%");
        yield return (EnemyDefense, 1, "적 방어 {0}%");
        yield return (EnemySpeed, 1, "적 행동 뒤 대기 {1}%");
        yield return (EnemyThorns, 1, "적 가시 {0}%");
        yield return (EnemyLifesteal, 1, "적 흡혈 {0}%");
        yield return (ExtraEnemies, 1, "보통 전투에 절반 세기의 적 {0}명");
        yield return (Elites, 1, "정예 지점 {0}%");
        yield return (AllyHp, -1, "아군 최대 HP {0}%");
        yield return (AllyPower, -1, "아군 위력 {0}%");
        yield return (AllyMp, -1, "아군 최대 MP {0}%");
        yield return (AllySpeed, -1, "아군 행동 뒤 대기 {1}%");
        yield return (Healing, -1, "아군 회복량 {0}%");
        yield return (AllyDamageTaken, 1, "아군이 받는 피해 {0}%");
        yield return (SealOnFall, 1, "쓰러지면 봉인 {0}");
        yield return (RestUnseal, -1, "휴식 봉인 풀기 {0}");
        yield return (StartLines, -1, "시작 줄 상한 {0}");
        yield return (Gold, -1, "전투 골드 {0}%");
        yield return (Shop, 1, "상점 값 {0}%");
        yield return (Upgrades, -1, "상점 강화 {0}번");
        yield return (Cards, -1, "보상 카드 {0}장");
        yield return (StartGold, -1, "시작 골드 {0}");
        yield return (ShopTier, -1, "상점 물건 등급 {0}");
        yield return (Renown, -1, "명성 {0}%");
    }

    /// <summary>바꾸는 것이 없다.</summary>
    public bool IsEmpty => EnemyEffects.Count == 0 && FrontEffects.Count == 0 && Numbers().All(n => n.Value == 0);

    /// <summary>판을 어렵게만 한다 (수식어의 조건).</summary>
    public bool OnlyHarder => Numbers().All(n => n.Value * n.Harder >= 0);

    /// <summary>판을 쉽게만 하고 효과를 걸지 않는다 (보상의 조건).</summary>
    public bool OnlyEasier => EnemyEffects.Count == 0 && FrontEffects.Count == 0 && Numbers().All(n => n.Value * n.Harder <= 0);

    /// <summary>모든 효과 ID (검증용).</summary>
    public IEnumerable<string> Effects => EnemyEffects.Concat(FrontEffects);

    /// <summary>화면과 도구에 적는 말: "적 HP +25%", "모든 적: 굳건함". 없는 것은 뺀다.</summary>
    public IEnumerable<string> Parts(GameData data)
    {
        foreach (var (value, _, format) in Numbers().Where(n => n.Value != 0))
        {
            yield return string.Format(format, value.ToString("+0;-0"), (-value).ToString("+0;-0"));
        }

        if (EnemyEffects.Count > 0)
        {
            yield return $"모든 적이 {string.Join(", ", EnemyEffects.Select(id => data.Effects[id].Name))} 효과를 지닌다";
        }

        if (FrontEffects.Count > 0)
        {
            yield return $"전열의 적이 {string.Join(", ", FrontEffects.Select(id => data.Effects[id].Name))} 효과를 지닌다";
        }
    }

    /// <summary>수치를 <paramref name="times"/>배 한다 (보상을 위험도만큼 받을 때). 효과는 그대로다.</summary>
    public RunTerms Times(int times) => new()
    {
        EnemyHp = EnemyHp * times, EnemyPower = EnemyPower * times, Elite = Elite * times, Boss = Boss * times,
        EnemyDefense = EnemyDefense * times, EnemySpeed = EnemySpeed * times, EnemyThorns = EnemyThorns * times,
        EnemyLifesteal = EnemyLifesteal * times, ExtraEnemies = ExtraEnemies * times, Elites = Elites * times,
        EnemyEffects = EnemyEffects, FrontEffects = FrontEffects,
        AllyHp = AllyHp * times, AllyPower = AllyPower * times, AllyMp = AllyMp * times, AllySpeed = AllySpeed * times,
        Healing = Healing * times, AllyDamageTaken = AllyDamageTaken * times, SealOnFall = SealOnFall * times,
        RestUnseal = RestUnseal * times, StartLines = StartLines * times,
        Gold = Gold * times, Shop = Shop * times, Upgrades = Upgrades * times, Cards = Cards * times,
        StartGold = StartGold * times, ShopTier = ShopTier * times, Renown = Renown * times,
    };

    public static RunTerms operator +(RunTerms a, RunTerms b) => new()
    {
        EnemyHp = a.EnemyHp + b.EnemyHp, EnemyPower = a.EnemyPower + b.EnemyPower, Elite = a.Elite + b.Elite, Boss = a.Boss + b.Boss,
        EnemyDefense = a.EnemyDefense + b.EnemyDefense, EnemySpeed = a.EnemySpeed + b.EnemySpeed, EnemyThorns = a.EnemyThorns + b.EnemyThorns,
        EnemyLifesteal = a.EnemyLifesteal + b.EnemyLifesteal, ExtraEnemies = a.ExtraEnemies + b.ExtraEnemies, Elites = a.Elites + b.Elites,
        EnemyEffects = [.. a.EnemyEffects, .. b.EnemyEffects.Except(a.EnemyEffects)],
        FrontEffects = [.. a.FrontEffects, .. b.FrontEffects.Except(a.FrontEffects)],
        AllyHp = a.AllyHp + b.AllyHp, AllyPower = a.AllyPower + b.AllyPower, AllyMp = a.AllyMp + b.AllyMp, AllySpeed = a.AllySpeed + b.AllySpeed,
        Healing = a.Healing + b.Healing, AllyDamageTaken = a.AllyDamageTaken + b.AllyDamageTaken, SealOnFall = a.SealOnFall + b.SealOnFall,
        RestUnseal = a.RestUnseal + b.RestUnseal, StartLines = a.StartLines + b.StartLines,
        Gold = a.Gold + b.Gold, Shop = a.Shop + b.Shop, Upgrades = a.Upgrades + b.Upgrades, Cards = a.Cards + b.Cards,
        StartGold = a.StartGold + b.StartGold, ShopTier = a.ShopTier + b.ShopTier, Renown = a.Renown + b.Renown,
    };
}

/// <summary>수식어의 갈래 (화면에서 묶어 보여 준다).</summary>
public enum RunModifierGroup
{
    /// <summary>적의 수치.</summary>
    Enemy,

    /// <summary>적의 특성: 어떤 전술이 통하는지를 바꾼다 (디버프 면역, 가시, 재생, 도발).</summary>
    Trait,

    /// <summary>아군의 수치와 규칙.</summary>
    Ally,

    /// <summary>골드, 상점, 보상.</summary>
    Economy,
}

/// <summary>원정 지도의 수식어 하나 (Path of Exile의 지도 수식어처럼): 판을 어렵게 하고, 위험도만큼 지도의 보상을 키운다.</summary>
public sealed record RunModifier
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public RunModifierGroup Group { get; init; }

    /// <summary>위험도 (1 이상): 지도의 보상과 명성을 키운다. 보통 사람 봇으로 잰 어려움에서 매긴다.</summary>
    public int Risk { get; init; } = 1;

    public RunTerms Terms { get; init; } = new();
}

/// <summary>원정 지도의 보상 하나 (지도마다 하나): 위험도 <see cref="Step"/>마다 <see cref="Terms"/>를 한 번 받는다.</summary>
public sealed record RunChartReward
{
    public required string Id { get; init; }

    /// <summary>지도 이름: "골드 지도".</summary>
    public required string Name { get; init; }

    public int Step { get; init; } = 1;

    /// <summary>받는 횟수의 끝 (0이면 끝이 없다).</summary>
    public int Max { get; init; }

    public RunTerms Terms { get; init; } = new();

    /// <summary>위험도 <paramref name="risk"/>인 지도에서 받는 횟수.</summary>
    public int Times(int risk) => Max > 0 ? Math.Min(Max, risk / Step) : risk / Step;
}

/// <summary>원정 지도의 규칙 (`run.json`의 <c>charts</c>).</summary>
public sealed record RunChartRules
{
    /// <summary>파티를 고른 뒤 기본 지도와 함께 보여 주는 지도 수.</summary>
    public int Offers { get; init; } = 3;

    /// <summary>지도 등급의 끝: 지도에 붙는 수식어 수의 끝.</summary>
    public int MaxTier { get; init; } = 6;

    /// <summary>위험도 1마다 더 받는 명성 %.</summary>
    public int RenownPerRisk { get; init; } = 10;

    public IReadOnlyList<RunModifier> Modifiers { get; init; } = [];

    public IReadOnlyList<RunChartReward> Rewards { get; init; } = [];
}

/// <summary>
/// 원정 지도 하나 (2026-10-23): 수식어 몇 개와 보상 하나. 판을 시작할 때 고르고 판 끝까지 간다. 기본 지도는 둘 다 없다.
/// 판을 이길 때마다 지도 등급(<see cref="Profile.MapTier"/>)이 올라 수식어가 더 붙은 지도가 나온다.
/// </summary>
public sealed record RunChart(IReadOnlyList<string> Modifiers, string? Reward)
{
    public static RunChart Base { get; } = new([], null);

    /// <summary>같은 수식어(같은 순서)와 같은 보상이면 같은 지도다.</summary>
    public bool Equals(RunChart? other) => other is not null && Reward == other.Reward && Modifiers.SequenceEqual(other.Modifiers);

    public override int GetHashCode() => HashCode.Combine(Reward, string.Join(',', Modifiers));

    public bool IsBase => Modifiers.Count == 0 && Reward is null;

    /// <summary>위험도: 수식어 위험도의 합.</summary>
    public int Risk(GameData data) => Modifiers.Sum(id => Modifier(data, id).Risk);

    public string Name(GameData data) => Reward is { } id ? RewardOf(data, id).Name : IsBase ? "기본 지도" : "보상 없는 지도";

    /// <summary>수식어가 거는 것 모두.</summary>
    public RunTerms Hardship(GameData data) => Modifiers.Aggregate(new RunTerms(), (sum, id) => sum + Modifier(data, id).Terms);

    /// <summary>보상이 주는 것: 보상 종류를 위험도만큼, 그리고 위험도만큼의 명성.</summary>
    public RunTerms Bounty(GameData data)
    {
        var risk = Risk(data);
        var bounty = new RunTerms { Renown = risk * Rules(data).RenownPerRisk };
        return Reward is { } id ? bounty + RewardOf(data, id).Terms.Times(RewardOf(data, id).Times(risk)) : bounty;
    }

    /// <summary>판에 거는 것 모두 (수식어 + 보상).</summary>
    public RunTerms Terms(GameData data) => Hardship(data) + Bounty(data);

    /// <summary>이 지도로 판을 시작할 수 없는 이유: 모르는 수식어나 보상, 겹친 수식어, 보상 없는 수식어.</summary>
    public string? WhyInvalid(GameData data)
    {
        var rules = Rules(data);
        if (Modifiers.FirstOrDefault(id => rules.Modifiers.All(m => m.Id != id)) is { } unknown)
        {
            return $"없는 수식어: {unknown}";
        }

        if (Reward is { } reward && rules.Rewards.All(r => r.Id != reward))
        {
            return $"없는 보상: {reward}";
        }

        if (Modifiers.Distinct().Count() != Modifiers.Count)
        {
            return "같은 수식어가 겹친다";
        }

        return Modifiers.Count > rules.MaxTier ? $"수식어는 {rules.MaxTier}개까지다" : null;
    }

    /// <summary>
    /// 지도 등급 <paramref name="tier"/>에서 지도 <paramref name="count"/>장을 굴린다. 지도마다 수식어는 등급-1 ~ 등급개(1개 이상)를 겹치지 않게 뽑고,
    /// 보상은 그 위험도에서 무언가를 주는 것 중 하나를 뽑는다. 등급이 0이면 지도가 없다(기본 지도뿐).
    /// </summary>
    public static IReadOnlyList<RunChart> Roll(GameData data, int tier, int count, Random random)
    {
        var rules = Rules(data);
        tier = Math.Min(tier, Math.Min(rules.MaxTier, rules.Modifiers.Count));
        if (tier <= 0 || rules.Rewards.Count == 0)
        {
            return [];
        }

        var charts = new List<RunChart>();
        for (var i = 0; i < count; i++)
        {
            var n = random.Next(Math.Max(1, tier - 1), tier + 1);
            var modifiers = rules.Modifiers.Select(m => m.Id).OrderBy(_ => random.Next()).Take(n).ToList();
            var risk = modifiers.Sum(id => Modifier(data, id).Risk);
            var rewards = rules.Rewards.Where(r => r.Times(risk) > 0).ToList();
            charts.Add(new RunChart(modifiers, rewards.Count > 0 ? rewards[random.Next(rewards.Count)].Id : null));
        }

        return charts;
    }

    private static RunModifier Modifier(GameData data, string id) => Rules(data).Modifiers.First(m => m.Id == id);

    private static RunChartReward RewardOf(GameData data, string id) => Rules(data).Rewards.First(r => r.Id == id);

    private static RunChartRules Rules(GameData data) => data.Run?.Charts ?? new RunChartRules();
}
