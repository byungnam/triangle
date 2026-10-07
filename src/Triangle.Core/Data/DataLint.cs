using Triangle.Core.Expeditions;
using Triangle.Core.Items;

namespace Triangle.Core.Data;

/// <summary>
/// 오류는 아니지만 데이터를 고칠 때 놓치기 쉬운 것 (아무도 못 쓰는 행동, 얻을 수 없는 아이템 등).
/// 로더 검증(<see cref="GameDataLoader"/>)을 통과한 데이터에만 돌린다. 게임은 경고를 보지 않는다.
/// </summary>
public static class DataLint
{
    public static IReadOnlyList<string> Check(GameData data)
    {
        var warnings = new List<string>();
        CheckActions(data, warnings);
        CheckEffects(data, warnings);
        CheckEncounters(data, warnings);
        CheckItems(data, warnings);
        return warnings;
    }

    /// <summary>공용도, 무기 행동도, 소환 행동도 아니고 주는 아이템도 없는 행동 (적도 아이템 행동만 쓴다).</summary>
    private static void CheckActions(GameData data, List<string> warnings)
    {
        var summonActions = data.Actions.Values
            .Select(a => a.Summon?.ActionId)
            .OfType<string>()
            .ToHashSet();
        var itemActions = data.Items.Values.SelectMany(i => i.Actions).ToHashSet();

        foreach (var action in data.Actions.Values)
        {
            var at = $"{GameDataLoader.ActionsFile} '{action.Id}'";
            if (action.SummonOnly)
            {
                if (!summonActions.Contains(action.Id))
                {
                    warnings.Add($"{at}: summonOnly but no summon uses it");
                }
            }
            else if (!action.Universal && action.Weapon is null && !itemActions.Contains(action.Id))
            {
                warnings.Add($"{at}: no item grants it");
            }
        }
    }

    private static void CheckEffects(GameData data, List<string> warnings)
    {
        var used = data.Actions.Values
            .SelectMany(a => a.Applies.Select(x => x.EffectId).Append(a.BonusAgainst?.EffectId))
            .OfType<string>()
            .ToHashSet();
        foreach (var effect in data.Effects.Values.Where(e => !used.Contains(e.Id)))
        {
            warnings.Add($"{GameDataLoader.EffectsFile} '{effect.Id}': no action applies it");
        }
    }

    private static void CheckEncounters(GameData data, List<string> warnings)
    {
        var used = data.Zones.Values.SelectMany(z => z.Encounters).Select(e => e.EncounterId).ToHashSet();
        foreach (var encounter in data.Encounters.Values.Where(e => !used.Contains(e.Id)))
        {
            warnings.Add($"{GameDataLoader.EncountersFile} '{encounter.Id}': no zone uses it");
        }
    }

    /// <summary>상점, 전리품, 제작, 신입 시작 장비 중 어디로도 얻을 수 없는 아이템. 재료는 제작에 안 쓰여도 경고.</summary>
    private static void CheckItems(GameData data, List<string> warnings)
    {
        var rewards = data.Zones.Values.Select(z => z.Rewards).ToList();
        var dropped = rewards.SelectMany(r => r.ItemDrops).Select(d => d.ItemId).ToHashSet();
        var tierDrops = rewards.Select(r => r.EquipmentDrop).OfType<TierDrop>().ToList();
        var recruitGear = data.Recruits.Values.SelectMany(r => r.Equipment.Values).ToHashSet();
        var materials = data.Recipes.Values.SelectMany(r => r.Materials).Select(m => m.ItemId).ToHashSet();

        foreach (var item in data.Items.Values)
        {
            var at = $"{GameDataLoader.ItemsFile} '{item.Id}'";
            var obtainable = dropped.Contains(item.Id)
                || (item.IsEquipment && (Shop.Sells(item)
                    || tierDrops.Any(d => item.Tier >= d.MinTier && item.Tier <= d.MaxTier)
                    || data.Recipes.ContainsKey(item.Id)
                    || recruitGear.Contains(item.Id)));
            if (!obtainable)
            {
                warnings.Add($"{at}: cannot be obtained (not sold, dropped, crafted or given to recruits)");
            }

            if (!item.IsEquipment && !materials.Contains(item.Id))
            {
                warnings.Add($"{at}: material used by no recipe");
            }
        }
    }
}
