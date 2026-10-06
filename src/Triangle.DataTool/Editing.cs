using System.Text.Json.Nodes;
using Triangle.Core.Data;

namespace Triangle.DataTool;

/// <summary>
/// 수정 명령 (set, unset, add, remove, new, copy, delete). 모두 고친 뒤 로더 검증을 통과해야 파일에 쓴다.
/// 하나라도 실패하면 아무 파일도 바꾸지 않는다.
/// </summary>
internal static class Editing
{
    public static readonly HashSet<string> Commands = ["set", "unset", "add", "remove", "new", "copy", "delete"];

    public static int Run(string command, IReadOnlyList<string> rest, Args options, string directory)
    {
        var document = DataDocument.Load(directory);
        var where = options.GetAll("where");
        var changes = new List<string>();

        IReadOnlyList<JsonObject> Select(string kind, string ids)
        {
            var selected = document.Select(kind, ids, where);
            if (selected.Count == 0)
            {
                throw new ArgumentException($"{kind}: nothing matches '{ids}'{(where.Count > 0 ? " with " + string.Join(", ", where) : "")}");
            }

            return selected;
        }

        try
        {
            switch (command, rest.Count)
            {
                case ("set", 4):
                    foreach (var entry in Select(rest[0], rest[1]))
                    {
                        var old = DataPath.Set(entry, rest[2], rest[3]);
                        changes.Add($"{At(rest[0], entry)}: {rest[2]} {Show(old)} -> {Show(DataPath.Get(entry, rest[2]))}");
                    }

                    break;

                case ("unset", 3):
                    foreach (var entry in Select(rest[0], rest[1]))
                    {
                        var old = DataPath.Unset(entry, rest[2]);
                        if (old is not null)
                        {
                            changes.Add($"{At(rest[0], entry)}: {rest[2]} {Show(old)} -> (기본값)");
                        }
                    }

                    break;

                case ("add", 4):
                    foreach (var entry in Select(rest[0], rest[1]))
                    {
                        if (DataPath.Add(entry, rest[2], rest[3]))
                        {
                            changes.Add($"{At(rest[0], entry)}: {rest[2]} += {rest[3]}");
                        }
                    }

                    break;

                case ("remove", 4):
                    foreach (var entry in Select(rest[0], rest[1]))
                    {
                        if (DataPath.Remove(entry, rest[2], rest[3]) > 0)
                        {
                            changes.Add($"{At(rest[0], entry)}: {rest[2]} -= {rest[3]}");
                        }
                    }

                    break;

                case ("new", 2):
                {
                    var entry = JsonNode.Parse(rest[1]) as JsonObject ?? throw new ArgumentException("new needs a JSON object");
                    document.Insert(rest[0], entry, options.Get("after"));
                    changes.Add($"{At(rest[0], entry)}: 새로 만듦");
                    break;
                }

                case ("copy", 3):
                {
                    var source = document.Entries(rest[0]).SingleOrDefault(e => DataDocument.IdOf(rest[0], e) == rest[1])
                        ?? throw new ArgumentException($"{rest[0]}: no '{rest[1]}'");
                    var entry = source.DeepClone().AsObject();
                    entry[DataDocument.KeyOf(rest[0])] = rest[2];
                    document.Insert(rest[0], entry, options.Get("after") ?? rest[1]);
                    changes.Add($"{At(rest[0], entry)}: '{rest[1]}'에서 복사");
                    break;
                }

                case ("delete", 2):
                    foreach (var id in Select(rest[0], rest[1]).Select(e => DataDocument.IdOf(rest[0], e)).ToList())
                    {
                        document.Remove(rest[0], id);
                        changes.Add($"{rest[0]} '{id}': 지움");
                    }

                    break;

                default:
                    Console.Error.WriteLine($"'{command}'의 인자 개수가 맞지 않는다. --help 참고.");
                    return 2;
            }
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.Text.Json.JsonException or FormatException)
        {
            Console.Error.WriteLine($"수정 실패, 저장하지 않았다: {e.Message}");
            return 1;
        }

        foreach (var change in changes)
        {
            Console.WriteLine(change);
        }

        if (changes.Count == 0)
        {
            Console.WriteLine("바뀐 것 없음");
            return 0;
        }

        // 항목을 고친 종류만 저장 대상이 된다 (set·add 등은 항목 노드를 직접 바꾼다).
        document.MarkChanged(rest[0]);

        GameData data;
        try
        {
            data = document.Validate();
        }
        catch (GameDataException e)
        {
            Console.WriteLine($"검증 오류 {e.Errors.Count}개, 저장하지 않았다:");
            foreach (var error in e.Errors)
            {
                Console.WriteLine($"  - {error}");
            }

            return 1;
        }

        if (options.Has("dry-run"))
        {
            Console.WriteLine($"검증 통과, {changes.Count}건 (--dry-run이라 저장하지 않았다)");
        }
        else
        {
            document.Save(directory);
            Console.WriteLine($"저장함: {changes.Count}건, {DataDocument.Files[rest[0]]}");
        }

        var warnings = DataLint.Check(data);
        if (warnings.Count > 0)
        {
            Console.WriteLine($"경고 {warnings.Count}개:");
            foreach (var warning in warnings)
            {
                Console.WriteLine($"  - {warning}");
            }
        }

        return 0;
    }

    private static string At(string kind, JsonObject entry) => $"{kind} '{DataDocument.IdOf(kind, entry)}'";

    private static readonly System.Text.Json.JsonSerializerOptions OneLine = new(GameDataJson.Options) { WriteIndented = false };

    private static string Show(JsonNode? node) => node?.ToJsonString(OneLine) ?? "(없음)";
}
