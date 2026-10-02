namespace Triangle.Core.Units;

/// <summary>
/// 직업 정의. <see cref="SpeedPercent"/>는 행동 속도 계수의 백분율이다(110 = 1.10배).
/// 클수록 행동 간격이 짧아진다. <see cref="Description"/>은 편집기용 메모다.
/// </summary>
public sealed record ClassDefinition(string Id, string Name, int SpeedPercent = 100, string? Description = null);
