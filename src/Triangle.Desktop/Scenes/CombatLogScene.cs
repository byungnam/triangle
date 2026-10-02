using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Effects;
using Triangle.Core.Units;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 끝까지 계산한 전투 결과를 받아, 이벤트를 한 줄씩 재생하며 텍스트 로그와 HP를 보여준다.
/// 결과는 이미 확정되어 있으므로(원정에 반영하고 저장했다) 다시 하기는 없다.
/// 로그는 두 칸이다. 행동한 쪽 칸(왼쪽 아군, 오른쪽 적)에 그 행동과 결과가 들어가고,
/// 위에서 아래로 시간 순서를 유지한다.
/// </summary>
internal sealed class CombatLogScene : IScene
{
    private const double SecondsPerLine = 0.35;

    private const int Margin = 24;
    private const int SidePanelWidth = 250;
    private const int HeaderHeight = 64;
    private const int FooterHeight = 40;
    private const int LogFontSize = 18;
    private const int LogLineHeight = 25;
    private const int LogHeaderHeight = 44;
    private const int LogPadding = 16;
    private const int LogIndent = 18;
    private const int BlockGap = 8;

    private readonly Ui _ui;
    private readonly GameData _data;
    private readonly string _encounterId;
    private readonly string _title;
    private readonly Rectangle _bounds;
    private readonly Action _back;
    private readonly string _backLabel;
    private IReadOnlyList<LogLine> _rewardLines = [];

    /// <summary>Side가 null이면 두 칸에 걸쳐 가운데에 그린다 (전투 시작/종료).</summary>
    private sealed record LogEntry(LogLine Line, CombatSide? Side, bool StartsBlock);

    private readonly List<LogEntry> _log = [];
    private readonly List<UnitView> _allies = [];
    private readonly List<UnitView> _enemies = [];
    private readonly Dictionary<string, UnitView> _units = [];

    private readonly CombatResult _result;
    private readonly CombatLogFormatter _formatter;
    private int _nextEvent;
    private double _timer;
    private CombatSide _actingSide;
    private bool _blockStarting;

    /// <param name="title">머리글 (예: "전투 2/3").</param>
    /// <param name="back">Esc/Backspace를 누르면 호출된다 (이전 화면으로).</param>
    /// <param name="backLabel">도움말에 보여줄 이전 화면 이름.</param>
    public CombatLogScene(
        Ui ui, GameData data, CombatResult result, string encounterId, string title, Rectangle bounds, Action back, string backLabel)
    {
        _ui = ui;
        _data = data;
        _result = result;
        _encounterId = encounterId;
        _title = title;
        _bounds = bounds;
        _back = back;
        _backLabel = backLabel;

        foreach (var c in _result.Combatants)
        {
            var view = new UnitView(c.Id, c.Side, c.Row, c.MaxHp, c.MaxMp) { Hp = c.StartHp, Mp = c.StartMp };
            (c.Side == CombatSide.Ally ? _allies : _enemies).Add(view);
            _units[c.Id] = view;
        }

        AssignDisplayNames(_result.Combatants);

        _formatter = new CombatLogFormatter(
            _units.ToDictionary(u => u.Key, u => u.Value.Name),
            _data.Actions.ToDictionary(a => a.Key, a => a.Value.Name),
            _data.Effects);

        _log.Add(new LogEntry(new LogLine($"{Korean.WaGwa(EncounterName)}의 전투 시작!", Theme.Text), null, true));
    }

    private bool Finished => _nextEvent >= _result.Events.Count;

    private string EncounterName => _data.Encounters[_encounterId].Name;

    public CombatResult Result => _result;

    /// <summary>로그 끝에 붙일 결과 줄 (경험치, 전리품, 사망).</summary>
    public void SetRewardLines(IReadOnlyList<LogLine> lines) => _rewardLines = lines;

    public void RevealAll() => RevealLines(int.MaxValue);

