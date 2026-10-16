using System.Text.Json.Serialization;

namespace Triangle.Core.Items;

/// <summary>
/// 장비 한 점: 창고나 멤버의 부위에 있는 실제 물건 (2026-10-16). 같은 아이템도 한 점씩 따로 저장한다.
/// 재료는 한 점씩 두지 않고 창고에 개수로 둔다(<see cref="Progress.Company.Materials"/>).
/// - <see cref="Uid"/>: 회사 안에서 겹치지 않는 번호. 회사가 새 장비를 만들 때 붙인다(<see cref="Progress.Company.NextItemUid"/>).
/// - <see cref="Level"/>: 아이템 레벨 (1 이상). 원정에서 떨어진 장비는 그 지역의 깊이(난이도), 상점·모집 장비는 티어,
///   제작품은 클리어한 가장 깊은 깊이다. 무작위 옵션의 값이 이 레벨에 비례한다(<see cref="AffixDefinition.ValueAt"/>).
/// - <see cref="Options"/>: 무작위 옵션 (0~3개, 반복 파밍 계획 2단위). 없으면 null로 두어 세이브에 쓰지 않는다.
/// </summary>
public sealed record ItemInstance(int Uid, string ItemId, int Level, IReadOnlyList<ItemOption>? Options = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ItemOption>? Options { get; init; } = Options is { Count: > 0 } ? Options.ToList() : null;

    /// <summary>붙은 옵션 (없으면 빈 목록).</summary>
    [JsonIgnore]
    public IReadOnlyList<ItemOption> Affixes => Options ?? [];

    /// <summary>등급: 옵션 수로 정한다.</summary>
    [JsonIgnore]
    public ItemRarity Rarity => (ItemRarity)Math.Min(Affixes.Count, (int)ItemRarity.Legendary);

    public bool Equals(ItemInstance? other) =>
        other is not null && Uid == other.Uid && ItemId == other.ItemId && Level == other.Level && Affixes.SequenceEqual(other.Affixes);

    public override int GetHashCode() => HashCode.Combine(Uid, ItemId, Level, Affixes.Count);
}
