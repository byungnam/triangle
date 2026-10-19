using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Units;

namespace Triangle.Desktop.Rendering;

/// <summary>
/// 전투 기록 화면의 요약 칸. 아군은 멤버마다 행동·피해·회복, 전술별 사용 횟수(막대)와 막힌 횟수, 잃은 차례와 이유를,
/// 적은 피해 합계만 보여준다(탐사 기록처럼 적의 전술 조건은 숨긴다). 칸에 다 들어가지 않으면 위에서부터 스크롤한다.
/// </summary>
internal sealed class CombatSummaryView
{
    private const int Padding = 16;
    private const int Indent = 18;
    private const int LineHeight = 24;
    private const int HeadingHeight = 30;
    private const int SectionHeight = 34;
    private const int UnitGap = 10;
    private const int PriorityWidth = 20;
    private const int BarWidth = 120;
    private const int CountWidth = 110;
    private const int ColumnGap = 10;

    private abstract record Line(int Height);

    private sealed record SectionLine(string Text, Color Color) : Line(SectionHeight);

    private sealed record HeadingLine(string Name, string Detail, Color Color, string Status, Color StatusColor) : Line(HeadingHeight);

    private sealed record TextLine(string Text, Color Color) : Line(LineHeight);

    private sealed record TacticLine(int Priority, string Label, int Used, int Blocked) : Line(LineHeight);

    private sealed record GapLine(int Size) : Line(Size);

    private readonly Ui _ui;
    private readonly GameData _data;
    private readonly IReadOnlyList<UnitSummary> _summaries;
    private readonly IReadOnlyDictionary<string, Combatant> _combatants;
    private readonly IReadOnlyDictionary<string, string> _names;
    private readonly string _enemyTitle;

    /// <summary>막대 길이의 기준: 아군 전술 중 가장 많이 고른 횟수(사용 + 막힘).</summary>
    private readonly int _scale;

    private List<Line>? _lines;
    private int _linesWidth;

    /// <param name="names">유닛 ID → 화면 이름 (같은 이름이면 "훈련병 A"처럼 구분한 이름).</param>
    /// <param name="enemyTitle">적 묶음의 제목 (적 팀 이름).</param>
    public CombatSummaryView(Ui ui, GameData data, CombatResult result, IReadOnlyDictionary<string, string> names, string enemyTitle)
    {
        _ui = ui;
        _data = data;
        _summaries = CombatSummary.From(result);
        _combatants = result.Combatants.ToDictionary(c => c.Id);
        _names = names;
        _enemyTitle = enemyTitle;
        _scale = Math.Max(1, _summaries
            .Where(s => s.Side == CombatSide.Ally)
            .SelectMany(s => s.Tactics)
            .Select(t => t.Used + t.BlockedTotal)
            .DefaultIfEmpty(0)
            .Max());
    }

    /// <summary>칸 높이를 넘는 만큼 (스크롤할 수 있는 최대 픽셀).</summary>
    public int MaxScroll(Rectangle area) => Math.Max(0, Lines(area.Width).Sum(l => l.Height) + Padding * 2 - area.Height);

    /// <param name="scroll">위에서부터 내린 픽셀 (0 ~ <see cref="MaxScroll"/>).</param>
    public void Draw(SpriteBatch batch, Rectangle area, int scroll)
    {
        var x = area.Left + Padding;
        var right = area.Right - Padding;
        var y = area.Top + Padding - scroll;
        foreach (var line in Lines(area.Width))
        {
            // 칸에 온전히 들어가는 줄만 그린다 (잘린 줄은 그리지 않는다).
            if (y >= area.Top && y + line.Height <= area.Bottom)
            {
                DrawLine(batch, line, x, right, y);
            }

            y += line.Height;
        }

        if (scroll < MaxScroll(area))
        {
            var font = _ui.Font(14);
            const string more = "▼ 아래에 더 있음 (휠)";
            var size = font.MeasureString(more);
            var at = new Vector2(right - size.X, area.Bottom - Padding - size.Y);
            _ui.Fill(batch, new Rectangle((int)at.X - 6, (int)at.Y - 2, (int)size.X + 12, (int)size.Y + 4), Theme.Panel);
            _ui.Text(batch, font, more, at, Theme.Cover);
        }
    }

