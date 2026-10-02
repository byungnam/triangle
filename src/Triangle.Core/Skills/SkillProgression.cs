namespace Triangle.Core.Skills;

/// <summary>스킬 레벨과 누적 SP의 관계 (EVE Online 표).</summary>
public static class SkillProgression
{
    /// <summary>랭크 1 기준, 레벨 1–5에 도달하는 데 필요한 누적 SP.</summary>
    private static readonly int[] BaseSp = [0, 250, 1_415, 8_000, 45_255, 256_000];

    /// <summary>level에 도달하는 데 필요한 누적 SP. level 0이면 0.</summary>
    public static int SpForLevel(int rank, int level) => BaseSp[Math.Clamp(level, 0, SkillDefinition.MaxLevel)] * rank;

    /// <summary>누적 SP로 도달한 레벨.</summary>
    public static int LevelFor(int rank, int sp)
    {
        var level = 0;
        while (level < SkillDefinition.MaxLevel && sp >= SpForLevel(rank, level + 1))
        {
            level++;
        }

        return level;
    }
}
