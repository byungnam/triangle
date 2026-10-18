using System.Text.Json;
using Triangle.Core.Data;

namespace Triangle.Core.Balance;

/// <summary>
/// 드래프트 시뮬레이션(<see cref="DraftSimulation"/>)의 정의 파일(`tools/draft/draft.json`). 로그라이트 재편(설계, 구현 전)의
/// 임시 규칙을 지금 게임 데이터 위에 얹는다: 클래스, 시작 파티, 막, 보상 후보. 게임 데이터가 아니라 설계 도구의 입력이다.
/// </summary>
public sealed record DraftDefinition
{
    public string? Description { get; init; }

    public required IReadOnlyList<DraftClass> Classes { get; init; }

    /// <summary>시작 파티의 클래스 ID (같은 클래스를 여럿 둘 수 있다).</summary>
    public required IReadOnlyList<string> Party { get; init; }

    /// <summary>멤버마다 시작 줄 상한.</summary>
    public int StartLines { get; init; } = 2;

    /// <summary>줄 상한의 최대치 ("줄 상한 +1" 카드로 늘릴 수 있는 끝).</summary>
    public int MaxLines { get; init; } = 6;

    public required IReadOnlyList<DraftAct> Acts { get; init; }

    public DraftOffer Offer { get; init; } = new();

    /// <summary>막의 보스를 이긴 뒤 휴식에서 멤버마다 푸는 봉인 수 (지도의 휴식 지점을 대신한다).</summary>
    public int UnsealAfterAct { get; init; } = 1;

    /// <summary>전투 하나의 행동 상한 (null이면 게임 규칙 <see cref="Combat.CombatRules.MaxActions"/>). 상한에 걸리면 무승부이고 판의 패배다.</summary>
    public int? MaxActions { get; init; }

    public DraftClass Class(string id) => Classes.First(c => c.Id == id);

    /// <summary>정의를 읽고 게임 데이터와 맞는지 본다. 형식이나 참조가 틀리면 <see cref="GameDataException"/>을 던진다.</summary>
    public static DraftDefinition Load(string path, GameData data)
    {
        DraftDefinition? draft;
        try
        {
            draft = JsonSerializer.Deserialize<DraftDefinition>(File.ReadAllText(path), GameDataJson.Options);
        }
        catch (JsonException e)
        {
            throw new GameDataException([$"{Path.GetFileName(path)}: {e.Message}"]);
        }

        if (draft is null)
        {
            throw new GameDataException([$"{Path.GetFileName(path)}: empty"]);
        }

        var errors = draft.Validate(data);
        if (errors.Count > 0)
        {
            throw new GameDataException(errors.Select(e => $"{Path.GetFileName(path)}: {e}").ToList());
        }

        return draft;
    }

    public IReadOnlyList<string> Validate(GameData data)
    {
        var errors = new List<string>();
        errors.AddRange(Classes.GroupBy(c => c.Id).Where(g => g.Count() > 1).Select(g => $"classes: duplicate id '{g.Key}'"));
        foreach (var c in Classes)
        {
            var at = $"classes.{c.Id}";
            errors.AddRange(c.Families.Where(f => !data.Masteries.ContainsKey(f)).Select(f => $"{at}: unknown mastery '{f}'"));
            if (!DraftSimulation.ActivePool(data, c).Contains(c.StartingSkill))
            {
                errors.Add($"{at}: starting skill '{c.StartingSkill}' is not in the class pool");
            }

            foreach (var (slot, itemId) in c.Member.Equipment)
            {
                if (!data.Items.TryGetValue(itemId, out var item))
                {
                    errors.Add($"{at}: unknown item '{itemId}'");
                    continue;
                }

                if (item.Slot != slot)
                {
                    errors.Add($"{at}: '{itemId}' is not a {slot} item");
                }

                foreach (var tier in Acts.Select(a => a.GearTier).Distinct().Where(t => DraftSimulation.AtTier(data, item, t) is null))
                {
                    errors.Add($"{at}: no tier {tier} item like '{itemId}'");
                }
            }
        }

        var classIds = Classes.Select(c => c.Id).ToHashSet();
        if (Party.Count is 0 or > Progress.Company.MaxLineup)
        {
            errors.Add($"party: 1 to {Progress.Company.MaxLineup} members");
        }

        errors.AddRange(Party.Where(id => !classIds.Contains(id)).Select(id => $"party: unknown class '{id}'"));
        if (StartLines < 0 || StartLines > MaxLines)
        {
            errors.Add($"startLines: 0 to maxLines ({MaxLines})");
        }

        if (Acts.Count == 0)
        {
            errors.Add("acts: at least one");
        }

        foreach (var (act, i) in Acts.Select((a, i) => (a, i)))
        {
            if (!data.Zones.TryGetValue(act.ZoneId, out var zone))
            {
                errors.Add($"acts[{i}]: unknown zone '{act.ZoneId}'");
            }
            else if (zone.Boss is null)
            {
                errors.Add($"acts[{i}]: zone '{act.ZoneId}' has no boss");
            }

            if (act.Fights < 0)
            {
                errors.Add($"acts[{i}]: fights must not be negative");
            }
        }

        if (MaxActions is < 1)
        {
            errors.Add("maxActions: at least 1");
        }

        if (Offer.Cards < 1 || Offer.Active < 0 || Offer.Passive < 0 || Offer.Line < 0 || Offer.Active + Offer.Passive + Offer.Line == 0)
        {
            errors.Add("offer: at least one card and one positive weight");
        }

        return errors;
    }
}

/// <param name="Families">이 클래스가 받는 숙련 계열. 그 계열 아이템이 주는 행동이 액티브 카드, 그 계열 패시브가 패시브 카드가 된다.</param>
/// <param name="StartingSkill">시작할 때 가진 스킬 사본 1장.</param>
/// <param name="Member">스탯, 줄, 기본 장비. 숙련과 패시브는 쓰지 않는다(재편에서 숙련은 없어진다).</param>
public sealed record DraftClass(string Id, string Name, IReadOnlyList<string> Families, string StartingSkill, BenchMember Member);

/// <param name="Fights">보스 앞의 전투 수. 지역의 적 조합을 가중치로 뽑는다.</param>
/// <param name="GearTier">이 막에서 낄 장비 티어. 기본 장비와 같은 종류의 그 티어로 바꿔 낀다(아직 없는 상점을 대신한다).</param>
public sealed record DraftAct(string ZoneId, int Fights, int GearTier);

/// <summary>전투 보상: 후보 카드 수와 종류별 가중치.</summary>
public sealed record DraftOffer
{
    public int Cards { get; init; } = 3;

    public int Active { get; init; } = 6;

    public int Passive { get; init; } = 3;

    public int Line { get; init; } = 1;
}
