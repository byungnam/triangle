using Microsoft.Xna.Framework;
using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Runs;
using Triangle.Core.Skills;

namespace Triangle.Desktop.Rendering;

/// <summary>한 판(로그라이트 재편) 화면의 글과 아이콘: 지점, 카드, 멤버, 클래스.</summary>
internal static class RunText
{
    public static (string Icon, Color Color, string Name) Node(RunNodeKind kind) => kind switch
    {
        RunNodeKind.Fight => ("node.fight", Theme.Text, "전투"),
        RunNodeKind.Elite => ("node.elite", Theme.Enemy, "정예"),
        RunNodeKind.Shop => ("node.shop", Theme.Cover, "상점"),
        RunNodeKind.Rest => ("node.rest", Theme.Heal, "휴식"),
        RunNodeKind.Event => ("node.event", Theme.Lightning, "이벤트"),
        _ => ("node.boss", Theme.Gold, "보스"),
    };

    /// <summary>지점 설명 (지도에서 마우스를 올렸을 때).</summary>
    public static string NodeHint(RunNodeKind kind, RunDefinition rules) => kind switch
    {
        RunNodeKind.Fight => rules.Reward.FightCards > 0
            ? $"보통 전투. 이기면 골드 {rules.Reward.GoldMin}~{rules.Reward.GoldMax}와 카드 {rules.Reward.FightCards}장 중 하나."
            : $"보통 전투. 이기면 골드 {rules.Reward.GoldMin}~{rules.Reward.GoldMax}. 카드는 없다.",
        RunNodeKind.Elite => $"강한 적. 이기면 골드 {rules.Reward.EliteGoldMin}~{rules.Reward.EliteGoldMax}와 카드 {rules.Reward.Cards + rules.Reward.EliteExtraCards}장 중 하나.",
        RunNodeKind.Shop => "아이템, 스킬 사본, 동료를 사고 봉인을 풀 수 있다.",
        RunNodeKind.Rest => $"모두의 봉인을 {rules.Rest.Unseal}개씩 풀거나, 패시브 {rules.Rest.TrainCards}장 중 하나를 고른다.",
        RunNodeKind.Event => "무슨 일이 일어날지 모른다. 맞바꿈이 있는 선택.",
        _ => $"막의 보스. 이기면 골드 {rules.Reward.BossGold}와 카드, 그리고 다음 막으로.",
    };

    /// <summary>클래스의 초상 (주무기 계열로 고른다).</summary>
    public static (string Icon, Color Color) Portrait(ClassDefinition c, GameData data) =>
        GameIcons.Portrait(data.Items.TryGetValue(c.Weapon, out var weapon) ? weapon.Mastery : null);

    public static string Row(Triangle.Core.Units.Row row) => row == Triangle.Core.Units.Row.Front ? "전위" : "후위";

    public static Color RowColor(Triangle.Core.Units.Row row) => row == Triangle.Core.Units.Row.Front ? Theme.FrontRow : Theme.BackRow;

    /// <summary>"줄 상한 2", 봉인되면 "줄 상한 1 (봉인 1)", 죽었으면 "죽음".</summary>
    public static (string Text, Color Color) Lines(RunMember member) =>
        member.Dead ? ("죽음", Theme.Death)
        : member.Sealed > 0 ? ($"줄 상한 {member.Lines} (봉인 {member.Sealed})", member.Lines == 0 ? Theme.Enemy : Theme.Cover)
        : ($"줄 상한 {member.Lines}", Theme.Text);

    /// <summary>
    /// 행동 설명 카드. 재편에서는 아이템이 행동을 주지 않고 패시브 요구도 없으므로, 출처를 "공용" 또는 "클래스 카드"로 바꾸고 요구를 뺀다.
    /// </summary>
    public static IReadOnlyList<TextLine> Action(ActionDefinition action, GameData data, ActionPreview? preview = null)
    {
        var lines = ActionText.Details(action with { Requirements = [] }, data, preview).ToList();
        var owner = data.Classes.Values.FirstOrDefault(c => c.Skills.Contains(action.Id));
        lines[1] = new TextLine(action.Universal ? "공용 행동 (카드 없이 누구나)" : owner is null ? "카드" : $"{owner.Name} 카드", Theme.TextDim, 14);
        return lines;
    }

