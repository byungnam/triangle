using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Triangle.Core.Data;

/// <summary>
/// 편집용 게임 데이터: data/*.json을 정의 클래스가 아니라 JSON 노드로 들고 있어서, 파일에 적힌 모양 그대로 고치고 다시 쓴다.
/// 고친 뒤 <see cref="Validate"/>(로더 검증)를 통과해야 저장한다. 명령줄 도구와 편집기 GUI가 함께 쓴다.
/// </summary>
public sealed class DataDocument
{
    /// <summary>종류 이름 → 파일 이름 (로드·검증 순서).</summary>
    public static IReadOnlyDictionary<string, string> Files { get; } = new Dictionary<string, string>
    {
        ["masteries"] = GameDataLoader.MasteriesFile,
        ["skills"] = GameDataLoader.SkillsFile,
        ["actions"] = GameDataLoader.ActionsFile,
        ["effects"] = GameDataLoader.EffectsFile,
        ["encounters"] = GameDataLoader.EncountersFile,
        ["items"] = GameDataLoader.ItemsFile,
        ["zones"] = GameDataLoader.ZonesFile,
        ["recruits"] = GameDataLoader.RecruitsFile,
        ["recipes"] = GameDataLoader.RecipesFile,
    };

    private readonly Dictionary<string, JsonArray> _entries;
    private readonly HashSet<string> _changed = [];

    private DataDocument(Dictionary<string, JsonArray> entries) => _entries = entries;

    /// <summary>고친 종류 (저장할 파일).</summary>
    public IReadOnlyCollection<string> Changed => _changed;

    public static DataDocument Load(string directory) =>
        FromJson(Files.ToDictionary(f => f.Key, f => File.ReadAllText(Path.Combine(directory, f.Value))));

    /// <summary>종류 이름 → JSON. 빠진 종류는 빈 배열.</summary>
    public static DataDocument FromJson(IReadOnlyDictionary<string, string> json) =>
        new(Files.Keys.ToDictionary(kind => kind, kind => JsonNode.Parse(json.GetValueOrDefault(kind, "[]"))!.AsArray()));

    /// <summary>항목을 가리키는 속성: 제작법은 결과 아이템, 나머지는 id.</summary>
    public static string KeyOf(string kind) => kind == "recipes" ? "result" : "id";

    public static string IdOf(string kind, JsonObject entry) => entry[KeyOf(kind)]?.GetValue<string>() ?? "";

    public IReadOnlyList<JsonObject> Entries(string kind) => Array(kind).Select(n => n!.AsObject()).ToList();

    /// <summary>
    /// 고를 항목: id 패턴(쉼표로 여럿, <c>*</c>는 아무 글자)에 맞고 모든 조건을 채우는 항목 (데이터 순서).
    /// 조건은 <see cref="DataPath.Matches"/>.
    /// </summary>
    public IReadOnlyList<JsonObject> Select(string kind, string idPattern, IReadOnlyList<string> where)
    {
        var patterns = idPattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Entries(kind)
            .Where(e => patterns.Any(p => Wildcard(p, IdOf(kind, e))) && where.All(w => DataPath.Matches(e, w)))
            .ToList();
    }

    /// <summary>항목을 고친 뒤 부른다 (저장 대상 표시).</summary>
    public void MarkChanged(string kind) => _changed.Add(kind);

    /// <summary>새 항목을 넣는다. <paramref name="afterId"/>가 있으면 그 뒤, 없으면 맨 끝.</summary>
    public void Insert(string kind, JsonObject entry, string? afterId = null)
    {
        var array = Array(kind);
        var id = IdOf(kind, entry);
        if (array.Any(n => IdOf(kind, n!.AsObject()) == id))
        {
            throw new ArgumentException($"{kind}: '{id}' already exists");
        }

        var index = array.Count;
        if (afterId is not null)
        {
            index = IndexOf(kind, afterId) + 1;
        }

        array.Insert(index, entry);
        MarkChanged(kind);
    }

    /// <summary>항목을 지운다. 가리키는 곳이 남아 있으면 <see cref="Validate"/>가 잡는다.</summary>
    public void Remove(string kind, string id)
    {
        Array(kind).RemoveAt(IndexOf(kind, id));
        MarkChanged(kind);
    }

    /// <summary>로더 검증. 통과하면 그 데이터를, 아니면 <see cref="GameDataException"/>을 던진다.</summary>
    public GameData Validate()
    {
        string J(string kind) => ToJson(kind);
        return GameDataLoader.Parse(J("masteries"), J("skills"), J("actions"), J("encounters"), J("effects"), J("items"), J("zones"),
            J("recruits"), J("recipes"));
    }

