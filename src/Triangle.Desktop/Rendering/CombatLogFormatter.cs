using Microsoft.Xna.Framework;
using Triangle.Core.Combat;
using Triangle.Core.Effects;
using static Triangle.Desktop.Rendering.Korean;

namespace Triangle.Desktop.Rendering;

internal sealed record LogLine(string Text, Color Color, bool Indented = false);

/// <summary>전투 이벤트를 짧은 한국어 문장으로 바꾼다. 로그가 두 칸으로 나뉘므로 짧게 쓴다.</summary>
internal sealed class CombatLogFormatter(
    IReadOnlyDictionary<string, string> unitNames,
    IReadOnlyDictionary<string, string> actionNames,
    IReadOnlyDictionary<string, EffectDefinition> effects)
{
    /// <summary>화면에 줄로 보여줄 필요가 없는 이벤트(턴 시작)면 null.</summary>
    public LogLine? Format(CombatEvent e) => e switch
    {
        TurnStarted => null,

        ActionUsed a => new($"{Name(a.ActorId)}의 {actionNames[a.ActionId]}", Theme.Text),

        Waited w => new(w.Reason switch
        {
            WaitReason.NotEnoughResource => $"{EunNeun(Name(w.ActorId))} 자원이 부족해 행동하지 못했다.",
            WaitReason.NoTarget => $"{EunNeun(Name(w.ActorId))} 대상이 없어 행동하지 못했다.",
            _ => $"{EunNeun(Name(w.ActorId))} 기다린다.",
        }, Theme.TextDim),

        Covered c => new($"{IGa(Name(c.CoverId))} {EulReul(Name(c.ProtectedId))} 감쌌다!", Theme.Cover, Indented: true),

        Damaged d => new($"{Name(d.TargetId)}에게 {d.Amount} 피해 (HP {d.HpAfter})", Theme.Damage, Indented: true),

        Healed h => new($"{Name(h.TargetId)} {h.Amount} 회복 (HP {h.HpAfter})", Theme.Heal, Indented: true),

        Died d => new($"{IGa(Name(d.UnitId))} 쓰러졌다!", Theme.Death, Indented: true),

        MpRestored m => new($"{Name(m.TargetId)} MP {m.Amount} 회복 (MP {m.MpAfter})", Theme.MpBar, Indented: true),

        EffectApplied a => new(
            a.Refreshed
                ? $"{Name(a.TargetId)}의 {Effect(a.EffectId)} 갱신 ({a.Duration}턴)"
                : $"{Name(a.TargetId)}에게 {Effect(a.EffectId)} ({a.Duration}턴)",
            effects[a.EffectId].Kind == EffectKind.Buff ? Theme.Cover : Theme.Enemy,
            Indented: true),

        EffectTicked t => t.HpChange < 0
            ? new($"{Name(t.TargetId)} {EuroRo(Effect(t.EffectId))} {-t.HpChange} 피해 (HP {t.HpAfter})", Theme.Damage, Indented: true)
            : new($"{Name(t.TargetId)} {EuroRo(Effect(t.EffectId))} {t.HpChange} 회복 (HP {t.HpAfter})", Theme.Heal, Indented: true),

        EffectExpired x => new($"{Name(x.TargetId)}의 {Effect(x.EffectId)} 효과가 끝났다", Theme.TextDim, Indented: true),

        CombatEnded end => new(end.Outcome switch
        {
            CombatOutcome.Victory => "전투 종료 — 승리!",
            CombatOutcome.Defeat => "전투 종료 — 패배...",
            _ => "전투 종료 — 무승부 (행동 횟수 상한)",
        }, end.Outcome == CombatOutcome.Victory ? Theme.Heal : Theme.Enemy),

        _ => null,
    };

    private string Name(string unitId) => unitNames[unitId];

    private string Effect(string effectId) => effects[effectId].Name;
}
