using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Triangle.Desktop.Audio;

/// <summary>
/// 배경음악. 화면마다 곡 이름(<see cref="Tracks"/>)을 정하면 지금 곡을 줄여 끄고 새 곡을 키우며 반복 재생한다.
/// 곡은 실행 폴더의 Music/&lt;이름&gt;.ogg이고, 파일이 없는 곡은 조용히 넘어간다.
/// 소리 장치가 없으면 (헤드리스 확인 등) 아무것도 하지 않는다.
/// </summary>
internal sealed class MusicPlayer : IDisposable
{
    /// <summary>줄여 끄거나 키우는 데 걸리는 시간 (초).</summary>
    private const float FadeSeconds = 0.8f;

    private readonly string _directory;
    private readonly Dictionary<string, Song?> _songs = [];
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
        if (_playing is not null && Load(_playing) is { } song)
        {
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = 0f;
            MediaPlayer.Play(song);
        }
    }

    /// <summary>곡을 처음 쓸 때 읽어 둔다. 파일이 없거나 읽지 못하면 null (다시 시도하지 않는다).</summary>
    private Song? Load(string track)
    {
        if (!_songs.TryGetValue(track, out var song))
        {
            var path = Path.Combine(_directory, track + ".ogg");
            try
            {
                song = File.Exists(path) ? Song.FromUri(track, new Uri(path)) : null;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
            {
                song = null;
            }

            _songs[track] = song;
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
    /// <summary>마을과 마을에서 여는 화면들 (편성, 상점, 제작 등).</summary>
    public const string Village = "village";

    /// <summary>원정 화면 (전투 사이).</summary>
    public const string Expedition = "expedition";

    /// <summary>전투 기록 화면.</summary>
    public const string Battle = "battle";
}
