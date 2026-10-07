using System.Text.Json.Nodes;
using Triangle.Core.Data;

namespace Triangle.DataTool;

/// <summary>원본 JSON을 그대로 보여 주기 위해 파일을 노드로 읽는다 (정의 클래스로 바꾸면 기본값까지 다 나온다).</summary>
internal static class RawData
{
    private static readonly string[] Files =
    [
        GameDataLoader.MasteriesFile, GameDataLoader.SkillsFile, GameDataLoader.ActionsFile, GameDataLoader.EffectsFile,
        GameDataLoader.ItemsFile, GameDataLoader.EncountersFile, GameDataLoader.ZonesFile, GameDataLoader.RecipesFile,
        GameDataLoader.RecruitsFile,
    ];

    /// <summary>id가 같은 항목 (제작법은 result). 종류가 달라 같은 id가 여러 파일에 있을 수 있다.</summary>
    public static List<(string File, JsonNode Node)> FindById(string directory, string id)
    {
        var found = new List<(string, JsonNode)>();
        foreach (var file in Files)
        {
            var key = file == GameDataLoader.RecipesFile ? "result" : "id";
            var array = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, file)))!.AsArray();
            foreach (var node in array)
            {
                if (node?[key]?.GetValue<string>() == id)
                {
                    found.Add((file, node));
                }
            }
        }

        return found;
    }
}
