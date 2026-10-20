using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;

namespace Triangle.Core.Runs;

/// <summary>
/// 액티브 스킬 강화 한 가지 (2026-10-20). 상점에서 골드로 사고, 파티가 가진 그 스킬의 사본 모두에 걸린다. 스킬마다 다른 강화를
/// <see cref="RunUpgrades.MaxPerSkill"/>개까지 쌓는다. 수치만 올리는 것(위력·절약·신속)과 행동 방식을 바꾸는 것(관통·연쇄·지속)이 있다.
/// 연쇄는 추가 대상에게도 위력이 그대로 들어가 적이 둘 이상이면 피해가 두 배가 되므로 위력을 깎는다.
/// JSON에는 이름으로 저장되므로 새 강화는 끝에 덧붙인다.
/// </summary>
public enum RunUpgradeKind
{
    /// <summary>위력 +30% (피해·회복·보호막).</summary>
    Power,

    /// <summary>MP 비용 절반.</summary>
    Thrift,

    /// <summary>행동 뒤 대기 -20%.</summary>
    Swift,

    /// <summary>엄호를 무시한다 (후열이나 약한 적을 골라 치는 공격).</summary>
    Pierce,

    /// <summary>다른 적 하나에게 더 튀는 대신 위력 -30% (적 하나를 치는 공격).</summary>
    Chain,

    /// <summary>거는 효과가 한 턴 더 간다.</summary>
    Lasting,
}

/// <summary>
/// 강화의 규칙. 전투에서 강화된 스킬은 다른 ID의 행동(<c>fireball+power+pierce</c>)이 되어, 같은 스킬을 쓰는 적에게는 걸리지 않는다.
/// 이름은 강화 수만큼 "+"를 붙인다(화염구++).
/// </summary>
public static class RunUpgrades
{
    public const int MaxPerSkill = 2;

    public const int PowerPercent = 130;

    public const int ThriftPercent = 50;

    public const int SwiftPercent = 80;

    public const int ChainPowerPercent = 70;

    private const char Separator = '+';

    /// <summary>이 행동에 이 강화가 뜻이 있는가 (위력이 없는 행동에 위력 강화는 없다).</summary>
    public static bool Fits(ActionDefinition action, RunUpgradeKind kind) => kind switch
    {
        RunUpgradeKind.Power => action.Power > 0,
        RunUpgradeKind.Thrift => action.MpCost > 1,
        RunUpgradeKind.Swift => true,
        RunUpgradeKind.Pierce => SingleEnemyStrike(action) && !action.IgnoresCover && action.Rule is not (TargetRule.FrontFirst or TargetRule.Random),
        RunUpgradeKind.Chain => SingleEnemyStrike(action),
        RunUpgradeKind.Lasting => action.Applies.Count > 0,
        _ => false,
    };

    /// <summary>강화를 건 행동. ID와 이름이 바뀐다. 강화 순서는 상관없다(늘 같은 순서로 붙인다).</summary>
    public static ActionDefinition Apply(ActionDefinition action, IEnumerable<RunUpgradeKind> kinds)
    {
        var sorted = kinds.Distinct().Order().ToList();
        if (sorted.Count == 0)
        {
            return action;
        }

        var upgraded = action with
        {
            Id = Id(action.Id, sorted),
            Name = action.Name + new string(Separator, sorted.Count),
        };
        foreach (var kind in sorted)
        {
            upgraded = kind switch
            {
                RunUpgradeKind.Power => upgraded with { Power = upgraded.Power * PowerPercent / 100 },
                RunUpgradeKind.Thrift => upgraded with { MpCost = upgraded.MpCost * ThriftPercent / 100 },
                RunUpgradeKind.Swift => upgraded with { Delay = (upgraded.Delay ?? CombatRules.Default.TimeConstant) * SwiftPercent / 100 },
                RunUpgradeKind.Pierce => upgraded with { IgnoresCover = true },
                RunUpgradeKind.Chain => upgraded with { ExtraTargets = upgraded.ExtraTargets + 1, Power = upgraded.Power * ChainPowerPercent / 100 },
                RunUpgradeKind.Lasting => upgraded with { Applies = upgraded.Applies.Select(e => e with { Duration = e.Duration + 1 }).ToList() },
                _ => upgraded,
            };
        }

        return upgraded;
    }

    /// <summary>강화된 행동의 ID: <c>fireball+power+pierce</c>.</summary>
    public static string Id(string actionId, IEnumerable<RunUpgradeKind> kinds) =>
        string.Join(Separator, kinds.Distinct().Order().Select(k => k.ToString().ToLowerInvariant()).Prepend(actionId));

    /// <summary>
    /// 강화된 행동의 ID를 게임 데이터의 행동으로 되돌려 강화를 건다 (전투 기록의 이름을 찾을 때). 강화 ID가 아니거나 모르는 행동이면 null.
    /// </summary>
    public static ActionDefinition? Resolve(GameData data, string id)
    {
        var parts = id.Split(Separator);
        if (parts.Length < 2 || !data.Actions.TryGetValue(parts[0], out var action))
        {
            return null;
        }

        var kinds = new List<RunUpgradeKind>();
        foreach (var part in parts.Skip(1))
        {
            if (!Enum.TryParse<RunUpgradeKind>(part, ignoreCase: true, out var kind))
            {
                return null;
            }

            kinds.Add(kind);
        }

        return Apply(action, kinds);
    }

    public static string Name(RunUpgradeKind kind) => kind switch
    {
        RunUpgradeKind.Power => "위력",
        RunUpgradeKind.Thrift => "절약",
        RunUpgradeKind.Swift => "신속",
        RunUpgradeKind.Pierce => "관통",
        RunUpgradeKind.Chain => "연쇄",
        RunUpgradeKind.Lasting => "지속",
        _ => kind.ToString(),
    };

    /// <summary>이 행동에 걸었을 때 바뀌는 것 한 줄: "위력 85 → 110", "엄호를 무시한다".</summary>
    public static string Describe(ActionDefinition action, RunUpgradeKind kind)
    {
        var after = Apply(action, [kind]);
        return kind switch
        {
            RunUpgradeKind.Power => $"위력 {action.Power} → {after.Power}",
            RunUpgradeKind.Thrift => $"MP {action.MpCost} → {after.MpCost}",
            RunUpgradeKind.Swift => $"행동 뒤 대기 {action.Delay ?? CombatRules.Default.TimeConstant} → {after.Delay}",
            RunUpgradeKind.Pierce => "엄호를 무시하고 후열도 친다",
            RunUpgradeKind.Chain => $"다른 적 {after.ExtraTargets}명에게 더 튄다, 위력 {action.Power} → {after.Power}",
            RunUpgradeKind.Lasting => "거는 효과가 한 턴 더 간다",
            _ => Name(kind),
        };
    }

    private static bool SingleEnemyStrike(ActionDefinition action) =>
        action is { Effect: ActionEffect.Damage, Side: TargetSide.Enemy, Scope: TargetScope.Single };
}
