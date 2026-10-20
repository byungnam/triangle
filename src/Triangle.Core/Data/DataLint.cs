using Triangle.Core.Runs;
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

    /// <summary>공용도, 무기 행동도, 소환 행동도 아니고 주는 아이템도, 클래스 카드도, 적의 행동 목록에도 없는 행동.</summary>
    private static void CheckActions(GameData data, List<string> warnings)
    {
        var summonActions = data.Actions.Values
            .Select(a => a.Summon?.ActionId)
            .OfType<string>()
            .ToHashSet();
        var itemActions = data.Items.Values.SelectMany(i => i.Actions)
            .Concat(data.Items.Values.Select(i => i.BasicAttack).OfType<string>())
            .Concat(data.Classes.Values.SelectMany(c => c.Skills))
            .Concat(data.Encounters.Values.SelectMany(e => e.Units).SelectMany(u => u.Actions ?? []))
            .ToHashSet();

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
            .Concat(data.Encounters.Values.SelectMany(e => e.Units).SelectMany(u => u.Effects))
            .Concat(data.Items.Values.SelectMany(i => i.Effects))
            .OfType<string>()
            .ToHashSet();
        foreach (var effect in data.Effects.Values.Where(e => !used.Contains(e.Id)))
        {
            warnings.Add($"{GameDataLoader.EffectsFile} '{effect.Id}': no action applies it");
        }
    }

    private static void CheckEncounters(GameData data, List<string> warnings)
    {
        var used = data.Zones.Values.SelectMany(z => z.Encounters).Select(e => e.EncounterId)
            .Concat(data.Zones.Values.Select(z => z.Boss?.EncounterId).OfType<string>())
            .Concat(data.Run?.Acts.SelectMany(a => a.Bosses.Concat(a.Elites)) ?? [])
            .ToHashSet();
        foreach (var encounter in data.Encounters.Values.Where(e => !used.Contains(e.Id)))
        {
            warnings.Add($"{GameDataLoader.EncountersFile} '{encounter.Id}': no zone or run act uses it");
        }
    }

    /// <summary>
    /// 판에서 판다고 적었는데(<see cref="ItemDefinition.Shop"/>) 어느 상점에도 나올 수 없는 아이템: 막의 상점 티어보다 높거나, 무기인데 그 계열을
    /// 쓰는 클래스가 없다. 팔지 않는 무기·방어구는 적, 클래스 기본 장비, 전술 측정의 성장 장비(같은 종류의 다음 티어)가 쓴다.
    /// </summary>
    private static void CheckItems(GameData data, List<string> warnings)
    {
        var tiers = data.Run?.Acts.Select(a => a.ShopTier).ToList() ?? [];
        var maxTier = tiers.Count == 0 ? 0 : tiers.Max();
        foreach (var item in data.Items.Values.Where(i => i.Shop))
        {
            if (item.Tier > maxTier)
            {
                warnings.Add($"{GameDataLoader.ItemsFile} '{item.Id}': sold in a run but no act shop sells tier {item.Tier}");
            }
            else if (item.Slot == EquipmentSlot.MainHand && !data.Classes.Values.Any(c => c.CanWield(item)))
            {
                warnings.Add($"{GameDataLoader.ItemsFile} '{item.Id}': sold in a run but no class wields '{item.Mastery}'");
            }
        }
    }
}
