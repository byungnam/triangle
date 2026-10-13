using Microsoft.Xna.Framework;
using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Effects;
using Triangle.Core.Skills;
using Triangle.Core.Units;

namespace Triangle.Desktop.Rendering;

/// <summary>설명 카드의 한 줄.</summary>
internal sealed record TextLine(string Text, Color Color, int Size = 15, bool Bold = false);

/// <summary>
/// 행동(스킬)과 효과의 화면 설명. 데이터의 수치에서 문장을 만든다 (데이터의 description은 편집기 메모라 쓰지 않는다).
/// </summary>
internal static class ActionText
{
    /// <summary>"MP 15 · 대기 1500"처럼 비용. 비용이 없으면 빈 문자열. 미리보기가 있으면 대기는 뺀다(따로 보여 준다).</summary>
    public static string Cost(ActionDefinition action, ActionPreview? preview = null)
    {
        var parts = new List<string>();
        var mp = preview?.MpCost ?? action.MpCost;
        if (mp > 0)
        {
            parts.Add(preview is not null && mp != action.MpCost ? $"MP {mp} (기본 {action.MpCost})" : $"MP {mp}");
        }

        if (action.HpCost > 0)
        {
            parts.Add($"HP {action.HpCost}");
        }

        if (action.Delay is { } delay && preview is null)
        {
            parts.Add($"대기 {delay}");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>"적 1명 · 전위 우선"처럼 누구에게 쓰는가.</summary>
    public static string Target(ActionDefinition action, GameData data)
    {
        if (action.Side == TargetSide.Self || action.Effect == ActionEffect.Summon)
        {
            return "자신";
        }

        var side = action.Side == TargetSide.Enemy ? "적" : "아군";
        var parts = new List<string>();
        var rows = action.Rows switch
        {
            RowRestriction.FrontOnly => "전열 ",
            RowRestriction.BackOnly => "후열 ",
            _ => "",
        };

        if (action.Scope == TargetScope.All)
        {
            parts.Add(rows.Length > 0 ? $"{side} {rows}전체" : $"{side} 전체");
        }
        else
        {
            parts.Add($"{side} {rows}1명");
            parts.Add(Rule(action, data));
        }

        if (action.Side == TargetSide.Enemy && action.Scope == TargetScope.Single && action.IgnoresCover)
        {
            parts.Add("엄호 무시");
        }

        return string.Join(" · ", parts);
    }

    private static string Rule(ActionDefinition action, GameData data) => action.Rule switch
    {
        TargetRule.FrontFirst => "전위 우선",
        TargetRule.BackFirst => "후위 우선",
        TargetRule.LowestHpRatio => "HP 비율 최저",
        TargetRule.LowestMpRatio => "MP 비율 최저",
        TargetRule.WithDebuffFirst => "디버프 걸린 대상 우선",
        TargetRule.WithoutEffectFirst when action.Applies.Count > 0 && data.Effects.TryGetValue(action.Applies[0].EffectId, out var e) =>
            $"{e.Name} 없는 대상 우선",
        TargetRule.WithoutEffectFirst => "효과 없는 대상 우선",
        _ => "무작위",
    };

    /// <summary>"물리 피해 85 ×2회"처럼 주 효과. 예상치가 있으면 함께 보여 준다.</summary>
    public static string Outcome(ActionDefinition action, ActionPreview? preview = null)
    {
        string WithPreview(string label) =>
            preview is { Amount: > 0 } p ? $"{label} {p.Amount} (위력 {action.Power})" : $"{label} 위력 {action.Power}";

        var text = action.Effect switch
        {
            ActionEffect.Damage => WithPreview(action.DamageType == DamageType.Physical ? "물리 피해" : "마법 피해"),
            ActionEffect.Heal => WithPreview("HP 회복"),
            ActionEffect.Shield => WithPreview("보호막"),
            ActionEffect.RestoreMp => WithPreview("MP 회복"),
            ActionEffect.Summon when action.Summon is { } s => $"{s.Name} 소환",
            _ => "",
        };

        if (action.Hits > 1)
        {
            text += $" ×{action.Hits}회";
        }

        return text;
    }

    /// <summary>한 줄 요약, 예: "적 1명 · 전위 우선 · 물리 피해 85 · 출혈 3턴".</summary>
    public static string Summary(ActionDefinition action, GameData data, ActionPreview? preview = null)
    {
        var parts = new List<string> { Target(action, data) };
        if (Outcome(action, preview) is { Length: > 0 } main)
        {
            parts.Add(main);
        }

        var bonus = preview?.EffectDurationBonus ?? 0;
        parts.AddRange(action.Applies.Select(a => $"{EffectName(a.EffectId, data)} {a.Duration + bonus}턴"));
        if (action.Chant > 1)
        {
            parts.Add($"영창 {action.Chant}회");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>설명 카드: 이름, 비용, 대상, 효과, 특수 규칙, 거는 효과의 내용, 요구 패시브.</summary>
    public static IReadOnlyList<TextLine> Details(ActionDefinition action, GameData data, ActionPreview? preview = null, SkillSet? skills = null)
    {
        var lines = new List<TextLine> { new(action.Name, Theme.Text, 19, Bold: true) };
        var source = action.Universal ? "공용 행동 (누구나)"
            : action.Weapon is { } weapon ? $"{data.Masteries[weapon].Name} 무기를 들면 쓴다"
            : $"주는 장비: {string.Join(", ", data.ItemsGranting(action.Id).Select(i => i.TypeOrName).Distinct())}";
        lines.Add(new(source, Theme.TextDim, 14));

        var cost = Cost(action, preview);
        var delay = preview is null ? "" : $"행동 뒤 대기 {preview.Delay}" + (action.Delay is { } d ? $" (대기 수치 {d})" : "");
        lines.Add(new(string.Join(" · ", new[] { cost.Length > 0 ? cost : "비용 없음", delay }.Where(s => s.Length > 0)), Theme.Cover));
        lines.Add(new($"대상: {Target(action, data)}", Theme.Text));

        if (Outcome(action, preview) is { Length: > 0 } main)
        {
            lines.Add(new(main, action.Effect == ActionEffect.Damage ? Theme.Damage : Theme.Heal));
        }

        if (preview is { Amount: > 0 })
        {
            lines.Add(new(action.Effect == ActionEffect.Damage
                ? "  지금 장비·패시브 기준 예상치 (대상 방어로 줄기 전)"
                : "  지금 장비·패시브 기준 예상치", Theme.TextDim, 13));
        }

        lines.AddRange(Mechanics(action, data).Select(m => new TextLine(m, Theme.Text)));

        var bonus = preview?.EffectDurationBonus ?? 0;
        foreach (var application in action.Applies)
        {
            if (!data.Effects.TryGetValue(application.EffectId, out var effect))
            {
                continue;
            }

            var color = effect.Kind == EffectKind.Buff ? Theme.Heal : Theme.Enemy;
            var duration = bonus > 0 ? $"{application.Duration + bonus}턴 (무기 +{bonus})" : $"{application.Duration}턴";
            lines.Add(new($"{effect.Name} {duration}: {Effect(effect)}", color));
        }

        if (action.Requirements.Count > 0)
        {
            var requirement = string.Join(", ", action.Requirements.Select(r => $"{data.Skills[r.SkillId].Name} Lv{r.Level}"));
            var met = skills?.Meets(action.Requirements);
            lines.Add(new($"요구 패시브: {requirement}{(met == false ? " (모자람)" : "")}", met == false ? Theme.Enemy : Theme.TextDim));
        }

        return lines;
    }

    /// <summary>연쇄, 처형, 넘어뜨림 같은 특수 규칙 설명.</summary>
    private static IEnumerable<string> Mechanics(ActionDefinition action, GameData data)
    {
        if (action.ExtraTargets > 0)
        {
            yield return $"다른 대상 {action.ExtraTargets}명에게 연쇄 (엄호 무시)";
        }

        if (action.MissingHpBonusPercent > 0)
        {
            yield return $"대상이 잃은 HP 비율만큼 피해 증가 (최대 +{action.MissingHpBonusPercent}%)";
        }

        if (action.BonusAgainst is { } against)
        {
            yield return $"{EffectName(against.EffectId, data)} 걸린 대상에게 피해 +{against.Percent}%";
        }

        if (action.PushBack > 0)
        {
            yield return $"넘어뜨림: 대상의 다음 차례를 늦춘다 (대기 {action.PushBack}), 영창도 끊는다";
        }

        if (action.MpDamage > 0)
        {
            yield return $"대상 MP {action.MpDamage} 소각";
        }

        if (action.MoveTo is { } row)
        {
            yield return $"대상을 {(row == Row.Front ? "전위" : "후위")}로 옮긴다";
        }

        if (action.RemovesDebuffs)
        {
            yield return "디버프를 모두 지운다";
        }

        if (action.Chant > 1)
        {
            yield return $"영창 {action.Chant}회: 연달아 {action.Chant}번 써야 효과가 난다 (다른 행동, 기절, 넘어뜨림이 끊는다)";
        }

        if (action.OncePerBattle)
        {
            yield return "전투당 한 번만 효과가 난다";
        }

        if (action.Summon is { } summon)
        {
            var summonRow = summon.Row == Row.Front ? "전위" : "후위";
            var duration = summon.Duration is { } d ? $"정령 자신의 행동 {d}번 동안" : "전투 끝까지";
            yield return $"{summonRow}에 나타나 {duration} 남는다";
            if (summon.StatSkill is { } stat && data.Skills.TryGetValue(stat, out var skill))
            {
                yield return $"{skill.Name} 레벨당 능력치 +{summon.StatPercentPerLevel}%";
            }
        }
    }

    public static string EffectName(string effectId, GameData data) =>
        data.Effects.TryGetValue(effectId, out var effect) ? effect.Name : effectId;

    /// <summary>"주는 피해 +20%, 방어 -30%"처럼 효과 내용.</summary>
    public static string Effect(EffectDefinition effect)
    {
        var parts = effect.Modifiers.Select(m => m.Kind switch
        {
            EffectModifierKind.PowerPercent => $"주는 피해 {Signed(m.Percent)}%",
            EffectModifierKind.DefensePercent => $"방어 {Signed(m.Percent)}%",
            EffectModifierKind.DelayReductionPercent => m.Percent >= 0 ? $"행동 대기 -{m.Percent}%" : $"행동 대기 +{-m.Percent}%",
            EffectModifierKind.DamageTakenReductionPercent => m.Percent >= 0 ? $"받는 피해 -{m.Percent}%" : $"받는 피해 +{-m.Percent}%",
            _ => m.Kind.ToString(),
        }).ToList();

        if (effect.TickHpPercent < 0)
        {
            parts.Add($"매 턴 최대 HP의 {-effect.TickHpPercent}% 피해");
        }
        else if (effect.TickHpPercent > 0)
        {
            parts.Add($"매 턴 최대 HP의 {effect.TickHpPercent}% 회복");
        }

        if (effect.Stun)
        {
            parts.Add("행동하지 못한다");
        }

        if (effect.Taunt)
        {
            parts.Add("적의 단일 공격을 끌어온다");
        }

        if (effect.NoCover)
        {
            parts.Add("후위를 엄호하지 못한다");
        }

        return string.Join(", ", parts);
    }

    private static string Signed(int value) => value >= 0 ? $"+{value}" : $"{value}";
}
