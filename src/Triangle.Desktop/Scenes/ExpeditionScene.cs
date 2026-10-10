using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;
using Triangle.Core.Combat;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Triangle.Desktop.Rendering;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 원정 중의 화면: 지역, 전투 n/최대, 출전 멤버의 HP·MP와 쓰러짐, 들고 있는 전리품, 직전 전투 요약.
/// 다음 전투로만 이어진다: 중간에 돌아오거나 전술을 바꿀 수 없다(출정 전에 짠다).
/// 원정 중에는 수동 저장이 없고 전투마다 자동 저장한다.
/// </summary>
internal sealed class ExpeditionScene : IScene
{
    private const int Margin = 24;
    private const int HeaderHeight = 64;
    private const int FooterHeight = 40;
    private const int BarHeight = 56;
    private const int MembersWidth = 400;

    private readonly Ui _ui;
    private readonly Widgets _w;
    private readonly GameSession _session;
    private readonly Rectangle _bounds;
    private readonly Action _nextBattle;
    private readonly MyraDesktop _desktop = new();
    private bool _dirty = true;

    public ExpeditionScene(Ui ui, GameSession session, Rectangle bounds, Action nextBattle)
    {
        _ui = ui;
        _w = new Widgets(ui);
        _session = session;
        _bounds = bounds;
        _nextBattle = nextBattle;
    }

    private Company Company => _session.Company;

    /// <summary>진행 중인 원정. 이 화면은 원정 중에만 보인다.</summary>
    private Expedition Expedition => Company.Expedition!;

    private ZoneDefinition Zone => _session.Data.Zones[Expedition.ZoneId];

    public void Refresh() => _dirty = true;

    public void Update(GameTime gameTime, Input input)
    {
        if (_dirty && Company.OnExpedition)
        {
            Rebuild();
            _dirty = false;
        }
    }

    public void Draw(SpriteBatch batch)
    {
        if (!Company.OnExpedition)
        {
            return;
        }

        if (_dirty)
        {
            Rebuild();
            _dirty = false;
        }

        batch.Begin();
        DrawHeader(batch);

        var top = _bounds.Top + HeaderHeight;
        var bottom = _bounds.Bottom - FooterHeight - BarHeight;
        var members = new Rectangle(_bounds.Left + Margin, top, MembersWidth, bottom - top);
        var report = new Rectangle(members.Right + Margin, top, _bounds.Right - Margin - members.Right - Margin, bottom - top);
        DrawMembers(batch, members);
        DrawReport(batch, report);

        const string help = "원정 중에는 전투가 끝날 때마다 자동 저장합니다. 창을 닫으면 저장하고 종료합니다.";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);
        if (_session.Notice is { } notice)
        {
            var font = _ui.Font(16);
            _ui.Text(batch, font, notice.Text, new Vector2(_bounds.Right - Margin - font.MeasureString(notice.Text).X, _bounds.Bottom - FooterHeight + 10), notice.Color);
        }

