using Triangle.Core.Combat;
using Triangle.Core.Skills;

namespace Triangle.Core.Items;

/// <summary>
/// 아이템. 부위(<see cref="Slot"/>)에 끼고 계열(<see cref="Mastery"/>)에 속한다.
/// - 무기(주무기, 보조)는 무기 계열, 머리·몸통·신발은 재질 계열이다. 장신구는 계열이 없다.
/// - 기본 성능은 <see cref="Bonuses"/>다.
/// - 판(로그라이트 재편)에서는 아이템이 행동을 주지 않고 요구 패시브도 보지 않는다. <see cref="Actions"/>와 <see cref="Requirements"/>는
///   전술 측정의 기준 파티(옛 성장 규칙)만 쓴다. 적은 행동 목록을 따로 가진다.
/// </summary>
public sealed record ItemDefinition
{
    /// <summary>가장 높은 티어. 판의 막 상점이 티어 1~3을 판다 (T4는 제작과 함께 없앴다, 2026-10-19).</summary>
    public const int MaxTier = 3;

    /// <summary>숙련 아이템 파워: 아이템 계열의 숙련 레벨 1당 그 아이템 보너스 +2% (상대값, 전술 측정만 쓴다. 판에서 숙련은 0이다).</summary>
    public const int PowerPercentPerMasteryLevel = 2;

    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>편집기용 메모.</summary>
    public string? Description { get; init; }

    public required EquipmentSlot Slot { get; init; }

    /// <summary>무기 종류 이름 (예: "한손검", "화염 지팡이"). 같은 종류는 티어만 다르다. 표시용.</summary>
    public string? Type { get; init; }

    /// <summary>표시용 종류: <see cref="Type"/>, 없으면 이름.</summary>
    public string TypeOrName => Type ?? Name;

    /// <summary>장비 계열 ID. 장신구는 null.</summary>
    public string? Mastery { get; init; }

    /// <summary>두손 무기. 끼면 보조 칸을 쓸 수 없다. 주무기만 된다.</summary>
    public bool TwoHanded { get; init; }

    /// <summary>1–3.</summary>
    public int Tier { get; init; } = 1;

    /// <summary>가격 (골드).</summary>
    public int Price { get; init; }

    /// <summary>착용 조건: 패시브 스킬 레벨.</summary>
    public IReadOnlyList<SkillRequirement> Requirements { get; init; } = [];

    /// <summary>끼고 있으면 주는 보너스.</summary>
    public IReadOnlyList<ItemBonus> Bonuses { get; init; } = [];

    /// <summary>
    /// 무기 위력 배율(%, 주무기만). 이 무기 계열 태그가 붙은 행동의 피해·회복·보호막과 소환 능력치에 마지막으로 곱한다.
    /// 마법 지팡이는 200이라 같은 속성 마법봉(100)의 2배다.
    /// </summary>
    public int PowerMultiplierPercent { get; init; } = 100;

    /// <summary>이 무기 계열 태그가 붙은 행동이 거는 효과의 지속 턴 보너스 (주무기만, 예: 마법 지팡이 +1).</summary>
    public int EffectDurationBonus { get; init; }

    /// <summary>끼면 쓸 수 있는 행동 (모두 쓴다, 고르지 않는다. 2026-10-06).</summary>
    public IReadOnlyList<string> Actions { get; init; } = [];

    /// <summary>끼고 있으면 전투를 시작할 때 걸려 있는 효과 (타고난 효과와 같다, 장신구만, 2026-10-19). 지워지기 전까지 끝나지 않는다.</summary>
    public IReadOnlyList<string> Effects { get; init; } = [];


    /// <summary>
    /// 계열 숙련 레벨에 따른 보너스 (숙련 아이템 파워). 각 보너스 × (100 + 레벨 × 2) / 100, 사사오입.
    /// 같은 아이템도 숙련이 높으면 조금 더 강하다.
    /// </summary>
    public IReadOnlyList<ItemBonus> BonusesAt(int masteryLevel)
    {
        var power = 100 + Math.Max(0, masteryLevel) * PowerPercentPerMasteryLevel;
        return Bonuses.Select(b => b with { Percent = Ratio.ApplyPercent(b.Percent, power) }).ToList();
    }
}

/// <param name="Tag">null이면 모든 행동에 적용한다.</param>
public sealed record ItemBonus(BonusKind Kind, int Percent, string? Tag = null);

