using FontStashSharp;

namespace Triangle.Desktop.Rendering;

/// <summary>직접 그리는 글자의 줄바꿈과 자르기.</summary>
internal static class TextLayout
{
    /// <summary>폭을 넘으면 공백 단위로 줄을 바꾼다. 공백 없는 긴 단어는 그대로 둔다.</summary>
    public static List<string> Wrap(SpriteFontBase font, string text, float width)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' '))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && font.MeasureString(candidate).X > width)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        lines.Add(current);
        return lines;
    }

    /// <summary>폭을 넘으면 뒤를 잘라 "…"를 붙인다.</summary>
    public static string Truncate(SpriteFontBase font, string text, float width)
    {
        if (font.MeasureString(text).X <= width)
        {
            return text;
        }

        var length = text.Length;
        while (length > 0 && font.MeasureString(text[..length] + "…").X > width)
        {
            length--;
        }

        return text[..length] + "…";
    }
}
