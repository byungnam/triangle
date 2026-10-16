using Triangle.Core.Data;

namespace Triangle.Core.Items;

/// <summary>
/// 무작위 옵션 규칙 (반복 파밍 계획 2단위, 2026-10-16).
/// - 떨어진 장비는 등급을 굴린다: 일반(옵션 0) 50%, 마법(1) 35%, 희귀(2) 13%, 전설(3) 2%.
///   그 장비에 붙을 수 있는 옵션이 모자라면 붙을 수 있는 만큼만 붙는다.
/// - 옵션은 그 장비에 붙을 수 있는 종류(<see cref="AffixDefinition.Fits"/>) 중에서 겹치지 않게 고르고, 값은 아이템 레벨에 비례한다.
/// - 상점·모집·시작 장비는 일반, 제작품은 마법(옵션 1개) 확정이다(<see cref="Progress.Company.Craft"/>).
/// - 적은 옵션 없는 장비를 입는다.
/// </summary>
public static class Affixes
{
    /// <summary>등급별 확률 (%, 일반·마법·희귀·전설).</summary>
    public static IReadOnlyList<int> RarityWeights { get; } = [50, 35, 13, 2];

    public const int MaxOptions = 3;

    /// <summary>등급을 굴린다 (옵션 수).</summary>
    public static int RollCount(Random random)
    {
        var roll = random.Next(RarityWeights.Sum());
        for (var count = 0; count < RarityWeights.Count; count++)
        {
            roll -= RarityWeights[count];
            if (roll < 0)
            {
                return count;
            }
        }

        return 0;
    }

    /// <summary>그 장비에 붙을 수 있는 옵션 (데이터 순서).</summary>
    public static IReadOnlyList<AffixDefinition> For(ItemDefinition item, GameData data) =>
        data.Affixes.Values.Where(a => a.Fits(item)).ToList();

    /// <summary>
    /// 그 장비에 옵션을 굴린다. count가 없으면 등급부터 굴린다(<see cref="RollCount"/>).
    /// 같은 옵션은 한 번만 붙고, 붙을 수 있는 옵션 수를 넘지 않는다. 옵션 순서는 데이터 순서다.
    /// </summary>
    public static IReadOnlyList<ItemOption> Roll(ItemDefinition item, int level, GameData data, Random random, int? count = null)
    {
        var pool = For(item, data).ToList();
        var wanted = Math.Min(Math.Min(count ?? RollCount(random), MaxOptions), pool.Count);
        var picked = new List<AffixDefinition>();
        for (var i = 0; i < wanted; i++)
        {
            var index = random.Next(pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }

        var order = data.Affixes.Values.ToList();
        return picked
            .OrderBy(order.IndexOf)
            .Select(a => new ItemOption(a.Id, a.ValueAt(level, random.Next(a.Min, a.Max + 1))))
            .ToList();
    }

    /// <summary>
    /// 낀 장비의 옵션을 보너스로 바꾼다. 상한이 있는 옵션(<see cref="AffixDefinition.MemberCap"/>)은 한 멤버의 합계를 자른다.
    /// 숙련 아이템 파워는 받지 않는다.
    /// </summary>
    public static IReadOnlyList<ItemBonus> Bonuses(IEnumerable<(ItemDefinition Item, ItemInstance Gear)> worn, GameData data)
    {
        var bonuses = new List<ItemBonus>();
        var options = worn
            .SelectMany(w => w.Gear.Affixes.Where(o => data.Affixes.ContainsKey(o.AffixId)).Select(o => (w.Item, Option: o)))
            .GroupBy(x => x.Option.AffixId);
        foreach (var group in options)
        {
            var affix = data.Affixes[group.Key];
            var room = affix.MemberCap ?? int.MaxValue;
            foreach (var (item, option) in group)
            {
                var percent = Math.Min(option.Percent, room);
                room -= percent;
                if (percent != 0)
                {
                    bonuses.Add(new ItemBonus(affix.Kind, percent, affix.TagFor(item)));
                }
            }
        }

        return bonuses;
    }

    /// <summary>
    /// 분해하면 나오는 재료: 그 장비 계열·부위의 가장 낮은 티어 제작법에서 가장 많이 드는 재료,
    /// 개수는 등급이 높을수록 많다(일반 1, 마법 2, 희귀·전설 3). 제작 재료가 없는 장비는 null.
    /// </summary>
    public static (string MaterialId, int Count)? DismantleYield(ItemInstance gear, GameData data)
    {
        if (!data.Items.TryGetValue(gear.ItemId, out var item) || !item.IsEquipment)
        {
            return null;
        }

        var material = data.Recipes.Values
            .Where(r => data.Items.TryGetValue(r.Result, out var result) && result.Slot == item.Slot && result.Mastery == item.Mastery)
            .OrderBy(r => data.Items[r.Result].Tier)
            .SelectMany(r => r.Materials.OrderByDescending(m => m.Count).Take(1))
            .Select(m => m.ItemId)
            .FirstOrDefault();
        return material is null ? null : (material, Math.Min(MaxOptions, 1 + gear.Affixes.Count));
    }
}
