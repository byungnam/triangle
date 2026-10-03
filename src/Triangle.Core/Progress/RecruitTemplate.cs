using Triangle.Core.Items;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>
/// 모집 후보 템플릿 (예: 신입 검사). 원정이 끝날 때마다 템플릿에서 후보를 굴린다.
/// 신입은 숙련 0, 패시브 없이 시작하므로 전술은 스킬이 필요 없고 시작 장비의 첫 옵션이나 공용인 행동만 쓸 수 있다.
/// </summary>
public sealed record RecruitTemplate
{
    public required string Id { get; init; }

    /// <summary>템플릿 이름 (예: 신입 검사). 후보의 직업으로 보여준다.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>후보 이름은 여기서 무작위로 고른다.</summary>
    public required IReadOnlyList<string> Names { get; init; }

    /// <summary>스탯 범위 (스탯마다 최소–최대, 균등).</summary>
    public required Stats StatsMin { get; init; }
    public required Stats StatsMax { get; init; }

    public required Row Row { get; init; }

    /// <summary>부위별 시작 장비 아이템 ID. 고용할 때 새로 생긴다(창고에서 빼지 않는다). 행동 칸은 첫 옵션.</summary>
    public IReadOnlyDictionary<EquipmentSlot, string> Equipment { get; init; } = new Dictionary<EquipmentSlot, string>();

    /// <summary>기본 전술. 두 세트 모두 이 전술로 시작한다.</summary>
    public IReadOnlyList<Tactic> Tactics { get; init; } = [];

    /// <summary>고용 비용 (골드).</summary>
    public required int Price { get; init; }
}

/// <summary>마을의 모집 후보 한 명. 템플릿과 굴린 이름·스탯.</summary>
public sealed record RecruitOffer(string TemplateId, string Name, Stats Stats, int Price);