    /// <summary>파일에 쓸 내용 (게임 데이터 JSON 설정, 끝에 줄바꿈).</summary>
    public string ToJson(string kind) => Array(kind).ToJsonString(GameDataJson.Options) + "\n";

    /// <summary>고친 파일만 쓴다. 검증은 먼저 따로 한다.</summary>
    public void Save(string directory)
    {
        foreach (var kind in _changed)
        {
            File.WriteAllText(Path.Combine(directory, Files[kind]), ToJson(kind));
        }

        _changed.Clear();
    }

    private JsonArray Array(string kind) =>
        _entries.TryGetValue(kind, out var array) ? array : throw new ArgumentException($"unknown kind '{kind}'");

    private int IndexOf(string kind, string id)
    {
        var array = Array(kind);
        for (var i = 0; i < array.Count; i++)
        {
            if (IdOf(kind, array[i]!.AsObject()) == id)
            {
                return i;
            }
        }

        throw new ArgumentException($"{kind}: no '{id}'");
    }

    private static bool Wildcard(string pattern, string text)
    {
        var parts = pattern.Split('*');
        if (parts.Length == 1)
        {
            return pattern == text;
        }

        if (!text.StartsWith(parts[0], StringComparison.Ordinal) || !text.EndsWith(parts[^1], StringComparison.Ordinal))
        {
            return false;
        }

        var at = parts[0].Length;
        foreach (var part in parts[1..^1])
        {
            var found = text.IndexOf(part, at, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            at = found + part.Length;
        }

        return at <= text.Length - parts[^1].Length;
    }
}

/// <summary>
/// 항목 안의 값 경로와 값 표기.
/// - 경로: 점으로 잇는다. 배열은 번호(0부터)나 원소의 id로 고른다. 예: <c>rewards.goldMin</c>, <c>units.e1.stats.str</c>, <c>tactics.0.actionId</c>
/// - 값: JSON으로 읽히면 JSON(<c>50</c>, <c>true</c>, <c>["a","b"]</c>, <c>{"kind":"PowerPercent","percent":5}</c>), 아니면 문자열.
///   <c>*=1.2</c>, <c>/=2</c>, <c>+=10</c>, <c>-=5</c>는 지금 숫자를 계산한다(정수였으면 반올림해서 정수).
/// </summary>
public static class DataPath
{
    /// <summary>값을 바꾼다. 없는 속성은 만든다. 바꾸기 전 값(없었으면 null)을 돌려준다.</summary>
    public static JsonNode? Set(JsonObject entry, string path, string value)
    {
        var (parent, last) = Parent(entry, path, create: true);
        var old = Child(parent, last);
        var next = Arithmetic(old, value, path) ?? ParseValue(value);
        Put(parent, last, next);
        return old?.DeepClone();
    }

    /// <summary>속성을 지워 기본값으로 돌린다. 지운 값(없었으면 null)을 돌려준다.</summary>
    public static JsonNode? Unset(JsonObject entry, string path)
    {
        var (parent, last) = Parent(entry, path, create: false);
        if (parent is not JsonObject obj || !obj.ContainsKey(last))
        {
            return null;
        }

        var old = obj[last]?.DeepClone();
        obj.Remove(last);
        return old;
    }

    /// <summary>배열에 값을 붙인다 (배열이 없으면 만든다). 이미 같은 값이 있으면 아무것도 안 하고 false.</summary>
    public static bool Add(JsonObject entry, string path, string value)
    {
        var (parent, last) = Parent(entry, path, create: true);
        if (Child(parent, last) is not JsonArray array)
        {
            if (Child(parent, last) is not null)
            {
                throw new ArgumentException($"'{path}' is not an array");
            }

            array = [];
            Put(parent, last, array);
        }

        var node = ParseValue(value);
        if (array.Any(n => JsonNode.DeepEquals(n, node)))
        {
            return false;
        }

        array.Add(node);
        return true;
    }

