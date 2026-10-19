using System.Text.Json;
using Triangle.Core.Data;

namespace Triangle.Core.Balance;

/// <summary>
/// 드래프트 시뮬레이션(<see cref="DraftSimulation"/>)의 정의 파일(`tools/draft/draft.json`). 로그라이트 재편(설계, 구현 전)의
/// 임시 규칙을 지금 게임 데이터 위에 얹는다: 시작 파티, 막, 보상 후보. 클래스는 게임 데이터(`data/classes.json`)의 것을 쓴다.
/// 게임 데이터가 아니라 설계 도구의 입력이다.
/// </summary>
public sealed record DraftDefinition
{
    public string? Description { get; init; }

    /// <summary>시작 파티의 클래스 ID (<see cref="GameData.Classes"/>, 같은 클래스를 여럿 둘 수 있다).</summary>
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
        if (Party.Count is 0 or > Progress.Company.MaxLineup)
        {
            errors.Add($"party: 1 to {Progress.Company.MaxLineup} members");
        }

        errors.AddRange(Party.Where(id => !data.Classes.ContainsKey(id)).Select(id => $"party: unknown class '{id}'"));
        foreach (var c in Party.Distinct().Where(data.Classes.ContainsKey).Select(id => data.Classes[id]))
        {
            foreach (var itemId in new[] { c.Weapon, c.Armor })
            {
                foreach (var tier in Acts.Select(a => a.GearTier).Distinct().Where(t => DraftSimulation.AtTier(data, data.Items[itemId], t) is null))
                {
                    errors.Add($"party: no tier {tier} item like '{itemId}' (class '{c.Id}')");
                }
            }
        }
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

            errors.AddRange(act.Bosses.Where(id => !data.Encounters.ContainsKey(id)).Select(id => $"acts[{i}]: unknown boss '{id}'"));
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

/// <param name="Fights">보스 앞의 전투 수. 지역의 적 조합을 가중치로 뽑는다.</param>
/// <param name="GearTier">이 막에서 낄 장비 티어. 기본 장비와 같은 종류의 그 티어로 바꿔 낀다(아직 없는 상점을 대신한다).</param>
public sealed record DraftAct(string ZoneId, int Fights, int GearTier)
{
    /// <summary>막 보스 후보. 판을 시작할 때 하나를 뽑아 공개한다. 비어 있으면 그 지역의 보스다.</summary>
    public IReadOnlyList<string> Bosses { get; init; } = [];
}

/// <summary>전투 보상: 후보 카드 수와 종류별 가중치.</summary>
public sealed record DraftOffer
{
    public int Cards { get; init; } = 3;

    public int Active { get; init; } = 6;

    public int Passive { get; init; } = 3;

    public int Line { get; init; } = 1;
}
