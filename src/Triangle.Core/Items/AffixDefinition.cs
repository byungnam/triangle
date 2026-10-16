using Triangle.Core.Skills;

namespace Triangle.Core.Items;

/// <summary>
/// 장비 무작위 옵션의 종류 (<c>affixes.json</c>, 2026-10-16 반복 파밍 계획 2단위).
/// 떨어진 장비 한 점에 0~3개가 붙는다(<see cref="Affixes"/>). 효과는 아이템 보너스와 같은 종류(<see cref="BonusKind"/>)다.
/// - 값(%)은 아이템 레벨 1의 범위(<see cref="Min"/>~<see cref="Max"/>)에서 굴리고, 레벨이 하나 오를 때마다 <see cref="PerLevel"/>을 더한다.
/// - 아이템 보너스와 달리 숙련 아이템 파워(레벨당 +2%)를 받지 않는다. 보이는 값이 그대로 전투에 들어간다.
/// </summary>
public sealed record AffixDefinition
{
    public required string Id { get; init; }

    /// <summary>표시 이름 (예: "계열 위력").</summary>
    public required string Name { get; init; }

    public required BonusKind Kind { get; init; }

    /// <summary>보너스 태그 (예: "spell"). null이면 모든 행동에 적용한다. <see cref="ItemMasteryTag"/>와 같이 쓰지 않는다.</summary>
    public string? Tag { get; init; }

    /// <summary>태그가 붙은 아이템의 계열이다 (계열 위력: 한손검이면 sword 행동에만).</summary>
    public bool ItemMasteryTag { get; init; }

    /// <summary>붙을 수 있는 장비. 하나라도 맞으면 붙는다.</summary>
    public IReadOnlyList<AffixTarget> On { get; init; } = [];

    /// <summary>아이템 레벨 1에서 굴리는 값의 범위 (%).</summary>
    public int Min { get; init; }

    public int Max { get; init; }

    /// <summary>아이템 레벨이 1 오를 때마다 더하는 값 (%, 소수 가능. 마지막에 반올림한다).</summary>
    public decimal PerLevel { get; init; }

    /// <summary>한 멤버가 낀 장비의 이 옵션 합계 상한 (%). 행동 간격·MP 소모·받는 피해 감소처럼 끝없이 커지면 전투가 깨지는 옵션에 둔다.</summary>
    public int? MemberCap { get; init; }

    /// <summary>그 장비에 붙을 수 있는가.</summary>
    public bool Fits(ItemDefinition item) =>
        item.IsEquipment && On.Any(t => t.Slot == item.Slot && (t.Masteries.Count == 0 || (item.Mastery is { } m && t.Masteries.Contains(m))));

    /// <summary>아이템 레벨 level에서 레벨 1 굴림값 roll의 값: roll + PerLevel × (level − 1), 반올림.</summary>
    public int ValueAt(int level, int roll) =>
        roll + (int)Math.Round(PerLevel * (Math.Max(1, level) - 1), MidpointRounding.AwayFromZero);

    /// <summary>그 아이템에 붙었을 때의 보너스 태그.</summary>
    public string? TagFor(ItemDefinition item) => ItemMasteryTag ? item.Mastery : Tag;
}

/// <summary>옵션이 붙는 곳: 부위, 그리고 비어 있지 않으면 그 계열의 장비만 (예: 방패에만).</summary>
public sealed record AffixTarget
{
    public required EquipmentSlot Slot { get; init; }

    public IReadOnlyList<string> Masteries { get; init; } = [];
}

/// <summary>장비 한 점에 붙은 옵션: 종류와 굴린 값(%).</summary>
public sealed record ItemOption(string AffixId, int Percent);

/// <summary>장비 등급. 옵션 수로 정한다: 일반 0, 마법 1, 희귀 2, 전설 3.</summary>
public enum ItemRarity
{
    Common,
    Magic,
    Rare,
    Legendary,
}
