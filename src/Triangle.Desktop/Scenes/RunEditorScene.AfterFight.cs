using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Triangle.Core.Combat;
using Triangle.Core.Runs;
using Triangle.Core.Tactics;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 정비 화면 (2026-10-24, 사용자 결정: 전투 직후에 바로 결과를 보고 고르고 고친다). 편성 화면 위에 띠를 붙인다.
/// - 왼쪽: 방금 전투의 결과 (승패, 골드, 봉인, 남은 목숨).
/// - 가운데: 보상 카드. 패시브와 줄 상한은 지금 고른 멤버가(그 클래스가 아니면 받을 수 있는 첫 멤버가) 받고, 강화는 걸 사본을 고른다.
/// - 오른쪽: 졌으면 다시 싸우기, 이겼으면 건너뛰기나 지도로.
/// 줄마다 방금 전투에서 쓴 횟수와 쓰지 못한 횟수를 붙인다. 그때 켜져 있던 줄과 조건·행동이 같은 줄에만 붙는다.
/// </summary>
internal sealed partial class RunEditorScene
{
    private const int StripHeight = 136;
    private const int StripResultWidth = 330;
    private const int StripButtonWidth = 190;
    private const int CopyPickerWidth = 760;

    private int? _rewardPicking;
    private string? _taken;
    private IReadOnlyList<UnitSummary>? _summaries;

    /// <summary>방금 전투에서 한 멤버가 줄마다 쓴 횟수: 지금 줄 순서대로(그때와 다른 줄이면 null), 끝은 기본 공격 줄.</summary>
    private sealed record LineUsage(IReadOnlyList<TacticUsage?> Lines, TacticUsage? Basic, UnitSummary Unit);

    private Widget BuildStrip()
    {
        var row = new HorizontalStackPanel { Spacing = 14 };
        row.Widgets.Add(BuildResult());
        var middleWidth = Body.Width - 28 - StripResultWidth - StripButtonWidth - 28;
        row.Widgets.Add(BuildOffer(middleWidth));
        row.Widgets.Add(BuildStripButtons());
        return row;
    }

    private Widget BuildResult()
    {
        var panel = new VerticalStackPanel { Spacing = 4, Width = StripResultWidth };
        if (Session.LastFight is { } fight)
        {
            var lines = RunText.Battle(fight.Battle, Run, Data);
            panel.Widgets.Add(W.Label(lines[0].Text, 18, lines[0].Color, bold: true, width: StripResultWidth, wrap: true));
            foreach (var line in lines.Skip(1))
            {
                panel.Widgets.Add(W.Label(line.Text, 14, line.Color, width: StripResultWidth, wrap: true));
            }

            if (fight.Battle.Result.Outcome != CombatOutcome.Victory)
            {
                var left = fight.Battle.Result.Combatants.Where(c => c.Side == CombatSide.Enemy && !c.IsSummon).ToList();
                var hp = left.Sum(c => c.MaxHp) is var max and > 0 ? 100 * left.Sum(c => Math.Max(0, c.Hp)) / max : 0;
                panel.Widgets.Add(W.Label($"적은 HP {hp}%를 남겼습니다.", 14, Theme.TextDim, width: StripResultWidth, wrap: true));
            }
        }
        else if (Run.Phase == RunPhase.Retry)
        {
            panel.Widgets.Add(W.Label("패배", 18, Theme.Enemy, bold: true));
            panel.Widgets.Add(W.Label($"남은 목숨 {Run.Lives}. 고쳐서 같은 적과 같은 조건으로 다시 싸웁니다.", 14, Theme.Enemy, width: StripResultWidth, wrap: true));
        }

        return panel;
    }

