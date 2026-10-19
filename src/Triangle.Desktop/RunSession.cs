using Microsoft.Xna.Framework;
using Triangle.Core.Data;
using Triangle.Core.Runs;

namespace Triangle.Desktop;

/// <summary>
/// 판 화면들이 함께 쓰는 상태: 게임 데이터, 진행 중인 판과 그 세이브, 판을 넘어 남는 기록(로그라이트 재편).
/// 판은 바뀔 때마다 자동 저장하고(<see cref="Save"/>), 끝난 판은 새 판을 고를 때 기록에 넣고 지운다(<see cref="Finish"/>).
/// </summary>
internal sealed class RunSession(GameData data, RunStore store, ProfileStore profiles, Profile profile)
{
    public GameData Data { get; } = data;

    /// <summary>판을 넘어 남는 것: 명성, 해금, 기록.</summary>
    public Profile Profile { get; } = profile;

    /// <summary>진행 중이거나 막 끝난 판. 없으면 파티를 고른다.</summary>
    public Run? Run { get; private set; }

    /// <summary>화면 아래에 보여줄 안내 (저장 실패, 할 수 없는 이유 등).</summary>
    public (string Text, Color Color)? Notice { get; set; }

    /// <summary>불러온 판으로 시작한다 (세이브가 없으면 null).</summary>
    public void Resume(Run? run) => Run = run;

    /// <summary>새 판을 시작하고 저장한다. <paramref name="rows"/>는 멤버마다 서는 줄이다.</summary>
    public void Start(IReadOnlyList<string> classIds, IReadOnlyList<Triangle.Core.Units.Row> rows)
    {
        Run = Run.Start(Data, classIds, Random.Shared.Next(), Profile.UnlocksForRun(Data), rows);
        Save();
    }

    /// <summary>판의 변경을 저장한다. 실패해도 판은 계속하고 안내만 남긴다.</summary>
    public void Save()
    {
        if (Run is null)
        {
            return;
        }

        try
        {
            store.Save(Run);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notice = ($"저장하지 못했습니다: {e.Message}", Theme.Enemy);
        }
    }

    /// <summary>끝난 판의 명성과 기록을 받고(기록을 먼저 저장한다) 판을 치운다.</summary>
    public void Finish()
    {
        if (Run is { Finished: true } run)
        {
            Profile.Record(run, Data);
            SaveProfile();
        }

        Clear();
    }

    /// <summary>해금을 열고 기록을 저장한다.</summary>
    public void Unlock(string unlockId)
    {
        Profile.Unlock(Data, unlockId);
        SaveProfile();
    }

    private void SaveProfile()
    {
        try
        {
            profiles.Save(Profile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notice = ($"기록을 저장하지 못했습니다: {e.Message}", Theme.Enemy);
        }
    }

    /// <summary>판을 치우고 세이브를 지운다.</summary>
    private void Clear()
    {
        Run = null;
        try
        {
            store.Delete();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notice = ($"세이브를 지우지 못했습니다: {e.Message}", Theme.Enemy);
        }
    }
}
