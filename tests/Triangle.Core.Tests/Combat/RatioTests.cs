using Triangle.Core.Combat;

namespace Triangle.Core.Tests.Combat;

public class RatioTests
{
    [Theory]
    [InlineData(3, 10, 30, 0)]      // 정확히 30%
    [InlineData(299, 1000, 30, -1)]
    [InlineData(301, 1000, 30, 1)]
    [InlineData(1, 3, 33, 1)]       // 33.33…% > 33%
    [InlineData(0, 0, 0, 0)]        // 최대치 0 → 비율 0
    [InlineData(0, 0, 1, -1)]
    public void CompareToPercent_is_exact_at_boundaries(int current, int max, int percent, int expected)
    {
        Assert.Equal(expected, Math.Sign(Ratio.CompareToPercent(current, max, percent)));
    }

    [Fact]
    public void Average_is_exact_where_floating_point_sum_is_not()
    {
        // 실수로는 (0.1 + 0.2) / 2 = 0.15000000000000002 이라 "15% 이하"가 거짓이 된다.
        Assert.True((0.1 + 0.2) / 2 > 0.15);
        Assert.Equal(0, Ratio.CompareAverageToPercent([(1, 10), (2, 10)], 15));
    }

    [Fact]
    public void Average_handles_many_large_denominators_without_overflow()
    {
        var values = Enumerable.Range(0, 12).Select(i => (Current: 2999 - i, Max: 3000 + i)).ToList();
        Assert.True(Ratio.CompareAverageToPercent(values, 99) > 0);
        Assert.True(Ratio.CompareAverageToPercent(values, 100) < 0);
    }

    [Fact]
    public void Compare_orders_ratios_exactly()
    {
        Assert.Equal(0, Ratio.Compare(1, 3, 2, 6));
        Assert.True(Ratio.Compare(900, 3000, 665, 700) < 0); // 30% < 95%
    }

    [Theory]
    [InlineData(15, 100, 150, 10)]
    [InlineData(5, 1, 2, 3)]        // 2.5 → 3 (사사오입)
    [InlineData(7, 1, 2, 4)]        // 3.5 → 4
    [InlineData(14, 100, 100, 14)]
    public void DivideRounded_rounds_half_up(int a, int b, int c, int expected)
    {
        Assert.Equal(expected, Ratio.DivideRounded((long)a * b, c));
    }
}