    private void DrawLine(SpriteBatch batch, Line line, int x, int right, int y)
    {
        switch (line)
        {
            case SectionLine s:
                _ui.Text(batch, _ui.BoldFont(20), s.Text, new Vector2(x, y + 6), s.Color);
                break;
            case HeadingLine h:
            {
                var nameFont = _ui.BoldFont(18);
                _ui.Text(batch, nameFont, h.Name, new Vector2(x, y + 6), h.Color);
                var detailFont = _ui.Font(15);
                _ui.Text(batch, detailFont, h.Detail, new Vector2(x + nameFont.MeasureString(h.Name).X + 10, y + 9), Theme.TextDim);
                _ui.Text(batch, detailFont, h.Status, new Vector2(right - detailFont.MeasureString(h.Status).X, y + 9), h.StatusColor);
                break;
            }
            case TextLine t:
                _ui.Text(batch, _ui.Font(16), t.Text, new Vector2(x + Indent, y + 3), t.Color);
                break;
            case TacticLine t:
                DrawTactic(batch, t, x + Indent, right, y);
                break;
        }
    }

    /// <summary>"순위  조건 → 행동  [막대]  n회 · 막힘 m". 한 번도 고르지 않은 전술은 흐리게 그린다.</summary>
    private void DrawTactic(SpriteBatch batch, TacticLine t, int x, int right, int y)
    {
        var font = _ui.Font(16);
        var idle = t.Used == 0 && t.Blocked == 0;
        var priority = t.Priority.ToString();
        _ui.Text(batch, _ui.Font(15), priority, new Vector2(x + PriorityWidth - _ui.Font(15).MeasureString(priority).X, y + 4), Theme.TextDim);
        _ui.Text(batch, font, t.Label, new Vector2(x + PriorityWidth + ColumnGap, y + 3), idle ? Theme.TextDim : Theme.Text);

        var bar = new Rectangle(BarLeft(right), y + 8, BarWidth, 10);
        _ui.Fill(batch, bar, Theme.BarBack);
        var used = BarWidth * t.Used / _scale;
        var blocked = BarWidth * t.Blocked / _scale;
        _ui.Fill(batch, bar with { Width = used }, Theme.Gold);
        _ui.Fill(batch, new Rectangle(bar.X + used, bar.Y, blocked, bar.Height), Theme.Enemy);

        var count = t.Blocked > 0 ? $"{t.Used}회 · 막힘 {t.Blocked}" : $"{t.Used}회";
        var countFont = _ui.Font(15);
        _ui.Text(batch, countFont, count, new Vector2(right - countFont.MeasureString(count).X, y + 4),
            t.Blocked > 0 ? Theme.Enemy : idle ? Theme.TextDim : Theme.Text);
    }

    private static int BarLeft(int right) => right - CountWidth - ColumnGap - BarWidth;

