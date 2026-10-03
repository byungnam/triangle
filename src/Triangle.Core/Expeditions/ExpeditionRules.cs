using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;

namespace Triangle.Core.Expeditions;

/// <summary>
/// 원정 규칙. 상태는 <see cref="Company.Expedition"/>에 있고, 여기 함수들이 바꾼다.
/// 흐름: <see cref="Start"/> → (<see cref="Fight"/> → <see cref="ApplyResult"/>) 반복 → 끝 (귀환, 클리어, 전멸, 무승부).
/// 같은 세이브에서 같은 선택을 하면 결과도 같다: 인카운터, 전투, 전리품, 사망 판정의 시드가
/// 모두 원정 시드와 전투 번호에서 나온다.
/// </summary>
public static class ExpeditionRules
{
    // 한 전투 안에서 시드를 나눠 쓰는 번호.
    private const int EncounterStream = 0;
    private const int CombatStream = 1;
    private const int LootStream = 2;
    private const int DeathStream = 3;

    /// <summary>출정할 수 없는 이유. null이면 출정할 수 있다.</summary>
    public static string? WhyCannotStart(Company company, GameData data, string zoneId)
    {
        if (company.OnExpedition)
        {
            return "이미 원정 중입니다";
        }

        if (!data.Zones.ContainsKey(zoneId))
        {
            return "없는 지역입니다";
        }

        if (company.Lineup.Count == 0)
        {
            return "출전 명단이 비어 있습니다";
        }

        return company.WhyLineupCannotFight(data);
    }

    /// <summary>출정한다. 출전 멤버는 HP·MP가 가득 찬 상태로 시작한다.</summary>
    public static Expedition Start(Company company, GameData data, string zoneId, CombatRules? rules = null)
    {
        if (WhyCannotStart(company, data, zoneId) is { } reason)
        {
            throw new InvalidOperationException($"Cannot start expedition: {reason}");
        }

        rules ??= CombatRules.Default;
        var members = company.LineupMembers.Select(m =>
        {
            var skills = m.CombatSkills(data);
            return new ExpeditionMember(m.Id, rules.MaxHp(m.Stats, skills), rules.MaxMp(m.Stats, skills), Down: false);
        });
        var expedition = new Expedition(zoneId, company.TakeSeed(), 0, members, 0, new Dictionary<string, int>(), null);
        company.Expedition = expedition;
        return expedition;
    }

    /// <summary>다음 전투에서 만날 인카운터. 지역의 가중치 목록에서 시드로 고른다 (결정적).</summary>
    public static string NextEncounter(Expedition expedition, GameData data)
    {
        var zone = data.Zones[expedition.ZoneId];
        var random = new Random(StreamSeed(expedition, EncounterStream));
        var roll = random.Next(zone.Encounters.Sum(e => e.Weight));
        foreach (var encounter in zone.Encounters)
        {
            roll -= encounter.Weight;
            if (roll < 0)
            {
                return encounter.EncounterId;
            }
        }

        throw new InvalidOperationException("Unreachable: weights are positive.");
    }

    /// <summary>다음 전투의 아군 입력: 쓰러지지 않은 출전 멤버, 시작 HP·MP는 이어진 값.</summary>
    public static IReadOnlyList<CombatantSetup> AllySetups(Company company, GameData data)
    {
        var expedition = Active(company);
        return expedition.Standing
            .Select(s => company.Member(s.Id).ToCombatantSetup(data, company.ActiveTacticSet) with { StartHp = s.Hp, StartMp = s.Mp })
            .ToList();
    }

    /// <summary>다음 전투를 계산한다. 상태는 바꾸지 않는다 (<see cref="ApplyResult"/>로 반영).</summary>
    public static CombatResult Fight(Company company, GameData data, CombatRules? rules = null)
    {
        var expedition = Active(company);
        return CombatSimulator.Run(
            AllySetups(company, data),
            data.CreateEncounterTeam(NextEncounter(expedition, data)),
            data.Catalog,
            StreamSeed(expedition, CombatStream),
            rules);
    }