    private Widget BuildOffer(int width)
    {
        var panel = new VerticalStackPanel { Spacing = 6, Width = width };
        if (Run.Phase == RunPhase.Retry)
        {
            panel.Widgets.Add(W.Label("같은 적과 같은 시드로 다시 싸웁니다. 아무것도 바꾸지 않으면 결과도 같습니다.", 15, Theme.Text, width: width, wrap: true));
            panel.Widgets.Add(W.Label("줄 옆의 수를 보고 고치세요: 안 쓴 줄, MP가 모자라 못 쓴 줄, 먼저 쓰러진 멤버. 장비와 서는 줄도 바꿀 수 있습니다.", 14, Theme.TextDim, width: width, wrap: true));
            return panel;
        }

        if (Run.Phase != RunPhase.Reward)
        {
            panel.Widgets.Add(W.Label(_taken is null ? "고를 카드가 없습니다." : $"고름: {_taken}", 16, Theme.Gold, bold: true, width: width, wrap: true));
            panel.Widgets.Add(W.Label("받은 것을 줄에 놓거나 줄을 고친 뒤 지도로 갑니다.", 14, Theme.TextDim, width: width, wrap: true));
            return panel;
        }

        panel.Widgets.Add(W.Label("보상: 하나 고르기", 14, Theme.TextDim));
        var cards = new HorizontalStackPanel { Spacing = 8 };
        var cardWidth = (width - 8 * (Run.Offer.Count - 1)) / Math.Max(1, Run.Offer.Count);
        for (var i = 0; i < Run.Offer.Count; i++)
        {
            cards.Widgets.Add(BuildOfferCard(i, cardWidth));
        }

        panel.Widgets.Add(cards);
        return panel;
    }

    private Widget BuildOfferCard(int index, int width)
    {
        var card = Run.Offer[index];
        var target = Target(index);
        var content = new VerticalStackPanel { Spacing = 2, Width = width - 16 };
        var head = new HorizontalStackPanel { Spacing = 6 };
        var (icon, color) = RunText.CardIcon(card, Data);
        head.Widgets.Add(W.Icon(icon, 20, color));
        head.Widgets.Add(W.Chip(RunText.CardKind(card.Kind), RunText.CardColor(card.Kind) * 0.55f, 12));
        content.Widgets.Add(head);
        content.Widgets.Add(W.Label(RunText.CardName(card, Data), 15, Theme.Text, bold: true, width: width - 16, wrap: true));
        var note = card.Kind switch
        {
            RunCardKind.Active => $"사본 {Run.Copies.GetValueOrDefault(card.Id!)} → {Run.Copies.GetValueOrDefault(card.Id!) + 1}장",
            RunCardKind.Upgrade => RunUpgrades.Rule(Enum.Parse<RunUpgradeKind>(card.Id!)),
            RunCardKind.Passive when target is not null => $"→ {Run.Name(Data, target)} (Lv{target.Level(card.Id!)} → {target.Level(card.Id!) + 1})",
            RunCardKind.Line when target is not null => $"→ {Run.Name(Data, target)} (줄 상한 {target.Earned} → {target.Earned + 1})",
            _ => Run.WhyCannotTake(Data, index, Member) ?? "",
        };
        content.Widgets.Add(W.Label(note, 12, Theme.Cover, width: width - 16, wrap: true));
        var button = Widgets.StyledButton(content, Theme.Button, Theme.ButtonHover);
        button.Width = width;
        button.Padding = new Thickness(8, 5);
        button.Tooltip = string.Join("\n", RunText.Card(card, Run, Data).Select(l => l.Text));
        var why = card.Kind is RunCardKind.Passive or RunCardKind.Line && target is null ? Run.WhyCannotTake(Data, index, Member) : null;
        button.Enabled = why is null;
        button.Click += (_, _) => Choose(index);
        return button;
    }

    /// <summary>패시브와 줄 상한을 받을 멤버: 지금 고른 멤버가 받을 수 있으면 그 멤버, 아니면 받을 수 있는 첫 멤버. 액티브와 강화는 null.</summary>
    private RunMember? Target(int index) =>
        Run.Offer[index].Kind is RunCardKind.Passive or RunCardKind.Line
            ? new[] { Member }.Concat(Run.Living).FirstOrDefault(m => !m.Dead && Run.WhyCannotTake(Data, index, m) is null)
            : null;