    /// <summary>패시브 설명: 이름, 설명, 레벨당 보너스, 지금 레벨.</summary>
    public static IReadOnlyList<TextLine> Passive(SkillDefinition skill, GameData data, int level = 0)
    {
        var lines = new List<TextLine> { new(skill.Name, Theme.Text, 19, Bold: true) };
        lines.Add(new(level > 0 ? $"패시브 · 지금 Lv{level} {SkillText.LevelPips(level)}" : "패시브", Theme.TextDim, 14));
        if (skill.Description is { Length: > 0 } description)
        {
            lines.Add(new(description, Theme.Text));
        }

        foreach (var bonus in skill.Bonuses)
        {
            lines.Add(new($"레벨당 {SkillText.BonusLabel(bonus, data)}", Theme.Heal));
        }

        return lines;
    }

    /// <summary>아이템 설명: 판에서는 숙련도 요구도 주는 행동도 없다. 장신구는 전투 내내 걸린 효과를 보여준다.</summary>
    public static IReadOnlyList<TextLine> Item(ItemDefinition item, GameData data)
    {
        // 판에는 보조 칸이 없으므로 한손·두손 구분도 없다.
        var lines = ItemText.Details(item with { Requirements = [], TwoHanded = false }, data, withActions: false).ToList();
        lines[1] = item.Slot switch
        {
            EquipmentSlot.Trinket => new TextLine($"장신구 · 등급 {item.Tier}", Theme.TextDim, 14),
            EquipmentSlot.MainHand => lines[1] with { Text = lines[1].Text.Replace("주무기", "무기").Replace(" · 한손", "") },
            _ => lines[1],
        };

        foreach (var effect in item.Effects.Select(id => data.Effects[id]))
        {
            lines.Insert(lines.Count - (item.Price > 0 ? 1 : 0), new TextLine($"전투 내내 {effect.Name}: {ActionText.Effect(effect)}",
                effect.Kind == Triangle.Core.Effects.EffectKind.Debuff ? Theme.Enemy : Theme.Heal));
        }

        if (item.BasicAttack is { } basic)
        {
            lines.Insert(lines.Count - (item.Price > 0 ? 1 : 0), new TextLine($"기본 공격이 바뀐다: {BasicAttackText(data.Actions[basic], data)}", Theme.Damage));
        }

        return lines;
    }

    /// <summary>무기가 바꾸는 기본 공격 한 줄: "멀리 쏘기 (위력 85, 엄호 무시, 후열 우선, 느림)".</summary>
    public static string BasicAttackText(ActionDefinition action, GameData data)
    {
        var parts = new List<string>
        {
            action.Effect == ActionEffect.Heal ? $"치유 {action.Power}"
                : $"{(action.DamageType == DamageType.Magical ? "마법" : "물리")} 위력 {action.Power}{(action.Hits > 1 ? $" × {action.Hits}번" : "")}",
        };
        if (action.Scope == TargetScope.All)
        {
            parts.Add(action.Rows == RowRestriction.FrontOnly ? "전열 모두" : "적 모두");
        }

        if (action.ExtraTargets > 0)
        {
            parts.Add($"{action.ExtraTargets + 1}명에게 튐");
        }

        if (action.IgnoresCover)
        {
            parts.Add("엄호 무시");
        }

        if (action.Rule == TargetRule.BackFirst)
        {
            parts.Add("후열 우선");
        }

        parts.AddRange(action.Applies.Select(a => $"{data.Effects[a.EffectId].Name} {a.Duration}턴"));
        if (action.Delay is { } delay && delay > Triangle.Core.Combat.CombatRules.Default.TimeConstant)
        {
            parts.Add("느림");
        }

        return $"{action.Name} ({string.Join(", ", parts)})";
    }

    public static string CardKind(RunCardKind kind) => kind switch
    {
        RunCardKind.Active => "액티브",
        RunCardKind.Passive => "패시브",
        _ => "줄 상한",
    };

    public static Color CardColor(RunCardKind kind) => kind switch
    {
        RunCardKind.Active => Theme.Damage,
        RunCardKind.Passive => Theme.Heal,
        _ => Theme.Gold,
    };

    public static (string Icon, Color Color) CardIcon(RunCard card, GameData data) => card.Kind switch
    {
        RunCardKind.Active => GameIcons.Action(data.Actions[card.Id!], data),
        RunCardKind.Passive => ("mastery", Theme.Heal),
        _ => ("tactics", Theme.Gold),
    };

