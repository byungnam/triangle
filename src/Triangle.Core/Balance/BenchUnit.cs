using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Skills;

namespace Triangle.Core.Balance;

/// <summary>
/// 전술 측정(<see cref="TacticBench"/>)의 기준 파티 멤버 한 명. 게임은 로그라이트 재편으로 숙련과 착용 조건을 없앴지만(2026-10-19),
/// 측정은 그 전 규칙으로 잰 적 설계를 이어 쓰므로 옛 성장 규칙을 여기에만 남긴다.
/// <list type="bullet">
/// <item>계열 숙련 레벨만큼 트리 포인트가 있고, 패시브는 랭크만큼 포인트를 써서 배운다(선행 패시브가 있어야 한다).</item>
/// <item>요구 패시브가 모자란 장비와, 두손 무기와 함께 낀 보조 장비는 끼지 못한 것으로 본다.</item>
/// <item>쓸 수 있는 행동은 낀 장비가 주는 행동과 공용 행동 중 요구 패시브를 채운 것이다.</item>
/// </list>
/// </summary>
public sealed class BenchUnit
{
    private readonly GameData _data;
    private readonly Dictionary<EquipmentSlot, string> _gear;
    private readonly Dictionary<string, int> _masteries;
    private readonly Dictionary<string, int> _skills;

    public BenchUnit(GameData data, BenchMember definition, IReadOnlyDictionary<EquipmentSlot, string> gear)
    {
        _data = data;
        Definition = definition;
        _gear = gear.ToDictionary();
        _masteries = definition.Masteries.ToDictionary();
        _skills = definition.Skills.Where(p => p.Value > 0).ToDictionary();
    }

    public BenchMember Definition { get; }

    public string Id => Definition.Id;

    public IReadOnlyDictionary<string, int> SkillLevels => _skills;

    public int MasteryLevel(string masteryId) => _masteries.GetValueOrDefault(masteryId);

    private SkillSet Skills => new(_skills, _data.Skills);

    /// <summary>실제로 낀 장비 (끼지 못한 것 빼고, 부위 순서).</summary>
    public IEnumerable<(EquipmentSlot Slot, ItemDefinition Item)> Worn =>
        EquipmentSlots.All.Where(s => _gear.ContainsKey(s) && CanWear(s)).Select(s => (s, _data.Items[_gear[s]]));

    private bool CanWear(EquipmentSlot slot)
    {
        var item = _data.Items[_gear[slot]];
        if (slot == EquipmentSlot.OffHand && _gear.TryGetValue(EquipmentSlot.MainHand, out var main) && _data.Items[main].TwoHanded)
        {
            return false;
        }

        return Skills.Missing(item.Requirements).Count == 0;
    }

    /// <summary>쓸 수 있는 행동 ID.</summary>
    public IReadOnlySet<string> UsableActions()
    {
        var granted = new Loadout(Worn).GrantedActions(_data.Actions.Values);
        var skills = Skills;
        return _data.Actions.Values.Where(a => a.IsUsableBy(granted, skills)).Select(a => a.Id).ToHashSet();
    }

    /// <summary>전투 입력 (전술은 비어 있다).</summary>
    public CombatantSetup Setup() => new Loadout(Worn).Apply(
        new CombatantSetup(Id, Definition.Name, Definition.Stats, Definition.Row, null, null, new Dictionary<string, int>(_skills), []),
        _data.Actions.Values,
        MasteryLevel);

    /// <summary>
    /// 성장 상태로 만든다: 낀 장비의 계열 숙련을 <paramref name="masteryLevel"/>까지 올리고(이미 높으면 그대로), 착용 조건 패시브를 배운 뒤
    /// 남은 포인트도 그 트리에 쓰고, 같은 계열·부위·종류의 <paramref name="tier"/> 티어 장비로 바꿔 낀다(그 티어가 없으면 그대로).
    /// </summary>
    public void Grow(int tier, int masteryLevel)
    {
        var targets = Worn
            .Select(w => _data.Items.Values.FirstOrDefault(i => i.Slot == w.Item.Slot && i.Mastery == w.Item.Mastery && i.Type == w.Item.Type && i.Tier == tier) ?? w.Item)
            .ToList();

        foreach (var mastery in targets.Select(i => i.Mastery).OfType<string>().Distinct())
        {
            _masteries[mastery] = Math.Max(MasteryLevel(mastery), masteryLevel);
        }

        foreach (var requirement in targets.SelectMany(i => i.Requirements))
        {
            LearnUpTo(requirement);
        }

        foreach (var mastery in targets.Select(i => i.Mastery).Distinct())
        {
            var tree = _data.Skills.Values.Where(s => s.Mastery == mastery).OrderBy(s => s.Rank).ToList();
            while (tree.FirstOrDefault(s => CanLearn(s.Id)) is { } skill)
            {
                _skills[skill.Id] = _skills.GetValueOrDefault(skill.Id) + 1;
            }
        }

        foreach (var item in targets.Where(i => _gear.GetValueOrDefault(i.Slot) != i.Id))
        {
            if (Skills.Missing(item.Requirements).Count > 0)
            {
                throw new InvalidOperationException($"{Definition.Name} cannot wear {item.Id}");
            }

            _gear[item.Slot] = item.Id;
            if (item.TwoHanded)
            {
                _gear.Remove(EquipmentSlot.OffHand);
            }
        }
    }

    private void LearnUpTo(SkillRequirement requirement)
    {
        foreach (var prerequisite in _data.Skills[requirement.SkillId].Prerequisites)
        {
            LearnUpTo(prerequisite);
        }

        while (_skills.GetValueOrDefault(requirement.SkillId) < requirement.Level && CanLearn(requirement.SkillId))
        {
            _skills[requirement.SkillId] = _skills.GetValueOrDefault(requirement.SkillId) + 1;
        }
    }

    /// <summary>한 레벨 더 배울 수 있다: 최고 레벨 아래, 선행 패시브를 채웠고, 그 트리에 랭크만큼 포인트가 남았다.</summary>
    private bool CanLearn(string skillId)
    {
        var skill = _data.Skills[skillId];
        if (_skills.GetValueOrDefault(skillId) >= SkillDefinition.MaxLevel || Skills.Missing(skill.Prerequisites).Count > 0)
        {
            return false;
        }

        var spent = _skills.Where(p => _data.Skills.TryGetValue(p.Key, out var s) && s.Mastery == skill.Mastery).Sum(p => _data.Skills[p.Key].Rank * p.Value);
        return MasteryLevel(skill.Mastery) - spent >= skill.Rank;
    }
}
