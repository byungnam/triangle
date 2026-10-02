using Triangle.Core.Skills;

namespace Triangle.Core.Progress;

public enum LearnBlockerKind
{
    MaxLevel,
    Prerequisites,
    NotEnoughPoints,
}

/// <param name="Missing">Prerequisites일 때 모자란 선행 스킬.</param>
public sealed record LearnBlocker(LearnBlockerKind Kind, IReadOnlyList<SkillRequirement> Missing);
