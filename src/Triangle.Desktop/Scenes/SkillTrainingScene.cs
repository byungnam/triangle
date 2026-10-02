using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Triangle.Core.Data;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Desktop.Rendering;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 한 유닛의 스킬 목록과 훈련 큐. 스킬을 큐에 넣고, 순서를 바꾸고, 지운다.
/// SP는 전투 보상으로만 들어오고, 이 화면은 어디에 쌓을지만 정한다.
/// </summary>
internal sealed class SkillTrainingScene : IScene
{
    private const int Margin = 24;
    private const int HeaderHeight = 96;
    private const int FooterHeight = 40;
    private const int QueueWidth = 380;

    private readonly Ui _ui;
    private readonly Widgets _w;
    private readonly GameData _data;
    private readonly PartyMember _member;
    private readonly Rectangle _bounds;
    private readonly Action _back;
    private readonly Action _changed;
    private readonly MyraDesktop _desktop = new();

    private bool _dirty = true;
    private (string Text, Color Color)? _notice;

    /// <param name="changed">파티가 바뀌었을 때 (저장하지 않은 변경 표시용).</param>
    public SkillTrainingScene(Ui ui, GameData data, PartyMember member, Rectangle bounds, Action back, Action changed)
    {
        _ui = ui;
        _w = new Widgets(ui);
        _data = data;
        _member = member;
        _bounds = bounds;
        _back = back;
        _changed = changed;
    }

    public void Update(GameTime gameTime, Input input)
    {
        if (input.Pressed(Keys.Escape) || input.Pressed(Keys.Back))
        {
            _back();
            return;
        }

        if (_dirty)
        {
            Rebuild();
            _dirty = false;
        }
    }

    public void Draw(SpriteBatch batch)
    {
        if (_dirty)
        {
            Rebuild();
            _dirty = false;
        }

        batch.Begin();
        _ui.Text(batch, _ui.BoldFont(28), $"스킬 훈련 — {_member.Name}", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);

        var s = _member.Stats;
        _ui.Text(batch, _ui.Font(16), $"근력 {s.Str}   민첩 {s.Dex}   체력 {s.Vital}   지능 {s.Intel}   신속 {s.Speed}",
            new Vector2(_bounds.Left + Margin, _bounds.Top + 58), Theme.TextDim);

        var unallocated = $"미배정 SP {SkillText.Sp(_member.UnallocatedSp)}";
        var font = _ui.BoldFont(20);
        _ui.Text(batch, font, unallocated, new Vector2(_bounds.Right - Margin - font.MeasureString(unallocated).X, _bounds.Top + 24),
            _member.UnallocatedSp > 0 ? Theme.Cover : Theme.TextDim);

        const string help = "SP는 전투 보상으로 들어와 큐 맨 앞부터 쌓인다. 배율은 스킬의 1차·2차 스탯으로 정해진다.     Esc  전술 편집으로";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);
        if (_notice is { } notice)
        {
            var nf = _ui.Font(16);
            _ui.Text(batch, nf, notice.Text, new Vector2(_bounds.Right - Margin - nf.MeasureString(notice.Text).X, _bounds.Top + 60), notice.Color);
        }

        _ui.Panel(batch, SkillArea);
        _ui.Panel(batch, QueueArea);
        batch.End();