    public static string CardName(RunCard card, GameData data) => card.Kind switch
    {
        RunCardKind.Active => data.Actions[card.Id!].Name,
        RunCardKind.Passive => data.Skills[card.Id!].Name,
        _ => "줄 상한 +1",
    };

    /// <summary>카드 설명. 액티브는 파티 사본 수도 보여준다.</summary>
    public static IReadOnlyList<TextLine> Card(RunCard card, Run run, GameData data) => card.Kind switch
    {
        RunCardKind.Active =>
        [
            .. Action(data.Actions[card.Id!], data).Skip(1),
            new($"파티가 가진 사본 {run.Copies.GetValueOrDefault(card.Id!)}장 → 고르면 {run.Copies.GetValueOrDefault(card.Id!) + 1}장", Theme.Cover, 14),
        ],
        RunCardKind.Passive => Passive(data.Skills[card.Id!], data).Skip(1).ToList(),
        _ =>
        [
            new("멤버 한 명", Theme.TextDim, 14),
            new("그 멤버가 켤 수 있는 전술 줄이 하나 는다. 쓰러져 받은 봉인을 메우는 데도 쓴다.", Theme.Text),
            new($"최대 {data.Run!.MaxLines}줄", Theme.TextDim, 14),
        ],
    };

    /// <summary>상점의 스킬 강화: 이름이 어떻게 바뀌는지(화염구 → 화염구+), 바뀌는 수치나 방식, 어느 사본에 걸리는지.</summary>
    public static IReadOnlyList<TextLine> Upgrade(Run run, GameData data, RunStock stock)
    {
        var before = RunUpgrades.Action(data, stock.Id);
        var kind = stock.Upgrade!.Value;
        var after = RunUpgrades.Action(data, RunUpgrades.With(stock.Id, kind));
        var holder = run.UpgradeHolder(stock.Id);
        var which = holder is null ? "놀고 있는 사본" : $"{run.Name(data, holder)}의 줄에 놓인 사본";
        return
        [
            new($"{before.Name} → {after.Name}", Theme.Text, 18, Bold: true),
            new($"{RunUpgrades.Name(kind)}: {RunUpgrades.Describe(before, kind)}", Theme.Gold, 15),
            new($"{which} 한 장에만 걸린다 (이 사본 {run.Copies.GetValueOrDefault(stock.Id)}장 중)", Theme.Cover, 14),
        ];
    }

    public static (string Text, Color Color) Outcome(CombatOutcome outcome) => outcome switch
    {
        CombatOutcome.Victory => ("승리", Theme.Heal),
        CombatOutcome.Defeat => ("패배", Theme.Enemy),
        _ => ("무승부", Theme.TextDim),
    };

    /// <summary>전투 결과 줄: 결과, 골드, 봉인과 죽음.</summary>
    public static IReadOnlyList<LogLine> Battle(RunBattle battle, Run run, GameData data)
    {
        var lines = new List<LogLine>();
        var (outcome, color) = Outcome(battle.Result.Outcome);
        lines.Add(new($"{outcome} — {data.Encounters[battle.EncounterId].Name}", color));
        if (battle.Result.Outcome == CombatOutcome.Victory)
        {
            lines.Add(new($"골드 +{battle.Gold}", Theme.Cover, Indented: true));
        }
        else
        {
            lines.Add(new(battle.Result.Outcome == CombatOutcome.Draw ? "비기면 판이 끝납니다." : "판이 끝났습니다.", Theme.Enemy, Indented: true));
        }

        foreach (var id in battle.Sealed)
        {
            var member = run.Members.Single(m => m.Id == id);
            lines.Add(new($"{run.Name(data, member)}: 쓰러져 봉인 1 (줄 상한 {member.Lines})", Theme.Cover, Indented: true));
        }

        foreach (var id in battle.Died)
        {
            lines.Add(new($"{run.Name(data, run.Members.Single(m => m.Id == id))}: 줄 상한이 0인데 또 쓰러져 죽었습니다", Theme.Enemy, Indented: true));
        }

        return lines;
    }

    /// <summary>"1막 · 근교 숲".</summary>
    public static string Act(Run run, GameData data) => $"{run.Act + 1}막 · {data.Zones[data.Run!.Acts[run.Act].ZoneId].Name}";
}