    /// <summary>
    /// 전투 결과를 반영한다. 원정이 끝났으면 그 요약을, 계속할 수 있으면 null을 돌려준다.
    /// - HP·MP를 갱신하고 쓰러진 멤버를 표시한다. 숙련 경험치는 바로 준다.
    /// - 이기면 전리품(골드, 정해진 아이템, 티어 범위의 장비)을 굴려 들고 있는 전리품에 더한다. 마지막 전투면 클리어 보너스를 받고 끝난다.
    /// - 영구 사망 지역이면 쓰러진 멤버를 로스터에서 삭제한다. 장착 아이템(최대 5개)은 하나씩 확률로 파괴되고, 남은 것은 들고 간다.
    /// - 지면(전멸) 전리품을 잃고 끝난다. 무승부는 강제 귀환이다(전리품 확정).
    /// </summary>
    public static ExpeditionSummary? ApplyResult(Company company, GameData data, CombatResult result)
    {
        var expedition = Active(company);
        var zone = data.Zones[expedition.ZoneId];
        var encounterId = NextEncounter(expedition, data);

        var downed = new List<PartyMember>();
        foreach (var c in result.Combatants.Where(c => c.Side == CombatSide.Ally))
        {
            expedition.Update(new ExpeditionMember(c.Id, c.Hp, c.Mp, Down: !c.IsAlive));
            if (!c.IsAlive)
            {
                downed.Add(company.Member(c.Id));
            }
        }

        var xp = GrantMasteryXp(company, result);

        var gold = 0;
        var drops = new List<string>();
        if (result.Outcome == CombatOutcome.Victory)
        {
            var random = new Random(StreamSeed(expedition, LootStream));
            var rewards = zone.Rewards;
            gold = random.Next(rewards.GoldMin, rewards.GoldMax + 1) * (100 + rewards.DepthBonusPercent * expedition.BattleIndex) / 100;
            drops.AddRange(rewards.ItemDrops.Where(d => random.Next(100) < d.Chance).Select(d => d.ItemId));
            if (rewards.EquipmentDrop is { } tierDrop && random.Next(100) < tierDrop.Chance)
            {
                var pool = data.EquipmentInTiers(tierDrop.MinTier, tierDrop.MaxTier);
                if (pool.Count > 0)
                {
                    drops.Add(pool[random.Next(pool.Count)].Id);
                }
            }
        }

        var destroyed = new List<string>();
        var recovered = new List<string>();
        var deaths = new List<string>();
        if (zone.Permadeath)
        {
            var random = new Random(StreamSeed(expedition, DeathStream));
            foreach (var member in downed)
            {
                foreach (var item in EquipmentSlots.All.Select(member.ItemIn).OfType<string>())
                {
                    (random.Next(100) < zone.EquipmentDestroyChance ? destroyed : recovered).Add(item);
                }

                company.RemoveMember(member.Id);
                expedition.RecordDeath(member.Id, member.Name);
                deaths.Add(member.Name);
            }
        }

        var number = expedition.BattleIndex + 1;
        expedition.BattleIndex = number;

        ExpeditionEnd? end = result.Outcome switch
        {
            CombatOutcome.Defeat => ExpeditionEnd.Wiped,
            CombatOutcome.Draw => ExpeditionEnd.ForcedReturn,
            _ when number >= zone.MaxBattles => ExpeditionEnd.Cleared,
            _ => null,
        };
        if (end == ExpeditionEnd.Cleared)
        {
            gold += zone.Rewards.ClearBonusGold;
        }

        expedition.CarriedGold += gold;
        foreach (var item in drops.Concat(recovered))
        {
            expedition.Carry(item);
        }

        expedition.LastBattle = new BattleReport
        {
            Number = number,
            EncounterId = encounterId,
            Outcome = result.Outcome,
            Xp = xp,
            Gold = gold,
            Drops = drops,
            Downed = downed.Select(m => m.Name).ToList(),
            Deaths = deaths,
            Destroyed = destroyed,
            Recovered = recovered,
        };

        return end is { } e ? Finish(company, data, e) : null;
    }

    /// <summary>남은 전투가 있는가 (이겼고 마지막 전투가 아니면 계속할 수 있다).</summary>
    public static bool CanContinue(Company company, GameData data) =>
        company.Expedition is { } e && e.BattleIndex < data.Zones[e.ZoneId].MaxBattles && e.Standing.Any();

    /// <summary>귀환한다. 들고 있던 전리품이 확정된다.</summary>
    public static ExpeditionSummary Return(Company company, GameData data)
    {
        Active(company);
        return Finish(company, data, ExpeditionEnd.Returned);
    }

    /// <summary>
    /// 원정을 끝낸다: 전멸이 아니면 전리품을 창고와 골드로 확정한다. 초보 지역에서 쓰러진 멤버는
    /// 원정 상태와 함께 HP가 사라지므로 저절로 회복한다. 모집 후보를 새로 굴린다.
    /// </summary>
    private static ExpeditionSummary Finish(Company company, GameData data, ExpeditionEnd end)
    {
        var expedition = Active(company);
        var kept = end != ExpeditionEnd.Wiped;
        if (kept)
        {
            company.Gold += expedition.CarriedGold;
            foreach (var (item, count) in expedition.CarriedItems)
            {
                company.AddToStash(item, count);
            }
        }

        company.Expedition = null;
        company.RerollRecruits(data);
        return new ExpeditionSummary(
            expedition.ZoneId,
            end,
            expedition.BattleIndex,
            kept ? expedition.CarriedGold : 0,
            kept ? new Dictionary<string, int>(expedition.CarriedItems) : new Dictionary<string, int>(),
            expedition.Deaths.ToList());
    }

    /// <summary>주무기 계열과 입은 방어구 재질 숙련에 경험치를 더한다.</summary>
    private static List<XpReport> GrantMasteryXp(Company company, CombatResult result)
    {
        var reports = new List<XpReport>();
        foreach (var gain in MasteryGain.ForAllies(result))
        {
            var member = company.Member(gain.CombatantId);
            var gains = gain.Armor.Select(p => (Mastery: (string?)p.Key, Amount: p.Value)).Prepend((gain.Weapon, gain.WeaponXp));
            foreach (var (mastery, amount) in gains)
            {
                if (mastery is not null)
                {
                    reports.Add(new XpReport(member.Id, member.Name, mastery, amount, member.AddMasteryXp(mastery, amount)));
                }
            }
        }

        return reports;
    }

    private static Expedition Active(Company company) =>
        company.Expedition ?? throw new InvalidOperationException("No expedition in progress.");

    private static int StreamSeed(Expedition expedition, int stream) => Seeds.Derive(Seeds.Derive(expedition.Seed, expedition.BattleIndex), stream);
}
