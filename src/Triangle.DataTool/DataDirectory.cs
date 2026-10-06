using Triangle.Core.Data;

namespace Triangle.DataTool;

internal static class DataDirectory
{
    /// <summary>현재 폴더에서 위로 올라가며 data/masteries.json이 있는 폴더를 찾는다.</summary>
    public static string? Find()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, GameDataLoader.MasteriesFile)))
            {
                return candidate;
            }
        }

        return null;
    }
}
