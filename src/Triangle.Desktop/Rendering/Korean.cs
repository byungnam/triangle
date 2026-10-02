namespace Triangle.Desktop.Rendering;

/// <summary>이름 뒤 조사 선택 (받침 유무에 따라 이/가, 을/를, 은/는, 와/과, 으로/로).</summary>
internal static class Korean
{
    public static string IGa(string word) => word + (HasFinalConsonant(word) ? "이" : "가");

    public static string EulReul(string word) => word + (HasFinalConsonant(word) ? "을" : "를");

    public static string EunNeun(string word) => word + (HasFinalConsonant(word) ? "은" : "는");

    public static string WaGwa(string word) => word + (HasFinalConsonant(word) ? "과" : "와");

    /// <summary>으로/로: 받침이 없거나 ㄹ 받침이면 "로".</summary>
    public static string EuroRo(string word) => word + (HasFinalConsonant(word) && !EndsWithRieul(word) ? "으로" : "로");

    private static bool EndsWithRieul(string word) => word.Length > 0 && word[^1] is >= '가' and <= '힣' && (word[^1] - '가') % 28 == 8;

    private static bool HasFinalConsonant(string word)
    {
        if (word.Length == 0)
        {
            return false;
        }

        var last = word[^1];
        return last is >= '가' and <= '힣'
            ? (last - '가') % 28 != 0
            // 한글이 아니면 숫자/영문 끝소리로 대략 판단한다 (예: "훈련병 A" → 에이 → 받침 없음).
            : "013678LMNRlmnr".Contains(last);
    }
}