        _desktop.Render();
    }

    private Rectangle SkillArea => new(
        _bounds.Left + Margin, _bounds.Top + HeaderHeight,
        _bounds.Width - Margin * 3 - QueueWidth, _bounds.Height - HeaderHeight - FooterHeight - 8);

    private Rectangle QueueArea => new(
        _bounds.Right - Margin - QueueWidth, _bounds.Top + HeaderHeight,
        QueueWidth, _bounds.Height - HeaderHeight - FooterHeight - 8);

    private void Changed((string, Color)? notice = null)
    {
        _notice = notice;
        _changed();
        _dirty = true;
    }

    // ── 위젯 트리 ──────────────────────────────────────────

    private void Rebuild()
    {
        var root = new Panel();
        root.Widgets.Add(Place(BuildSkillList(), SkillArea));
        root.Widgets.Add(Place(BuildQueue(), QueueArea));
        _desktop.Root = root;
    }

    private static Widget Place(Widget content, Rectangle area)
    {
        var scroll = new ScrollViewer
        {
            Content = content,
            ShowHorizontalScrollBar = false,
            Left = area.X + 12,
            Top = area.Y + 10,
            Width = area.Width - 24,
            Height = area.Height - 20,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        return scroll;
    }

    private Widget BuildSkillList()
    {
        var list = new VerticalStackPanel { Spacing = 6 };
        var levels = _member.SkillLevels(_data);

        foreach (var group in _data.Skills.Values.GroupBy(s => s.Group))
        {
            list.Widgets.Add(_w.Label(group.Key.Length == 0 ? "기타" : group.Key, 18, Theme.Ally, bold: true));
            foreach (var skill in group)
            {
                list.Widgets.Add(BuildSkillRow(skill, levels.GetValueOrDefault(skill.Id)));
            }

            list.Widgets.Add(new Panel { Height = 6 });
        }

        return list;
    }

    private Widget BuildSkillRow(SkillDefinition skill, int level)
    {
        var row = new HorizontalStackPanel { Spacing = 10 };
        var sp = _member.SkillPoints.GetValueOrDefault(skill.Id);

        row.Widgets.Add(_w.Label(skill.Name, 16, level > 0 ? Theme.Text : Theme.TextDim, bold: true, width: 120));
        row.Widgets.Add(_w.Label(SkillText.LevelPips(level), 15, Theme.Cover, width: 70));
        row.Widgets.Add(_w.Label($"×{skill.Rank}", 15, Theme.TextDim, width: 28));

        var spText = level >= SkillDefinition.MaxLevel
            ? "최대"
            : $"{SkillText.Sp(sp)} / {SkillText.Sp(SkillProgression.SpForLevel(skill.Rank, level + 1))}";
        row.Widgets.Add(_w.Label(spText, 15, Theme.TextDim, width: 140));

        var multiplier = Training.MultiplierPercent(_member, skill);
        row.Widgets.Add(_w.Label(
            $"{SkillText.StatLabel(skill.Primary)}·{SkillText.StatLabel(skill.Secondary)} {multiplier}%", 15,
            multiplier >= 100 ? Theme.Heal : Theme.Damage, width: 110));

        row.Widgets.Add(_w.Label(string.Join(", ", skill.Bonuses.Select(SkillText.BonusLabel)), 15, Theme.Text, width: 170));

        var missing = Training.MissingPrerequisites(_member, skill.Id, _data);
        var next = Training.NextQueueLevel(_member, skill.Id, _data);
        if (missing.Count > 0)
        {
            var text = "선행: " + string.Join(", ", missing.Select(m => $"{_data.Skills[m.SkillId].Name} {m.Level}"));
            row.Widgets.Add(_w.Label(text, 14, Theme.Enemy));
        }
        else if (next is { } nextLevel)
        {
            var button = _w.TextButton($"+ Lv {nextLevel}", Theme.Button, Theme.ButtonHover, size: 15);
            button.Padding = new Thickness(8, 3);
            button.Click += (_, _) => Enqueue(skill);
            row.Widgets.Add(button);
        }

        return row;
    }

    private void Enqueue(SkillDefinition skill)
    {
        if (!Training.Enqueue(_member, skill.Id, _data, out var applied))
        {
            return;
        }

        (string, Color)? notice = null;
        if (applied is { } result)
        {
            var ups = result.LevelUps.Select(u => $"{_data.Skills[u.SkillId].Name} Lv {u.Level}").ToList();
            notice = ups.Count > 0
                ? ($"미배정 SP를 넣었습니다: {string.Join(", ", ups)}", Theme.Heal)
                : ($"미배정 SP {SkillText.Sp(result.Granted)}를 넣었습니다", Theme.Heal);
        }

        Changed(notice);
    }

    private Widget BuildQueue()
    {
        var list = new VerticalStackPanel { Spacing = 8 };
        list.Widgets.Add(_w.Label("훈련 큐", 18, Theme.Ally, bold: true));

        var queue = _member.TrainingQueue;
        if (queue.Count == 0)
        {
            list.Widgets.Add(_w.Label("비어 있음. 받은 SP는 미배정으로 남는다.", 15, Theme.TextDim));
            return list;
        }

        var totalRaw = 0L;
        var startSp = new Dictionary<string, int>(_member.SkillPoints);
        for (var i = 0; i < queue.Count; i++)
        {
            var entry = queue[i];
            var skill = _data.Skills[entry.SkillId];
            var from = Math.Max(startSp.GetValueOrDefault(entry.SkillId), SkillProgression.SpForLevel(skill.Rank, entry.Level - 1));
            var needed = Math.Max(0, SkillProgression.SpForLevel(skill.Rank, entry.Level) - from);
            var multiplier = Training.MultiplierPercent(_member, skill);
            var raw = ((long)needed * 100 + multiplier - 1) / multiplier;
            totalRaw += raw;
            startSp[entry.SkillId] = SkillProgression.SpForLevel(skill.Rank, entry.Level);

            list.Widgets.Add(BuildQueueRow(i, $"{skill.Name} → Lv {entry.Level}", $"남은 SP {SkillText.Sp(needed)} (보상 SP {SkillText.Sp((int)raw)})"));
        }

        var victories = (totalRaw + TrainingRules.Default.VictorySp - 1) / TrainingRules.Default.VictorySp;
        list.Widgets.Add(_w.Label($"모두 마치려면 보상 SP {SkillText.Sp((int)totalRaw)} (승리 약 {victories}회)", 15, Theme.Cover));
        return list;
    }

    private Widget BuildQueueRow(int index, string title, string detail)
    {
        var row = new HorizontalStackPanel { Spacing = 6 };
        var text = new VerticalStackPanel { Spacing = 2, Width = 220 };
        text.Widgets.Add(_w.Label($"{index + 1}. {title}", 16, index == 0 ? Theme.Text : Theme.TextDim, bold: index == 0));
        text.Widgets.Add(_w.Label(detail, 14, Theme.TextDim));
        row.Widgets.Add(text);

        row.Widgets.Add(QueueButton("▲", index > 0, () => Training.MoveInQueue(_member, index, -1, _data), "순서를 바꾸면 선행 조건이 깨집니다"));
        row.Widgets.Add(QueueButton("▼", index < _member.TrainingQueue.Count - 1, () => Training.MoveInQueue(_member, index, 1, _data), "순서를 바꾸면 선행 조건이 깨집니다"));

        var remove = _w.TextButton("삭제", Theme.Button, Theme.ButtonHover, size: 15);
        remove.Width = 52;
        remove.Padding = new Thickness(0, 4);
        remove.Click += (_, _) =>
        {
            var removed = Training.RemoveFromQueue(_member, index, _data);
            Changed(removed > 1 ? ($"함께 지운 항목 {removed - 1}개 (순서나 선행 조건이 깨져서)", Theme.Cover) : null);
        };
        row.Widgets.Add(remove);
        return row;
    }

    private Button QueueButton(string text, bool enabled, Func<bool> move, string failMessage)
    {
        var button = _w.TextButton(text, Theme.Button, Theme.ButtonHover, size: 15);
        button.Width = 32;
        button.Padding = new Thickness(0, 4);
        button.Enabled = enabled;
        button.Click += (_, _) =>
        {
            if (move())
            {
                Changed();
            }
            else
            {
                _notice = (failMessage, Theme.Enemy);
            }
        };
        return button;
    }
}
