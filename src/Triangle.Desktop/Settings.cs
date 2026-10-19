using System.Text.Json;

namespace Triangle.Desktop;

/// <summary>
/// 세이브와 따로 두는 기기별 설정 (음악 음량 등). 세이브 폴더의 settings.json에 둔다.
/// 없거나 읽지 못하면 기본값으로 시작한다.
/// </summary>
internal sealed record Settings(float MusicVolume = 0.6f, bool MusicMuted = false)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string PathNextTo(string savePath) =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(savePath))!, "settings.json");

    public static Settings Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new() : new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    /// <summary>저장한다. 실패해도 게임에는 지장이 없으니 넘어간다.</summary>
    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
