namespace Triangle.Core.Combat;

public sealed record CombatResult(
    CombatOutcome Outcome,
    int ActionCount,
    IReadOnlyList<CombatEvent> Events,
    IReadOnlyList<Combatant> Combatants);