    private void Choose(int index)
    {
        var card = Run.Offer[index];
        if (card.Kind == RunCardKind.Upgrade)
        {
            _rewardPicking = index;
            Refresh();
            return;
        }

        var target = Target(index);
        Take(index, target, null, card.Kind == RunCardKind.Active ? RunText.CardName(card, Data) : $"{RunText.CardName(card, Data)} → {(target is null ? "" : Run.Name(Data, target))}");
    }

    private void Take(int index, RunMember? member, string? copy, string taken)
    {
        if (Run.WhyCannotTake(Data, index, member, copy) is { } why)
        {
            Refuse(why);
            return;
        }

        Run.Take(Data, index, member, copy);
        _taken = taken;
        _rewardPicking = null;
        Session.Notice = null;
        Session.Save();
        Refresh();
    }

    private Widget BuildStripButtons()
    {
        var panel = new VerticalStackPanel { Spacing = 8, Width = StripButtonWidth };
        switch (Run.Phase)
        {
            case RunPhase.Retry:
                panel.Widgets.Add(Button("다시 싸우기 ▶", () => _retry?.Invoke(), _retry is null ? "다시 싸울 수 없다" : Run.WhyCannotRetry(), accent: true, width: StripButtonWidth));
                panel.Widgets.Add(W.Label($"남은 목숨 {Run.Lives}", 15, Theme.Enemy, bold: true));
                break;
            case RunPhase.Reward:
                panel.Widgets.Add(Button("건너뛰기", () =>
                {
                    Run.Skip();
                    _taken = "건너뜀";
                    Session.Save();
                    Refresh();
                }, width: StripButtonWidth));
                panel.Widgets.Add(W.Label("고르거나 건너뛰면 지도로 갈 수 있습니다.", 13, Theme.TextDim, width: StripButtonWidth, wrap: true));
                break;
            default:
                panel.Widgets.Add(Button("지도로 ▶", _back, accent: true, width: StripButtonWidth));
                break;
        }

        return panel;
    }

    /// <summary>강화 카드를 걸 사본: 그 강화가 맞는 사본마다(줄에 놓인 것 먼저) 단추. 단추에 바뀌는 수치를 적는다.</summary>
    private Widget BuildCopyPicker(int index)
    {
        var card = Run.Offer[index];
        var kind = Enum.Parse<RunUpgradeKind>(card.Id!);
        var popup = Box(CopyPickerWidth, null, border: Theme.Gold, spacing: 8);
        popup.Padding = new Thickness(18, 14);
        var head = new HorizontalStackPanel { Spacing = 12 };
        head.Widgets.Add(W.Label($"{Korean.EulReul(RunText.CardName(card, Data))} 걸 사본", 20, Theme.Gold, bold: true, width: CopyPickerWidth - 36 - 132));
        head.Widgets.Add(Button("닫기 (Esc)", () =>
        {
            _rewardPicking = null;
            Refresh();
        }, width: 120));
        popup.Widgets.Add(head);
        popup.Widgets.Add(W.Label(RunUpgrades.Rule(kind), 15, Theme.TextDim));
        var copies = Run.UpgradableCopies(Data).Where(c => RunUpgrades.Fits(RunUpgrades.Action(Data, c), kind))
            .OrderBy(c => Run.UpgradeHolder(c) is null ? 1 : 0).ToList();
        if (copies.Count == 0)
        {
            popup.Widgets.Add(W.Label("이 강화를 걸 수 있는 사본이 없습니다. 건너뛰거나 다른 카드를 고르세요.", 15, Theme.TextDim));
        }

        var list = new VerticalStackPanel { Spacing = 6 };
        foreach (var copy in copies)
        {
            var why = Run.WhyCannotTake(Data, index, null, copy);
            var label = $"{RunText.Copy(Run, Data, copy)}  —  {RunUpgrades.Describe(RunUpgrades.Action(Data, copy), kind)}";
            var name = RunUpgrades.Action(Data, copy).Name;
            list.Widgets.Add(Button(label, () => Take(index, null, copy, $"{RunText.CardName(card, Data)} → {name}"), why, accent: why is null, width: CopyPickerWidth - 36, size: 15));
        }

        popup.Widgets.Add(new ScrollPane { Content = list, ShowHorizontalScrollBar = false, Height = Math.Min(420, copies.Count * 44 + 8) });
        return Overlay(popup);
    }

