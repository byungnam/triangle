namespace Triangle.Core.Data;

/// <summary>ID 하나를 가리키는 곳을 모두 찾는다 (편집기의 "쓰는 곳"). 종류를 가리지 않고 같은 문자열을 찾는다.</summary>
public static class DataReferences
{
    public static IReadOnlyList<string> Find(GameData data, string id)
    {
        var found = new List<string>();

        void Add(bool hit, string where)
        {
            if (hit)
            {
                found.Add(where);
            }
        }

        foreach (var s in data.Skills.Values)
        {
            var at = $"{GameDataLoader.SkillsFile} '{s.Id}'";
            Add(s.Mastery == id, $"{at}: mastery");
            Add(s.Prerequisites.Any(p => p.SkillId == id), $"{at}: prerequisites");
            Add(s.Bonuses.Any(b => b.Tag == id), $"{at}: bonuses tag");
        }

        foreach (var a in data.Actions.Values)
        {
            var at = $"{GameDataLoader.ActionsFile} '{a.Id}'";
            Add(a.Weapon == id, $"{at}: weapon");
            Add(a.Tags.Contains(id), $"{at}: tags");
            Add(a.Requirements.Any(r => r.SkillId == id), $"{at}: requirements");
            Add(a.Applies.Any(x => x.EffectId == id), $"{at}: applies");
            Add(a.BonusAgainst?.EffectId == id, $"{at}: bonusAgainst");
            Add(a.Summon?.ActionId == id, $"{at}: summon actionId");
            Add(a.Summon?.StatSkill == id, $"{at}: summon statSkill");
        }

        foreach (var i in data.Items.Values)
        {
            var at = $"{GameDataLoader.ItemsFile} '{i.Id}'";
            Add(i.Mastery == id, $"{at}: mastery");
            Add(i.Requirements.Any(r => r.SkillId == id), $"{at}: requirements");
            Add(i.Actions.Contains(id), $"{at}: actions");
            Add(i.Bonuses.Any(b => b.Tag == id), $"{at}: bonuses tag");
        }

        foreach (var e in data.Encounters.Values)
        {
            foreach (var u in e.Units)
            {
                var at = $"{GameDataLoader.EncountersFile} '{e.Id}' unit '{u.Id}'";
                Add(u.Weapon == id || u.Armor == id, $"{at}: equipment");
                Add(u.Skills.ContainsKey(id), $"{at}: skills");
                Add(u.Tactics.Any(t => t.ActionId == id), $"{at}: tactics");
            }
        }

        foreach (var z in data.Zones.Values)
        {
            var at = $"{GameDataLoader.ZonesFile} '{z.Id}'";
            Add(z.Encounters.Any(e => e.EncounterId == id), $"{at}: encounters");
            Add(z.Rewards.ItemDrops.Any(d => d.ItemId == id), $"{at}: itemDrops");
        }

        foreach (var r in data.Recipes.Values)
        {
            var at = $"{GameDataLoader.RecipesFile} '{r.Result}'";
            Add(r.Result == id, $"{at}: result");
            Add(r.Materials.Any(m => m.ItemId == id), $"{at}: materials");
        }

        foreach (var r in data.Recruits.Values)
        {
            var at = $"{GameDataLoader.RecruitsFile} '{r.Id}'";
            Add(r.Equipment.Values.Contains(id), $"{at}: equipment");
            Add(r.Tactics.Any(t => t.ActionId == id), $"{at}: tactics");
        }

        return found;
    }
}