    /// <summary>폭에 맞춰 줄을 만든다 (문장은 줄을 바꾸고, 전술 이름은 막대 앞에서 자른다). 폭이 같으면 다시 만들지 않는다.</summary>
    private List<Line> Lines(int areaWidth)
    {
        if (_lines is not null && _linesWidth == areaWidth)
        {
            return _lines;
        }

        var x = Padding + Indent;
        var right = areaWidth - Padding;
        var textWidth = right - x;
        var labelWidth = BarLeft(right) - ColumnGap - (x + PriorityWidth + ColumnGap);
        var font = _ui.Font(16);

        var lines = new List<Line>();
        void Text(string text, Color color) => lines.AddRange(TextLayout.Wrap(font, text, textWidth).Select(l => new TextLine(l, color)));

        lines.Add(new SectionLine("아군", Theme.Ally));
        foreach (var unit in _summaries.Where(s => s.Side == CombatSide.Ally))
        {
            lines.Add(Heading(unit, Theme.Ally));
            Text(Stats(unit), Theme.Text);
            foreach (var usage in unit.Tactics)
            {
                var action = _data.Actions.GetValueOrDefault(usage.Tactic.ActionId);
                var effect = action?.Applies.FirstOrDefault() is { } applied ? _data.Effects.GetValueOrDefault(applied.EffectId)?.Name : null;
                var label = $"{TacticText.Sentence(usage.Tactic, effect)} → {action?.Name ?? usage.Tactic.ActionId}";
                lines.Add(new TacticLine(usage.Tactic.Priority, TextLayout.Truncate(font, label, labelWidth), usage.Used, usage.BlockedTotal));
            }

            if (unit.LostTurns.Count > 0)
            {
                Text(LostTurns(unit), Theme.Enemy);
            }

            lines.Add(new GapLine(UnitGap));
        }

        lines.Add(new SectionLine(_enemyTitle, Theme.Enemy));
        foreach (var unit in _summaries.Where(s => s.Side == CombatSide.Enemy))
        {
            lines.Add(Heading(unit, Theme.Enemy));
            Text(Stats(unit), Theme.Text);
            lines.Add(new GapLine(UnitGap));
        }

        _lines = lines;
        _linesWidth = areaWidth;
        return lines;
    }

    private HeadingLine Heading(UnitSummary unit, Color color)
    {
        var row = _combatants[unit.Id].StartRow == Row.Front ? "전위" : "후위";
        var (status, statusColor) = unit.DownedAt is { } turn
            ? ($"{turn}번째 차례에 쓰러짐", Theme.Death)
            : ("생존", Theme.TextDim);
        return new HeadingLine(_names[unit.Id], row, color, status, statusColor);
    }

    /// <summary>"행동 12 · 준 피해 1170 (지속 230) · 회복 96 · 받은 피해 610". 0인 회복·보호막·MP·소환수·면역·봉쇄 항목은 뺀다.</summary>
    private static string Stats(UnitSummary s)
    {
        var parts = new List<string>
        {
            $"행동 {s.Actions}",
            s.DotDamage > 0 ? $"준 피해 {s.DamageDealt} (지속 {s.DotDamage})" : $"준 피해 {s.DamageDealt}",
        };
        if (s.Healing > 0)
        {
            parts.Add($"회복 {s.Healing}");
        }

        if (s.Shielding > 0)
        {
            parts.Add($"보호막 {s.Shielding}");
        }

        if (s.MpRestored > 0)
        {
            parts.Add($"MP 회복 {s.MpRestored}");
        }

        parts.Add($"받은 피해 {s.DamageTaken}");
        if (s.SummonDamage > 0)
        {
            parts.Add($"소환수 피해 {s.SummonDamage}");
        }

        if (s.SummonHealing > 0)
        {
            parts.Add($"소환수 회복 {s.SummonHealing}");
        }

        if (s.Nullified > 0)
        {
            parts.Add($"면역에 막힘 {s.Nullified}");
        }

        if (s.HealsBlocked > 0)
        {
            parts.Add($"봉쇄된 회복 {s.HealsBlocked}");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>"잃은 차례 3: 자원 부족 3 (12·14·15번째)". 이유별로 묶고, 그 유닛의 몇 번째 차례였는지 붙인다.</summary>
    private static string LostTurns(UnitSummary s)
    {
        var groups = s.LostTurns
            .GroupBy(l => l.Reason)
            .Select(g => $"{ReasonLabel(g.Key)} {g.Count()} ({string.Join("·", g.Select(l => l.Turn))}번째)");
        return $"잃은 차례 {s.LostTurns.Count}: {string.Join(", ", groups)}";
    }

    private static string ReasonLabel(WaitReason reason) => reason switch
    {
        WaitReason.NotEnoughResource => "자원 부족",
        WaitReason.NoTarget => "대상 없음",
        WaitReason.Stunned => "기절",
        _ => "맞는 전술 없음",
    };

}
