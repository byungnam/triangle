namespace Triangle.Core.Progress;

/// <summary>
/// 시드 계산. 같은 입력이면 항상 같은 값을 낸다 (세이브를 다시 불러도 결과가 같도록).
/// System.Random의 내부 상태는 저장할 수 없으므로, 필요한 시드를 정수 하나에서 이어서 만든다.
/// </summary>
public static class Seeds
{
    /// <summary>다음 시드 (SplitMix32 한 단계).</summary>
    public static int Next(int seed) => Mix(unchecked((uint)seed + 0x9E3779B9u));

    /// <summary>한 시드에서 번호별로 갈라진 시드 (예: 원정 시드 + 전투 번호).</summary>
    public static int Derive(int seed, int index) => Mix(unchecked((uint)seed ^ ((uint)index * 0x85EBCA6Bu + 0x632BE59Bu)));

    private static int Mix(uint z)
    {
        z = unchecked((z ^ (z >> 16)) * 0x85EBCA6Bu);
        z = unchecked((z ^ (z >> 13)) * 0xC2B2AE35u);
        return (int)(z ^ (z >> 16));
    }
}
