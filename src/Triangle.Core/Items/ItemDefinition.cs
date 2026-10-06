using Triangle.Core.Combat;
using Triangle.Core.Skills;

namespace Triangle.Core.Items;

/// <summary>
/// 아이템. 장비는 부위(<see cref="Slot"/>)에 끼고, 계열(<see cref="Mastery"/>)의 숙련 경험치를 받는다.
/// - 무기(주무기, 보조)는 무기 계열, 머리·몸통·신발은 재질 계열이다. 보조 아이템은 같이 쓰는 한손 무기 계열에 속한다.
/// - 기본 성능은 <see cref="Bonuses"/>, 끼면 <see cref="Actions"/>의 행동을 모두 쓸 수 있다.
/// - 높은 티어는 그 계열 패시브 레벨을 요구한다(<see cref="Requirements"/>, EVE식).
/// 재료(<see cref="EquipmentSlot.Material"/>)는 계열, 보너스, 행동이 없다.
/// </summary>
public sealed record ItemDefinition
{
    public const int MaxTier = 4;

    /// <summary>숙련 아이템 파워 (Albion): 아이템 계열의 숙련 레벨 1당 그 아이템 보너스 +2% (상대값, 임시).</summary>
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

    /// <summary>장비 계열(숙련) ID. 재료는 null.</summary>
    public string? Mastery { get; init; }

    /// <summary>두손 무기. 끼면 보조 칸을 쓸 수 없다. 주무기만 된다.</summary>
    public bool TwoHanded { get; init; }

    /// <summary>1–4.</summary>
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

    public bool IsEquipment => Slot.IsEquipment();

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