    /// <summary>배열에서 같은 값을 모두 뺀다. 뺀 개수.</summary>
    public static int Remove(JsonObject entry, string path, string value)
    {
        var (parent, last) = Parent(entry, path, create: false);
        if (Child(parent, last) is not JsonArray array)
        {
            return 0;
        }

        var node = ParseValue(value);
        var removed = 0;
        for (var i = array.Count - 1; i >= 0; i--)
        {
            if (JsonNode.DeepEquals(array[i], node))
            {
                array.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>경로의 값. 없으면 null.</summary>
    public static JsonNode? Get(JsonObject entry, string path)
    {
        var (parent, last) = Parent(entry, path, create: false);
        return Child(parent, last);
    }

    /// <summary>경로의 값. 경로가 없거나 맞지 않으면 null (조건 검사용).</summary>
    private static JsonNode? TryGet(JsonObject entry, string path)
    {
        try
        {
            return Get(entry, path);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// 조건 <c>경로=값</c>, <c>경로!=값</c>, <c>경로&gt;=수</c>, <c>경로&lt;=수</c>, <c>경로&gt;수</c>, <c>경로&lt;수</c>.
    /// 값이 배열이면 <c>=</c>는 "포함", <c>!=</c>는 "포함하지 않음". 파일에 적힌 값만 본다(생략한 속성은 없음으로 본다).
    /// </summary>
    public static bool Matches(JsonObject entry, string condition)
    {
        foreach (var op in (string[])["!=", ">=", "<=", "=", ">", "<"])
        {
            var at = condition.IndexOf(op, StringComparison.Ordinal);
            if (at <= 0)
            {
                continue;
            }

            var actual = TryGet(entry, condition[..at]);
            var text = condition[(at + op.Length)..];
            if (op is "=" or "!=")
            {
                var expected = ParseValue(text);
                var equal = actual is JsonArray array
                    ? array.Any(n => JsonNode.DeepEquals(n, expected))
                    : JsonNode.DeepEquals(actual, expected);
                return equal == (op == "=");
            }

            if (!TryNumber(actual, out var number, out _))
            {
                return false;
            }

            var limit = double.Parse(text, CultureInfo.InvariantCulture);
            return op switch
            {
                ">=" => number >= limit,
                "<=" => number <= limit,
                ">" => number > limit,
                _ => number < limit,
            };
        }

        throw new ArgumentException($"condition '{condition}' needs =, !=, >=, <=, > or <");
    }

    public static JsonNode? ParseValue(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);
        }
    }

    private static JsonNode? Arithmetic(JsonNode? old, string value, string path)
    {
        if (value.Length < 3 || value[1] != '=' || "*/+-".IndexOf(value[0]) < 0)
        {
            return null;
        }

        if (!double.TryParse(value[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var operand))
        {
            return null;
        }

        if (!TryNumber(old, out var current, out var integer))
        {
            throw new ArgumentException($"'{path}' has no number to calculate with");
        }

        var result = value[0] switch
        {
            '*' => current * operand,
            '/' => current / operand,
            '+' => current + operand,
            _ => current - operand,
        };

        return integer
            ? JsonValue.Create((long)Math.Round(result, MidpointRounding.AwayFromZero))
            : JsonValue.Create(result);
    }

    /// <summary>숫자 값인가. 정수로 적혔는지도 알려 준다 (JSON 원본이든 만든 값이든).</summary>
    private static bool TryNumber(JsonNode? node, out double number, out bool integer)
    {
        number = 0;
        integer = false;
        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.Number)
        {
            return false;
        }

        var text = value.ToJsonString();
        integer = !text.Contains('.') && !text.Contains('e') && !text.Contains('E');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    private static (JsonNode Parent, string Last) Parent(JsonObject entry, string path, bool create)
    {
        var segments = path.Split('.');
        if (segments.Any(s => s.Length == 0))
        {
            throw new ArgumentException($"bad path '{path}'");
        }

        JsonNode current = entry;
        foreach (var segment in segments[..^1])
        {
            var next = Child(current, segment);
            if (next is null)
            {
                if (!create || current is not JsonObject)
                {
                    throw new ArgumentException($"'{path}': no '{segment}'");
                }

                next = new JsonObject();
                Put(current, segment, next);
            }

            current = next;
        }

        return (current, segments[^1]);
    }

    private static JsonNode? Child(JsonNode? node, string segment) => node switch
    {
        JsonObject obj => obj[segment],
        JsonArray array => array[ArrayIndex(array, segment)],
        _ => null,
    };

    private static void Put(JsonNode parent, string segment, JsonNode? value)
    {
        switch (parent)
        {
            case JsonObject obj:
                obj[segment] = value;
                break;
            case JsonArray array:
                array[ArrayIndex(array, segment)] = value;
                break;
            default:
                throw new ArgumentException($"cannot set '{segment}' inside a value");
        }
    }

    /// <summary>배열 원소: 번호, 아니면 id가 같은 원소.</summary>
    private static int ArrayIndex(JsonArray array, string segment)
    {
        if (int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            if (index < array.Count)
            {
                return index;
            }

            throw new ArgumentException($"index {segment} is out of range (0-{array.Count - 1})");
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is JsonObject o && o["id"]?.GetValue<string>() == segment)
            {
                return i;
            }
        }

        throw new ArgumentException($"no element '{segment}'");
    }
}
