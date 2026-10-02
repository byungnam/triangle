namespace Triangle.Core.Masteries;

/// <summary>숙련 레벨과 누적 경험치의 관계: 레벨 L까지 50 × L × (L + 1). 레벨 1당 트리 포인트 1점.</summary>
public static class MasteryProgression
{
    public const int MaxLevel = 30;

    public static int XpForLevel(int level)
    {
        var l = Math.Clamp(level, 0, MaxLevel);
        return 50 * l * (l + 1);
    }

    public static int LevelFor(int xp)
    {
        var level = 0;
        while (level < MaxLevel && xp >= XpForLevel(level + 1))
        {
            level++;
        }

        return level;
    }
}
