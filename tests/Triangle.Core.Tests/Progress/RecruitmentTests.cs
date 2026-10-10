using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

public class RecruitmentTests
{
    private static readonly GameData Data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static Company NewCompany(int gold) =>
        new([new PartyMember("a", "A", new Stats(10, 10, 20, 10, 10), Row.Front, TestGear.Of(),
                new Dictionary<string, int>(), new Dictionary<string, int>(), [])],
            ["a"], gold, new Dictionary<string, int>(), activeTacticSet: 0, nextSeed: 9);

    [Fact]
    public void Offers_are_deterministic_and_within_template_ranges()
    {
        var offers = Recruitment.Roll(Data, seed: 5);

        Assert.Equal(Recruitment.OfferCount, offers.Count);
        Assert.Equal(offers, Recruitment.Roll(Data, seed: 5));
        foreach (var offer in offers)
        {
            var t = Data.Recruits[offer.TemplateId];
            Assert.Contains(offer.Name, t.Names);
            Assert.Equal(t.Price, offer.Price);
            Assert.InRange(offer.Stats.Vital, t.StatsMin.Vital, t.StatsMax.Vital);
            Assert.InRange(offer.Stats.Intel, t.StatsMin.Intel, t.StatsMax.Intel);
        }
    }

    [Fact]
    public void Hire_pays_gold_and_adds_a_fresh_member_with_new_gear()
    {
        var company = NewCompany(gold: 1000);
        company.RerollRecruits(Data);
        var offer = company.RecruitOffers[1];
        var template = Data.Recruits[offer.TemplateId];

        company.AddTacticSet();
        company.AddTacticSet();
        var hired = company.Hire(1, Data);

        Assert.NotNull(hired);
        Assert.Equal(1000 - offer.Price, company.Gold);
        Assert.Equal(2, company.RecruitOffers.Count);
        Assert.Equal((offer.Name, offer.Stats, template.Row), (hired.Name, hired.Stats, template.Row));
        Assert.Equal(template.Equipment, hired.Equipment);
        Assert.Empty(company.Stash); // 창고에서 빼지 않는다
        Assert.Empty(hired.MasteryXp);
        Assert.Empty(hired.SkillLevels);
        Assert.Equal(3, hired.TacticSets.Count); // 모든 세트를 템플릿 전술로 시작한다
        Assert.All(hired.TacticSets, set => Assert.Equal(template.Tactics, set));
        Assert.Empty(hired.LockedTacticIndexes(Data, 0));
        Assert.Equal(["a", hired.Id], company.Lineup);
        Assert.Equal("recruit_1", hired.Id);
    }

    [Fact]
    public void Hire_is_refused_without_enough_gold()
    {
        var company = NewCompany(gold: 0);
        company.RerollRecruits(Data);

        Assert.False(company.CanHire(0));
        Assert.Null(company.Hire(0, Data));
        Assert.Equal(Recruitment.OfferCount, company.RecruitOffers.Count);
        Assert.Single(company.Roster);
    }

    [Fact]
    public void Offers_are_rerolled_when_an_expedition_ends()
    {
        var company = NewCompany(gold: 0);
        company.RerollRecruits(Data);
        var before = company.RecruitOffers.ToList();

        // 시작 회사의 전술을 쓰지 않는 멤버도 출정할 수 있다 (전술이 없으면 기다리기만 한다).
        ExpeditionRules.Start(company, Data, Data.Zones.Keys.First());
        ExpeditionRules.Return(company, Data);

        Assert.Equal(Recruitment.OfferCount, company.RecruitOffers.Count);
        Assert.NotEqual(before, company.RecruitOffers);
    }

    [Fact]
    public void Shipped_recruit_tactics_need_no_skills()
    {
        foreach (var template in Data.Recruits.Values)
        {
            var member = new PartyMember("r", "r", template.StatsMin, template.Row, template.Equipment,
                new Dictionary<string, int>(), new Dictionary<string, int>(), [template.Tactics]);
            Assert.Empty(member.LockedTacticIndexes(Data, 0));
            Assert.Contains(template.Tactics, t => t.Condition == Condition.Always);
        }
    }
}
