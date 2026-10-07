using Triangle.Core.Data;
using Triangle.Core.Items;

namespace Triangle.DataTool;

/// <summary><c>list</c> 명령: 종류별 한 줄 요약. 데이터 순서를 지킨다.</summary>
internal static class Listing
{
    public static List<string>? Rows(GameData data, string kind, Args options)
    {
        var search = options.Get("search");
        var mastery = options.Get("mastery");

        bool Matches(params string?[] fields) =>
            search is null || fields.Any(f => f?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);

        return kind switch
        {
            "masteries" => data.Masteries.Values
                .Where(m => Matches(m.Id, m.Name))
                .Select(m => Row(m.Id, m.Name, m.Kind.ToString()))
                .ToList(),

            "skills" => data.Skills.Values
                .Where(s => Matches(s.Id, s.Name) && (mastery is null || s.Mastery == mastery))
                .Select(s => Row(s.Id, s.Name, s.Mastery, $"rank {s.Rank}"))
                .ToList(),

            "actions" => data.Actions.Values
                .Where(a => Matches(a.Id, a.Name) && (mastery is null || a.Weapon == mastery
                    || data.ItemsGranting(a.Id).Any(i => i.Mastery == mastery)))
                .Select(a => Row(a.Id, a.Name, a.Effect.ToString(), $"power {a.Power}", $"mp {a.MpCost}",
                    a.Delay is { } d ? $"delay {d}" : "", Source(data, a.Id, a.Universal, a.Weapon, a.SummonOnly)))
                .ToList(),

            "effects" => data.Effects.Values
                .Where(e => Matches(e.Id, e.Name))
                .Select(e => Row(e.Id, e.Name, e.Kind.ToString()))
                .ToList(),

            "items" => Items(data, options, Matches),

            "encounters" => data.Encounters.Values
                .Where(e => Matches(e.Id, e.Name))
                .Select(e => Row(e.Id, e.Name, string.Join(", ", e.Units.Select(u => $"{u.Name}({(u.Equipment.TryGetValue(EquipmentSlot.MainHand, out var w) ? data.Items[w].TypeOrName : "맨손")})"))))
                .ToList(),

            "zones" => data.Zones.Values
                .Where(z => Matches(z.Id, z.Name))
                .Select(z => Row(z.Id, z.Name, $"난이도 {z.Difficulty}", $"전투 {z.MaxBattles}",
                    string.Join(", ", z.Encounters.Select(e => $"{e.EncounterId}×{e.Weight}"))))
                .ToList(),

            "recipes" => data.Recipes.Values
                .Where(r => Matches(r.Result, data.Items.GetValueOrDefault(r.Result)?.Name))
                .Select(r => Row(r.Result, $"{r.Gold}G", string.Join(", ", r.Materials.Select(m => $"{m.ItemId}×{m.Count}"))))
                .ToList(),

            "recruits" => data.Recruits.Values
                .Where(r => Matches(r.Id, r.Name))
                .Select(r => Row(r.Id, r.Name, $"{r.Price}G", r.Row.ToString(), string.Join(", ", r.Equipment.Values)))
                .ToList(),

            _ => null,
        };
    }

    private static List<string> Items(GameData data, Args options, Func<string?[], bool> matches)
    {
        var mastery = options.Get("mastery");
        var type = options.Get("type");
        var slot = options.Get("slot") is { } s ? Enum.Parse<EquipmentSlot>(s, ignoreCase: true) : (EquipmentSlot?)null;
        var tier = options.Get("tier") is { } t ? int.Parse(t) : (int?)null;

        return data.Items.Values
            .Where(i => matches([i.Id, i.Name, i.Type])
                && (mastery is null || i.Mastery == mastery)
                && (type is null || i.Type == type)
                && (slot is null || i.Slot == slot)
                && (tier is null || i.Tier == tier))
            .Select(i => Row(i.Id, i.Name, i.Slot.ToString(), i.Type ?? "", $"T{i.Tier}", $"{i.Price}G",
                i.TwoHanded ? "두손" : "", string.Join(", ", i.Actions)))
            .ToList();
    }

    /// <summary>행동을 어디서 얻는가: 공용, 무기 계열, 소환, 아이템 개수.</summary>
    private static string Source(GameData data, string id, bool universal, string? weapon, bool summonOnly)
    {
        if (universal)
        {
            return "공용";
        }

        if (summonOnly)
        {
            return "소환 전용";
        }

        var items = data.ItemsGranting(id).Count();
        return weapon is not null ? $"{weapon} 무기" : $"아이템 {items}개";
    }

    /// <summary>앞의 두 칸(id, 이름)은 폭을 맞춘다. 한글은 터미널에서 두 칸을 차지한다.</summary>
    private static string Row(params string[] columns) =>
        string.Join("  ", columns.Where(c => c.Length > 0).Select((c, i) => i < 2 ? Pad(c, i == 0 ? 22 : 20) : c)).TrimEnd();

    private static string Pad(string text, int width) =>
        text + new string(' ', Math.Max(0, width - text.Sum(ch => ch >= '\u1100' ? 2 : 1)));
}
