using Triangle.Core.Items;
using Triangle.Core.Units;

namespace Triangle.Core.Runs;

/// <summary>
/// 클래스 (로그라이트 재편, 2026-10-19). 원정을 시작할 때 클래스로 파티를 고른다(같은 클래스를 여럿 데려가도 된다).
/// 클래스는 스탯, 기본 무기와 방어구를 정하고, 드래프트에서 받을 수 있는 액티브와 패시브(카드 풀)를 가진다.
/// 전열·후열은 클래스에 묶이지 않는다: 멤버마다 고르고 판 중에도 바꾼다(2026-10-19, 사용자 결정). <see cref="Row"/>는 처음 고른 값이다.
/// 액티브와 패시브는 한 클래스에만 속한다. 아이템은 행동을 주지 않는다: 액티브는 카드로만 얻는다.
/// </summary>
public sealed record ClassDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>파티에 넣거나 합류할 때 처음 서는 줄. 멤버마다 바꿀 수 있다.</summary>
    public required Row Row { get; init; }

    /// <summary>고정 스탯 (옛 모집 틀의 중간값에서 시작했다).</summary>
    public required Stats Stats { get; init; }

    /// <summary>이 클래스의 계열(숙련 ID). 무기는 이 계열의 주무기만 낀다(클래스 전용).</summary>
    public required IReadOnlyList<string> Families { get; init; }

    /// <summary>기본 무기 (주무기 부위).</summary>
    public required string Weapon { get; init; }

    /// <summary>기본 방어구 (몸통 부위).</summary>
    public required string Armor { get; init; }

    /// <summary>시작할 때 가진 스킬 사본 1장. <see cref="Skills"/>에 있어야 한다.</summary>
    public required string StartingSkill { get; init; }

    /// <summary>드래프트의 액티브 카드 풀 (행동 ID). 사본 한 장은 전술 한 줄에만 놓는다.</summary>
    public required IReadOnlyList<string> Skills { get; init; }

    /// <summary>드래프트의 패시브 카드 풀 (스킬 ID). 같은 패시브를 다시 받으면 레벨이 오른다.</summary>
    public required IReadOnlyList<string> Passives { get; init; }

    /// <summary>이 클래스가 낄 수 있는 무기인가: 주무기이고 계열이 이 클래스의 것.</summary>
    public bool CanWield(ItemDefinition item) => item.Slot == EquipmentSlot.MainHand && item.Mastery is not null && Families.Contains(item.Mastery);
}
