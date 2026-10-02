using Triangle.Core.Data;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>모집 후보를 굴린다. 템플릿, 이름, 스탯을 시드 RNG로 정한다 (같은 시드면 같은 후보).</summary>
public static class Recruitment
{
    public const int OfferCount = 3;

    public static IReadOnlyList<RecruitOffer> Roll(GameData data, int seed)
    {
        var templates = data.Recruits.Values.ToList();
        if (templates.Count == 0)
        {
            return [];
        }

        var random = new Random(seed);
        int Between(int min, int max) => random.Next(min, max + 1);

        return Enumerable.Range(0, OfferCount).Select(_ =>
        {
            var t = templates[random.Next(templates.Count)];
            var name = t.Names[random.Next(t.Names.Count)];
            var (min, max) = (t.StatsMin, t.StatsMax);
            var stats = new Stats(
                Between(min.Str, max.Str),
                Between(min.Dex, max.Dex),
                Between(min.Vital, max.Vital),
                Between(min.Intel, max.Intel),
                Between(min.Speed, max.Speed));
            return new RecruitOffer(t.Id, name, stats, t.Price);
        }).ToList();
    }
}
