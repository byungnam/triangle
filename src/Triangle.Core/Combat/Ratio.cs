using System.Numerics;

namespace Triangle.Core.Combat;

/// <summary>
/// HP/MP 비율 판정을 정수 연산으로 한다. 실수로 나누면 경계값에서 오차가 생길 수 있으므로
/// 교차 곱셈으로 비교한다. 최대치가 0이면 비율은 0으로 본다.
/// </summary>
internal static class Ratio
{
    /// <summary>current / max 와 percent / 100 비교.</summary>
    public static int CompareToPercent(int current, int max, int percent) =>
        ((long)Numerator(current, max) * 100).CompareTo((long)percent * Denominator(max));

    /// <summary>current1 / max1 와 current2 / max2 비교.</summary>
    public static int Compare(int current1, int max1, int current2, int max2) =>
        ((long)Numerator(current1, max1) * Denominator(max2))
            .CompareTo((long)Numerator(current2, max2) * Denominator(max1));

    /// <summary>
    /// 비율들의 평균과 percent / 100 비교. 분모들의 곱이 long을 넘을 수 있어 BigInteger로 계산한다.
    /// </summary>
    public static int CompareAverageToPercent(IReadOnlyCollection<(int Current, int Max)> values, int percent)
    {
        // Σ(aᵢ/bᵢ) = num / den
        BigInteger num = 0;
        BigInteger den = 1;
        foreach (var (current, max) in values)
        {
            num = num * Denominator(max) + den * Numerator(current, max);
            den *= Denominator(max);
        }

        // (Σ / n) ? (p / 100)  ⇔  num × 100 ? p × n × den
        return (num * 100).CompareTo(percent * values.Count * den);
    }

    /// <summary>value × percent / 100 을 사사오입한다 (음수가 아닌 값 전용).</summary>
    public static int ApplyPercent(int value, int percent) => DivideRounded((long)value * percent, 100);

    /// <summary>numerator / denominator 를 사사오입한다 (음수가 아닌 값 전용).</summary>
    public static int DivideRounded(long numerator, long denominator) =>
        (int)((numerator * 2 + denominator) / (denominator * 2));

    private static int Numerator(int current, int max) => max == 0 ? 0 : current;

    private static int Denominator(int max) => max == 0 ? 1 : max;
}