    public void RevealLines(int count)
    {
        for (var i = 0; i < count && !Finished; i++)
        {
            RevealNext();
        }
    }

    public void Update(GameTime gameTime, Input input)
    {
        if (input.Pressed(Keys.Escape) || input.Pressed(Keys.Back))
        {
            _back();
            return;
        }

        if (input.Pressed(Keys.Space))
        {
            RevealAll();
            return;
        }

        _timer += gameTime.ElapsedGameTime.TotalSeconds;
        while (_timer >= SecondsPerLine && !Finished)
        {
            _timer -= SecondsPerLine;
            RevealNext();
        }
    }

    /// <summary>줄이 하나 생길 때까지 이벤트를 적용한다 (턴 시작처럼 줄이 없는 이벤트는 바로 넘긴다).</summary>
    private void RevealNext()
    {
        while (!Finished)
        {
            var e = _result.Events[_nextEvent++];
            Apply(e);

            if (e is TurnStarted t)
            {
                _actingSide = _units[t.ActorId].Side;
                _blockStarting = true;
            }

            if (_formatter.Format(e) is { } line)
            {
                CombatSide? side = e is CombatEnded ? null : _actingSide;
                _log.Add(new LogEntry(line, side, _blockStarting || side is null));
                if (e is CombatEnded)
                {
                    AddRewardLines();
                }

                _blockStarting = false;
                return;
            }
        }
    }

    private void AddRewardLines()
    {
        for (var i = 0; i < _rewardLines.Count; i++)
        {
            _log.Add(new LogEntry(_rewardLines[i], null, i == 0));
        }
    }

    private void Apply(CombatEvent e)
    {
        switch (e)
        {
            case TurnStarted t:
                foreach (var u in _units.Values)
                {
                    u.Acting = u.Id == t.ActorId;
                }

                break;
            case ActionUsed a:
                _units[a.ActorId].Hp = a.ActorHp;
                _units[a.ActorId].Mp = a.ActorMp;
                break;
            case Damaged d:
                _units[d.TargetId].Hp = d.HpAfter;
                break;
            case Healed h:
                _units[h.TargetId].Hp = h.HpAfter;
                break;
            case MpRestored m:
                _units[m.TargetId].Mp = m.MpAfter;
                break;
            case EffectTicked t:
                _units[t.TargetId].Hp = t.HpAfter;
                break;
            case EffectApplied a when !_units[a.TargetId].Effects.Contains(a.EffectId):
                _units[a.TargetId].Effects.Add(a.EffectId);
                break;
            case EffectExpired x:
                _units[x.TargetId].Effects.Remove(x.EffectId);
                break;
            case Died d:
                _units[d.UnitId].Effects.Clear();
                break;
            case CombatEnded:
                foreach (var u in _units.Values)
                {
                    u.Acting = false;
                }

                break;
        }
    }

    /// <summary>같은 진영에 같은 이름이 있으면 "훈련병 A", "훈련병 B"처럼 구분한다.</summary>
    private void AssignDisplayNames(IEnumerable<Combatant> setups)
    {
        foreach (var group in setups.GroupBy(s => (_units[s.Id].Side, s.Name)))
        {
            var members = group.ToList();
            for (var i = 0; i < members.Count; i++)
            {
                _units[members[i].Id].Name = members.Count == 1 ? members[i].Name : $"{members[i].Name} {(char)('A' + i)}";
            }
        }
    }

    // ── 그리기 ─────────────────────────────────────────────

    public void Draw(SpriteBatch batch)
    {
        batch.Begin(samplerState: SamplerState.PointClamp);

        DrawHeader(batch);

        var top = _bounds.Top + HeaderHeight;
        var height = _bounds.Height - HeaderHeight - FooterHeight;
        var left = new Rectangle(_bounds.Left + Margin, top, SidePanelWidth, height);
        var right = new Rectangle(_bounds.Right - Margin - SidePanelWidth, top, SidePanelWidth, height);
        var center = new Rectangle(left.Right + Margin, top, right.Left - left.Right - Margin * 2, height);

        DrawTeam(batch, left, "아군", Theme.Ally, _allies);
        DrawTeam(batch, right, EncounterName, Theme.Enemy, _enemies);
        DrawLog(batch, center);
        DrawFooter(batch);

        batch.End();
    }

