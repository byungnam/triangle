using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Masteries;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>
/// 플레이어 파티의 유닛 한 명.
/// - 전열과 전술을 편집한다. 전술은 세트 두 벌(<see cref="TacticSetCount"/>)을 저장하고,
///   어느 세트로 싸울지는 회사가 정한다(<see cref="Company.ActiveTacticSet"/>).
/// - 무기 아이템 하나, 방어구 아이템 하나를 장착한다. 아이템의 계열이 전투와 숙련을 정한다.
///   장착과 해제는 회사 창고와 오간다(<see cref="Company.Equip"/>).
/// - 장착한 계열로 싸우면 그 숙련 경험치가 쌓이고, 숙련 레벨 1당 그 트리 포인트 1점이 생긴다.
///   포인트로 그 트리의 패시브 스킬을 배운다 (되돌릴 수 없다).
/// </summary>
public sealed class PartyMember
{
    public const int TacticSetCount = 2;

    private readonly TacticList[] _tacticSets;
    private readonly Dictionary<string, int> _masteryXp;
    private readonly Dictionary<string, int> _skillLevels;

    public PartyMember(
        string id,
        string name,
        Stats stats,
        Row row,
        string? weapon,
        string? armor,
        IReadOnlyDictionary<string, int> masteryXp,
        IReadOnlyDictionary<string, int> skillLevels,
        IReadOnlyList<IEnumerable<Tactic>> tacticSets)
    {
        Id = id;
        Name = name;
        Stats = stats;
        Row = row;
        Weapon = weapon;
        Armor = armor;
        _masteryXp = new Dictionary<string, int>(masteryXp);
        _skillLevels = new Dictionary<string, int>(skillLevels.Where(p => p.Value > 0));
        if (tacticSets.Count > TacticSetCount)
        {
            throw new ArgumentException($"At most {TacticSetCount} tactic sets.", nameof(tacticSets));
        }

        // 모자란 세트는 빈 목록으로 채운다.
        _tacticSets = Enumerable.Range(0, TacticSetCount)
            .Select(i => new TacticList(i < tacticSets.Count ? tacticSets[i] : []))
            .ToArray();
    }

    public string Id { get; }
    public string Name { get; }
    public Stats Stats { get; }
    public Row Row { get; set; }

    /// <summary>장착한 무기 아이템 ID. 계열이 바뀌면 그 무기가 필요한 전술이 잠길 수 있다.</summary>
    public string? Weapon { get; internal set; }

    /// <summary>장착한 방어구 아이템 ID.</summary>
    public string? Armor { get; internal set; }

    /// <summary>장착한 무기의 계열 ID (맨손이면 null).</summary>
    public string? WeaponMastery(GameData data) => data.MasteryOf(Weapon);

    /// <summary>장착한 방어구의 계열 ID (맨몸이면 null).</summary>
    public string? ArmorMastery(GameData data) => data.MasteryOf(Armor);

    /// <summary>전술 세트들 (항상 <see cref="TacticSetCount"/>벌).</summary>
    public IReadOnlyList<TacticList> TacticSets => _tacticSets;

    /// <summary>숙련 ID별 누적 경험치.</summary>
    public IReadOnlyDictionary<string, int> MasteryXp => _masteryXp;

    /// <summary>배운 스킬의 레벨 (1–5).</summary>
    public IReadOnlyDictionary<string, int> SkillLevels => _skillLevels;

    public SkillSet Skills(GameData data) => new(_skillLevels, data.Skills);

    public int MasteryLevel(string masteryId) => MasteryProgression.LevelFor(_masteryXp.GetValueOrDefault(masteryId));

    /// <summary>그 트리에 쓴 포인트 = Σ(랭크 × 레벨).</summary>
    public int PointsSpent(string masteryId, GameData data) =>
        _skillLevels
            .Where(p => data.Skills.TryGetValue(p.Key, out var s) && s.Mastery == masteryId)
            .Sum(p => data.Skills[p.Key].Rank * p.Value);

    public int PointsAvailable(string masteryId, GameData data) => MasteryLevel(masteryId) - PointsSpent(masteryId, data);

    /// <summary>
    /// 경험치를 더한다. 오른 숙련 레벨 수(= 새로 생긴 포인트)를 돌려준다.
    /// </summary>
    public int AddMasteryXp(string masteryId, int xp)
    {
        var before = MasteryLevel(masteryId);
        _masteryXp[masteryId] = _masteryXp.GetValueOrDefault(masteryId) + Math.Max(0, xp);
        return MasteryLevel(masteryId) - before;
    }

    /// <summary>스킬을 다음 레벨로 올릴 수 없는 이유. null이면 올릴 수 있다.</summary>
    public LearnBlocker? WhyCannotLearn(string skillId, GameData data)
    {
        var skill = data.Skills[skillId];
        var level = _skillLevels.GetValueOrDefault(skillId);
        if (level >= SkillDefinition.MaxLevel)
        {
            return new LearnBlocker(LearnBlockerKind.MaxLevel, []);
        }

        var missing = Skills(data).Missing(skill.Prerequisites);
        if (missing.Count > 0)
        {
            return new LearnBlocker(LearnBlockerKind.Prerequisites, missing);
        }

        return PointsAvailable(skill.Mastery, data) < skill.Rank ? new LearnBlocker(LearnBlockerKind.NotEnoughPoints, []) : null;
    }

    /// <summary>포인트를 써서 스킬을 한 레벨 올린다. 올릴 수 없으면 false. 되돌릴 수 없다.</summary>
    public bool Learn(string skillId, GameData data)
    {
        if (WhyCannotLearn(skillId, data) is not null)
        {
            return false;
        }

        _skillLevels[skillId] = _skillLevels.GetValueOrDefault(skillId) + 1;
        return true;
    }

    /// <summary>
    /// 지금 장비와 스킬로 쓸 수 없는 행동이 든 전술의 위치.
    /// 무기를 바꾸었거나 게임 데이터가 바뀌었을 때 생긴다.
    /// </summary>
    public IReadOnlyList<int> LockedTacticIndexes(GameData data, int tacticSet)
    {
        var skills = Skills(data);
        var weapon = WeaponMastery(data);
        return _tacticSets[tacticSet]
            .Select((t, i) => (t, i))
            .Where(x => !data.Actions.TryGetValue(x.t.ActionId, out var action) || !action.IsUsableBy(weapon, skills))
            .Select(x => x.i)
            .ToList();
    }

    public void ToggleRow() => Row = Row == Row.Front ? Row.Back : Row.Front;

    /// <summary>전투 입력으로 바꾼다. 장비는 아이템에서 계열로 바꿔 넘긴다.</summary>
    public CombatantSetup ToCombatantSetup(GameData data, int tacticSet) =>
        new(Id, Name, Stats, Row, WeaponMastery(data), ArmorMastery(data), new Dictionary<string, int>(_skillLevels), _tacticSets[tacticSet].ToList());
}