    // ── 방금 전투에서 쓴 횟수 ─────────────────────────────

    /// <summary>방금 전투가 있으면 이 멤버의 줄마다 쓴 횟수. 그 전투에 없던 멤버(합류)나 전투가 없으면 null.</summary>
    private LineUsage? Usage(RunMember member)
    {
        if (Session.LastFight is not { } fight || !fight.Fought.TryGetValue(member.Id, out var fought))
        {
            return null;
        }

        _summaries ??= CombatSummary.From(fight.Battle.Result);
        if (_summaries.FirstOrDefault(u => u.Id == member.Id) is not { } unit || unit.Tactics.Count != fought.Count + 1)
        {
            return null;
        }

        // 지금 줄마다 그때 켜져 있던 같은 줄(조건과 행동이 같은)을 위에서부터 하나씩 짝짓는다.
        var left = fought.Select((t, i) => (Key: Key(t), Use: unit.Tactics[i])).ToList();
        var lines = new List<TacticUsage?>();
        foreach (var tactic in member.Tactics)
        {
            var match = left.FindIndex(l => l.Key == Key(tactic));
            lines.Add(match < 0 ? null : left[match].Use);
            if (match >= 0)
            {
                left.RemoveAt(match);
            }
        }

        return new LineUsage(lines, unit.Tactics[^1], unit);
    }

    private static string Key(Tactic tactic) => tactic.ActionId + "|" + string.Join(",", tactic.Conditions.Select(c => $"{c.Condition}:{c.Value}"));

    /// <summary>줄 옆에 쓴 횟수와 쓰지 못한 횟수를 붙인다. 붙였으면 true.</summary>
    private bool AddUsage(HorizontalStackPanel row, TacticUsage? use)
    {
        if (use is null)
        {
            return false;
        }

        row.Widgets.Add(use.Used > 0 ? W.Chip($"{use.Used}번 씀", Theme.Heal * 0.45f, 12) : W.Chip("안 씀", Theme.TextDim * 0.45f, 12));
        foreach (var (reason, count) in use.Blocked.Where(b => b.Value > 0))
        {
            var text = reason switch
            {
                WaitReason.NotEnoughResource => "MP 부족",
                WaitReason.NoTarget => "대상 없음",
                _ => "못 씀",
            };
            row.Widgets.Add(W.Chip($"{text} {count}", Theme.Enemy * 0.5f, 12));
        }

        return true;
    }

    /// <summary>멤버 목록에 붙는 방금 전투 한 줄: 준 피해·회복, 쓰러짐.</summary>
    private (string Text, Color Color)? FightLine(RunMember member)
    {
        if (Usage(member) is not { } usage)
        {
            return null;
        }

        var unit = usage.Unit;
        var parts = new List<string> { $"피해 {unit.DamageDealt + unit.SummonDamage}" };
        if (unit.Healing + unit.SummonHealing + unit.Shielding > 0)
        {
            parts.Add($"회복 {unit.Healing + unit.SummonHealing + unit.Shielding}");
        }

        if (unit.DownedAt is not null)
        {
            parts.Add("쓰러짐");
        }

        return (string.Join(" · ", parts), unit.DownedAt is null ? Theme.TextDim : Theme.Enemy);
    }
}