    private void DrawHeader(SpriteBatch batch)
    {
        var title = $"{_title} — {EncounterName}";
        _ui.Text(batch, _ui.BoldFont(28), title, new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);

        var status = Finished
            ? _result.Outcome switch
            {
                CombatOutcome.Victory => ("승리", Theme.Heal),
                CombatOutcome.Defeat => ("패배", Theme.Enemy),
                _ => ("무승부", Theme.TextDim),
            }
            : ("진행 중", Theme.TextDim);

        var font = _ui.BoldFont(22);
        var text = status.Item1;
        var size = font.MeasureString(text);
        _ui.Text(batch, font, text, new Vector2(_bounds.Right - Margin - size.X, _bounds.Top + 24), status.Item2);
    }

    private void DrawTeam(SpriteBatch batch, Rectangle area, string title, Color color, List<UnitView> units)
    {
        _ui.Panel(batch, area);
        var x = area.Left + 16;
        var y = area.Top + 14;
        _ui.Text(batch, _ui.BoldFont(22), title, new Vector2(x, y), color);
        y += 40;

        foreach (var row in new[] { Row.Front, Row.Back })
        {
            _ui.Text(batch, _ui.Font(16), row == Row.Front ? "전위" : "후위", new Vector2(x, y), Theme.TextDim);
            y += 24;

            foreach (var unit in units.Where(u => u.Row == row))
            {
                DrawUnit(batch, unit, new Rectangle(x, y, area.Width - 32, 64), color);
                y += 74;
            }

            y += 8;
        }
    }

    private void DrawUnit(SpriteBatch batch, UnitView unit, Rectangle area, Color color)
    {
        var alive = unit.Hp > 0;
        var nameColor = !alive ? Theme.Death : unit.Acting ? Theme.Cover : color;

        if (unit.Acting)
        {
            _ui.Fill(batch, area with { X = area.X - 8, Width = 3 }, Theme.Cover);
        }

        _ui.Text(batch, _ui.BoldFont(18), alive ? unit.Name : $"{unit.Name} (쓰러짐)", new Vector2(area.X, area.Y), nameColor);

        // 걸려 있는 효과는 이름 줄 오른쪽에 (버프는 노랑, 디버프는 빨강).
        var effectFont = _ui.Font(14);
        var x = (float)area.Right;
        foreach (var effectId in unit.Effects.AsEnumerable().Reverse())
        {
            var effect = _data.Effects[effectId];
            var size = effectFont.MeasureString(effect.Name);
            x -= size.X;
            _ui.Text(batch, effectFont, effect.Name, new Vector2(x, area.Y + 3), effect.Kind == EffectKind.Buff ? Theme.Cover : Theme.Enemy);
            x -= 8;
        }

        var hpLow = unit.Hp * 4 <= unit.MaxHp;
        var hpBar = new Rectangle(area.X, area.Y + 26, area.Width, 12);
        _ui.Bar(batch, hpBar, unit.Hp, unit.MaxHp, hpLow ? Theme.HpBarLow : Theme.HpBar);
        var mpBar = new Rectangle(area.X, area.Y + 42, area.Width, 6);
        _ui.Bar(batch, mpBar, unit.Mp, unit.MaxMp, Theme.MpBar);

        var font = _ui.Font(14);
        var hpText = $"HP {unit.Hp}/{unit.MaxHp}   MP {unit.Mp}/{unit.MaxMp}";
        _ui.Text(batch, font, hpText, new Vector2(area.X, area.Y + 50), Theme.TextDim);
    }

