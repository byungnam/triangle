using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Triangle.Desktop.Audio;

/// <summary>
/// 배경음악. 화면마다 곡 이름(<see cref="Tracks"/>)을 정하면 지금 곡을 줄여 끄고 새 곡을 키우며 반복 재생한다.
/// 곡은 실행 폴더의 Music/&lt;이름&gt;.ogg이고, 파일이 없는 곡은 조용히 넘어간다.
/// Music/&lt;이름&gt;-1.ogg, -2.ogg처럼 여러 벌을 두면 그 곡을 틀 때마다 하나를 고른다 (바로 전에 튼 것은 피한다).
/// 소리 장치가 없으면 (헤드리스 확인 등) 아무것도 하지 않는다.
/// </summary>
internal sealed class MusicPlayer : IDisposable
{
    /// <summary>줄여 끄거나 키우는 데 걸리는 시간 (초).</summary>
    private const float FadeSeconds = 0.8f;

    private readonly string _directory;
    private readonly Dictionary<string, Song?> _songs = [];
    private readonly Dictionary<string, string> _lastFile = [];
    private bool _disabled;
    private string? _wanted;
    private string? _playing;

    /// <summary>0이면 줄여서 꺼진 상태, 1이면 설정 음량.</summary>
    private float _fade;

    public MusicPlayer(string directory, float volume, bool muted)
    {
        _directory = directory;
        Volume = volume;
        Muted = muted;
    }

    /// <summary>설정 음량 0~1.</summary>
    public float Volume { get; private set; }

    public bool Muted { get; private set; }

    /// <summary>재생할 곡을 정한다. null이면 음악을 끈다. 같은 곡이면 이어서 튼다.</summary>
    public void Play(string? track) => _wanted = track;

    /// <summary>음량을 바꾼다 (0~1, 10% 단위로 맞춘다). 음량을 바꾸면 음소거를 푼다.</summary>
    public void ChangeVolume(float delta)
    {
        Volume = Math.Clamp(MathF.Round((Volume + delta) * 10) / 10, 0f, 1f);
        Muted = false;
    }

    public void ToggleMute() => Muted = !Muted;

    public void Update(GameTime gameTime)
    {
        if (_disabled)
        {
            return;
        }

        try
        {
            var step = (float)gameTime.ElapsedGameTime.TotalSeconds / FadeSeconds;
            if (_playing != _wanted)
            {
                // 다른 곡으로 바꿀 때는 지금 곡을 다 줄인 뒤 바꾼다.
                _fade = Math.Max(0f, _fade - step);
                if (_fade == 0f || _playing is null)
                {
                    Switch();
                }
            }
            else if (_playing is not null)
            {
                _fade = Math.Min(1f, _fade + step);
            }

            MediaPlayer.Volume = Muted ? 0f : Volume * _fade;
        }
        catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException)
        {
            _disabled = true;
        }
    }

    private void Switch()
    {
        MediaPlayer.Stop();
        _playing = _wanted;
        _fade = 0f;
        if (_playing is not null && Pick(_playing) is { } file && Load(file) is { } song)
        {
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = 0f;
            MediaPlayer.Play(song);
        }
    }

    /// <summary>곡 이름에 맞는 파일 하나를 고른다. 여러 벌이면 바로 전에 튼 것을 빼고 무작위로 고른다. 없으면 null.</summary>
    private string? Pick(string track)
    {
        string[] files;
        try
        {
            files = Directory.Exists(_directory)
                ? Directory.GetFiles(_directory, track + "-*.ogg").Order().ToArray()
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            files = [];
        }

        if (files.Length == 0)
        {
            return Path.Combine(_directory, track + ".ogg");
        }

        var candidates = files.Length > 1 && _lastFile.TryGetValue(track, out var last)
            ? files.Where(f => f != last).ToArray()
            : files;
        var file = candidates[Random.Shared.Next(candidates.Length)];
        _lastFile[track] = file;
        return file;
    }

    /// <summary>곡 파일을 처음 쓸 때 읽어 둔다. 파일이 없거나 읽지 못하면 null (다시 시도하지 않는다).</summary>
    private Song? Load(string path)
    {
        if (!_songs.TryGetValue(path, out var song))
        {
            try
            {
                song = File.Exists(path) ? Song.FromUri(Path.GetFileNameWithoutExtension(path), new Uri(path)) : null;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
            {
                song = null;
            }

            _songs[path] = song;
        }

        return song;
    }

    public void Dispose()
    {
        if (!_disabled)
        {
            try
            {
                MediaPlayer.Stop();
            }
            catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException)
            {
            }
        }

        foreach (var song in _songs.Values)
        {
            song?.Dispose();
        }
    }
}

/// <summary>곡 이름. Music 폴더의 파일 이름(.ogg 앞)과 같다.</summary>
internal static class Tracks
{
    /// <summary>메인 테마. 판 밖의 화면: 파티 고르기, 해금·기록, 결과.</summary>
    public const string Lobby = "lobby";

    /// <summary>진행 중인 판의 화면 (지도, 보상, 상점, 휴식, 이벤트, 편성).</summary>
    public const string Expedition = "expedition";

    /// <summary>전투 기록 화면. 여러 벌(battle-1.ogg 등)을 두고 전투마다 하나를 고른다.</summary>
    public const string Battle = "battle";
}
