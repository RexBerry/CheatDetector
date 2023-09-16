using System.Globalization;
using System.Text;

namespace CheatingDetector;

public class CppMinifier
{
    public string Minify(string src)
    {
        // This likely will not produce equivalent valid C++ code
        // This is only meant to reduce differences between strings
        // This does not attempt to detect strings or comments

        src = src
            .Normalize(NormalizationForm.FormKC)
            .ReplaceLineEndings();

        List<int> codepoints = new();
        for (int i = 0; i < src.Length;)
        {
            int codepoint = char.ConvertToUtf32(src, i);
            codepoints.Add(codepoint);
            i += char.IsHighSurrogate(src[i]) ? 2 : 1;
        }

        StringBuilder sb = new();
        for (int i = 0; i < codepoints.Count;)
        {
            int codepoint = codepoints[i];
            if (!IsWhitespace(codepoints[i]))
            {
                sb.Append(char.ConvertFromUtf32(codepoint));
                ++i;
                continue;
            }

            if (i == 0 || !IsIdentifierChar(codepoints[i - 1]))
            {
                ++i;
                continue;
            }

            int j = i + 1;
            while (j < codepoints.Count && IsWhitespace(codepoints[j]))
            {
                j++;
            }

            if (
                i > 0
                && j < codepoints.Count
                && IsIdentifierChar(codepoints[i - 1])
                && IsIdentifierChar(codepoints[j])
            )
            {
                sb.Append(' ');
            }

            i = j;
        }

        return sb.ToString();
    }

    private bool IsIdentifierChar(int codepoint)
    {
        // Probably not 100% correct, but good enough for me

        string s = char.ConvertFromUtf32(codepoint);
        if (s.Length > 1)
        {
            return false;
        }

        return s[0] == '_' || char.IsLetterOrDigit(s[0]);
    }

    private bool IsWhitespace(int codepoint)
    {
        // Probably not 100% correct, but good enough for me

        string s = char.ConvertFromUtf32(codepoint);
        if (s.Length > 1)
        {
            return false;
        }

        return char.IsWhiteSpace(s[0]);
    }
}