    private void DrawLog(SpriteBatch batch, Rectangle area)
    {
        _ui.Panel(batch, area);

        var mid = area.Center.X;
        var columnWidth = area.Width / 2 - LogPadding * 2;
        var leftX = area.Left + LogPadding;
        var rightX = mid + LogPadding;

        // 칸 제목과 구분선
        var headerFont = _ui.BoldFont(18);
        _ui.Text(batch, headerFont, "아군", new Vector2(leftX, area.Top + 12), Theme.Ally);
        _ui.Text(batch, headerFont, EncounterName, new Vector2(rightX, area.Top + 12), Theme.Enemy);
        var contentTop = area.Top + LogHeaderHeight;
        _ui.Fill(batch, new Rectangle(area.Left + 1, contentTop - 6, area.Width - 2, 1), Theme.PanelBorder);
        _ui.Fill(batch, new Rectangle(mid, area.Top + 8, 1, LogHeaderHeight - 14), Theme.PanelBorder);

        // 줄바꿈까지 마친 화면 줄을 만든 뒤, 최근 줄이 아래에 오도록 들어가는 만큼만 그린다.
        var font = _ui.Font(LogFontSize);
        var rows = new List<(string Text, Color Color, float X, int GapBefore, bool Spans)>();
        foreach (var entry in _log)
        {
            var indent = entry.Line.Indented ? LogIndent : 0;
            var width = entry.Side is null ? area.Width - LogPadding * 2 : columnWidth - indent;
            var wrapped = Wrap(font, entry.Line.Text, width);

            for (var i = 0; i < wrapped.Count; i++)
            {
                var x = entry.Side switch
                {
                    CombatSide.Ally => leftX + indent,
                    CombatSide.Enemy => rightX + indent,
                    _ => mid - font.MeasureString(wrapped[i]).X / 2,
                };
                var gap = i == 0 && entry.StartsBlock ? BlockGap : 0;
                rows.Add((wrapped[i], entry.Line.Color, x, gap, entry.Side is null));
            }
        }

        var available = area.Bottom - LogPadding - contentTop;
        var first = rows.Count;
        var used = 0;
        while (first > 0 && used + LogLineHeight + rows[first - 1].GapBefore <= available)
        {
            first--;
            used += LogLineHeight + rows[first].GapBefore;
        }

        // 구분선은 두 칸으로 나뉜 줄에만 그린다 (전투 시작/종료 줄은 가로지르지 않는다).
        var y = contentTop - 6;
        for (var i = first; i < rows.Count; i++)
        {
            var gap = i > first ? rows[i].GapBefore : 0;
            if (!rows[i].Spans)
            {
                _ui.Fill(batch, new Rectangle(mid, y, 1, gap + LogLineHeight), Theme.PanelBorder);
            }

            y += gap;
            _ui.Text(batch, font, rows[i].Text, new Vector2(rows[i].X, y + 6), rows[i].Color);
            y += LogLineHeight;
        }
    }

    /// <summary>폭을 넘으면 공백 단위로 줄을 바꾼다. 공백 없는 긴 단어는 그대로 둔다.</summary>
    private static List<string> Wrap(FontStashSharp.SpriteFontBase font, string text, float width)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' '))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && font.MeasureString(candidate).X > width)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        lines.Add(current);
        return lines;
    }

    private void DrawFooter(SpriteBatch batch)
    {
        var help = $"Space  끝까지 보기     Esc  {_backLabel}";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);
    }

    private sealed class UnitView(string id, CombatSide side, Row row, int maxHp, int maxMp)
    {
        public string Id { get; } = id;
        public CombatSide Side { get; } = side;
        public Row Row { get; } = row;
        public int MaxHp { get; } = maxHp;
        public int MaxMp { get; } = maxMp;
        public string Name { get; set; } = id;
        public int Hp { get; set; } = maxHp;
        public int Mp { get; set; } = maxMp;
        public bool Acting { get; set; }
        public List<string> Effects { get; } = [];
    }
}
