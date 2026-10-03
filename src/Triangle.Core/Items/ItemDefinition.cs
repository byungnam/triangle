using Triangle.Core.Combat;
using Triangle.Core.Skills;

namespace Triangle.Core.Items;

/// <summary>
/// 아이템. 장비는 부위(<see cref="Slot"/>)에 끼고, 계열(<see cref="Mastery"/>)의 숙련 경험치를 받는다.
/// - 무기(주무기, 보조)는 무기 계열, 머리·몸통·신발은 재질 계열이다. 보조 아이템은 같이 쓰는 한손 무기 계열에 속한다.
/// - 기본 성능은 <see cref="Bonuses"/>, 행동은 행동 칸(<see cref="Abilities"/>)에서 하나씩 고른다 (Albion식).
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

    /// <summary>행동 칸. 칸마다 후보 행동 중 하나를 고른다.</summary>
    public IReadOnlyList<AbilitySlot> Abilities { get; init; } = [];

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

    /// <summary>각 칸의 첫 옵션 (처음 끼었을 때의 선택).</summary>
    public IReadOnlyList<string> DefaultChoices => Abilities.Select(a => a.Options[0]).ToList();
}

/// <param name="Tag">null이면 모든 행동에 적용한다.</param>
public sealed record ItemBonus(BonusKind Kind, int Percent, string? Tag = null);

/// <summary>아이템의 행동 칸 하나. 후보(행동 ID) 중 하나를 고른다.</summary>
public sealed record AbilitySlot(IReadOnlyList<string> Options);