        batch.End();
        _desktop.Render();
    }

    private void DrawHeader(SpriteBatch batch)
    {
        _ui.Text(batch, _ui.BoldFont(28), $"원정 — {Zone.Name}", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);

        var font = _ui.BoldFont(22);
        var progress = $"전투 {Expedition.BattleIndex}/{Zone.MaxBattles}";
        var x = _bounds.Right - Margin - font.MeasureString(progress).X;
        _ui.Text(batch, font, progress, new Vector2(x, _bounds.Top + 24), Theme.Text);
    }

    private void DrawMembers(SpriteBatch batch, Rectangle area)
    {
        _ui.Panel(batch, area);
        var x = area.Left + 16;
        var y = area.Top + 14;
        _ui.Text(batch, _ui.BoldFont(22), "출전 멤버", new Vector2(x, y), Theme.Ally);
        y += 44;

        var rules = CombatRules.Default;
        foreach (var state in Expedition.Members)
        {
            var member = Company.Member(state.Id);
            var skills = member.CombatSkills(_session.Data);
            var (maxHp, maxMp) = (rules.MaxHp(member.Stats, skills), rules.MaxMp(member.Stats, skills));
            var name = state.Down ? $"{member.Name} (쓰러짐)" : member.Name;
            _ui.Text(batch, _ui.BoldFont(19), name, new Vector2(x, y), state.Down ? Theme.Death : Theme.Ally);
            var row = member.Row == Triangle.Core.Units.Row.Front ? "전위" : "후위";
            var rowFont = _ui.Font(15);
            _ui.Text(batch, rowFont, row, new Vector2(area.Right - 16 - rowFont.MeasureString(row).X, y + 3), Theme.TextDim);
            y += 28;

            var barWidth = area.Width - 32;
            var hpLow = state.Hp * 4 <= maxHp;
            _ui.Bar(batch, new Rectangle(x, y, barWidth, 10), state.Hp, maxHp, hpLow ? Theme.HpBarLow : Theme.HpBar);
            y += 14;
            _ui.Bar(batch, new Rectangle(x, y, barWidth, 6), state.Mp, maxMp, Theme.MpBar);
            y += 10;
            _ui.Text(batch, _ui.Font(15), $"HP {state.Hp}/{maxHp}   MP {state.Mp}/{maxMp}", new Vector2(x, y), Theme.TextDim);
            y += 34;
        }
    }

    private void DrawReport(SpriteBatch batch, Rectangle area)
    {
        _ui.Panel(batch, area);
        var data = _session.Data;
        var x = area.Left + 16;
        var y = area.Top + 14;

        _ui.Text(batch, _ui.BoldFont(22), "들고 있는 전리품", new Vector2(x, y), Theme.Cover);
        y += 40;
        _ui.Text(batch, _ui.Font(17), $"골드 {Expedition.CarriedGold}", new Vector2(x, y), Theme.Text);
        y += 28;
        var items = Expedition.CarriedItems.Count == 0 ? "아이템 없음" : ExpeditionText.Items(data, Expedition.CarriedItems);
        _ui.Text(batch, _ui.Font(17), items, new Vector2(x, y), Theme.Text);
        y += 28;
        _ui.Text(batch, _ui.Font(15), "원정이 끝나면 확정됩니다. 전멸해도 이미 얻은 것은 남습니다.", new Vector2(x, y), Theme.TextDim);
        y += 40;

        _ui.Fill(batch, new Rectangle(x, y, area.Width - 32, 1), Theme.PanelBorder);
        y += 16;

        if (Expedition.LastBattle is not { } last)
        {
            _ui.Text(batch, _ui.BoldFont(20), "아직 전투를 치르지 않았습니다", new Vector2(x, y), Theme.TextDim);
            return;
        }

        var (outcome, color) = ExpeditionText.Outcome(last.Outcome);
        _ui.Text(batch, _ui.BoldFont(20), $"직전 전투 {last.Number}: {outcome} — {data.Encounters[last.EncounterId].Name}", new Vector2(x, y), color);
        y += 34;
        foreach (var line in ExpeditionText.Battle(data, last))
        {
            _ui.Text(batch, _ui.Font(16), line.Text, new Vector2(x, y), line.Color);
            y += 24;
        }
    }

    private void Rebuild()
    {
        var bar = new HorizontalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var next = _w.TextButton("다음 전투  ▶", Theme.Accent, Theme.AccentHover, bold: true);
        next.Width = 180;
        next.Enabled = ExpeditionRules.CanContinue(Company, _session.Data);
        next.Click += (_, _) => _nextBattle();
        bar.Widgets.Add(next);

        bar.Left = _bounds.Left + Margin;
        bar.Top = _bounds.Bottom - FooterHeight - BarHeight + 8;
        var root = new Panel();
        root.Widgets.Add(bar);
        _desktop.Root = root;
    }
}
