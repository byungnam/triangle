using Microsoft.Xna.Framework;
using Triangle.Core.Data;
using Triangle.Core.Progress;

namespace Triangle.Desktop;

/// <summary>
/// 화면들이 함께 쓰는 게임 상태: 게임 데이터, 회사, 세이브 파일, 저장하지 않은 변경 표시.
/// 마을에서는 수동 저장이고, 원정 중에는 전투가 끝날 때마다 자동 저장한다(<see cref="AutoSave"/>).
/// </summary>
internal sealed class GameSession(GameData data, Company company, SaveStore store)
{
    public GameData Data { get; } = data;
    public Company Company { get; } = company;

    /// <summary>마지막 저장 이후 바뀐 것이 있다.</summary>
    public bool Unsaved { get; private set; }

    /// <summary>화면 아래에 보여줄 안내 (저장 결과 등). 바뀌면 지운다.</summary>
    public (string Text, Color Color)? Notice { get; set; }

    public void MarkChanged()
    {
        Unsaved = true;
        Notice = null;
    }

    /// <summary>저장한다. 실패하면 안내에 이유를 남기고 false.</summary>
    public bool Save()
    {
        try
        {
            store.Save(Company);
            Unsaved = false;
            Notice = ($"저장했습니다 ({DateTime.Now:HH:mm:ss})", Theme.Heal);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notice = ($"저장하지 못했습니다: {e.Message}", Theme.Enemy);
            return false;
        }
    }

    /// <summary>원정 중 자동 저장. 실패해도 게임은 계속하고 안내만 남긴다.</summary>
    public void AutoSave()
    {
        if (Save())
        {
            Notice = ($"자동 저장했습니다 ({DateTime.Now:HH:mm:ss})", Theme.TextDim);
        }
    }
}
