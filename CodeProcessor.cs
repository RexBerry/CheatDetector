using System.Text;

namespace CheatingDetector;

public enum WhitespaceProcessingOptions
{
    KeepWhitespace,
    ReduceWhitespace,
}

public enum ContentProcessingOptions
{
    KeepContent,
    ObscureAlphanumeric,
    ObscureContent,
    ObscureAndShortenAlphanumeric,
    ObscureAndShortenContent,
    RemoveAlphanumeric,
    RemoveContent,
}

public enum StringLiteralProcessingOptions
{
    KeepContent,
    RemoveContent,
}

public enum CommentProcessingOptions
{
    KeepContent,
    RemoveContent,
    RemoveComments,
}

public class CodeProcessor
{
    public string Process(
        string src,
        WhitespaceProcessingOptions whitespaceProcessing
            = WhitespaceProcessingOptions.KeepWhitespace,
        ContentProcessingOptions contentProcessing
            = ContentProcessingOptions.KeepContent,
        StringLiteralProcessingOptions stringLiteralProcessing
            = StringLiteralProcessingOptions.KeepContent,
        CommentProcessingOptions commentProcessing
            = CommentProcessingOptions.KeepContent
    )
    {
        // TODO: Implement contentProcessing
        // This method is too big

        string code = src
            .Normalize(NormalizationForm.FormC)
            .ReplaceLineEndings();

        List<int> codepoints;
        StringBuilder sb;

        bool removeWhitespace
            = whitespaceProcessing
                is not WhitespaceProcessingOptions.KeepWhitespace;
        bool removeStringContent
            = stringLiteralProcessing
                is not StringLiteralProcessingOptions.KeepContent;
        bool removeCommentContent
            = commentProcessing
                is not CommentProcessingOptions.KeepContent;
        bool removeComments
            = commentProcessing is CommentProcessingOptions.RemoveComments;

        codepoints = GetCodepoints(code).ToList();
        sb = new();
        bool previousWasContent = false;
        for (int i = 0; i < codepoints.Count;)
        {
            int codepoint = codepoints[i];

            if (removeWhitespace && IsWhitespace(codepoint))
            {
                previousWasContent = false;

                if (i == 0 || !IsAlphanumericChar(codepoints[i - 1]))
                {
                    ++i;
                    continue;
                }

                int j = i + 1;
                while (j < codepoints.Count && IsWhitespace(codepoints[j]))
                {
                    ++j;
                }

                if (
                    i > 0
                    && j < codepoints.Count
                    && IsAlphanumericChar(codepoints[i - 1])
                    && IsAlphanumericChar(codepoints[j])
                )
                {
                    sb.Append(' ');
                }

                i = j;
                continue;
            }

            if (
                codepoint == '"'
                || (
                    codepoint == '\''
                    && (
                        i <= 0
                        || !IsDigit(codepoints[i - 1])
                        || codepoints.Count - i <= 1
                        || !IsDigit(codepoints[i + 1])
                    )
                )
            )
            {
                previousWasContent = false;

                sb.Append(char.ConvertFromUtf32(codepoint));
                int beginQuote = codepoint;
                bool isCharLiteral = beginQuote == '\'';
                bool isRawString
                    = !isCharLiteral && i > 0 && codepoints[i - 1] == 'R';
                ++i;

                if (isRawString)
                {
                    int delimiterBegin = i;
                    while (i < codepoints.Count)
                    {
                        codepoint = codepoints[i];
                        if (codepoint == '(')
                        {
                            break;
                        }

                        if (!removeStringContent)
                        {
                            sb.Append(char.ConvertFromUtf32(codepoint));
                        }
                        ++i;
                    }
                    int delimiterEnd = i;
                    int delimiterSize = delimiterEnd - delimiterBegin;

                    if (i >= codepoints.Count)
                    {
                        continue;
                    }

                    if (!removeStringContent)
                    {
                        sb.Append(char.ConvertFromUtf32(codepoints[i]));
                        ++i;
                    }

                    while (i < codepoints.Count)
                    {
                        codepoint = codepoints[i];

                        if (!removeStringContent)
                        {
                            sb.Append(char.ConvertFromUtf32(codepoint));
                        }
                        ++i;

                        if (codepoint != ')')
                        {
                            continue;
                        }

                        if (codepoints.Count - i < delimiterSize + 1)
                        {
                            continue;
                        }

                        bool foundDelimiter = true;
                        int j;
                        for (j = 0; j < delimiterSize; ++j)
                        {
                            codepoint = codepoints[i + j];
                            if (!removeStringContent)
                            {
                                sb.Append(char.ConvertFromUtf32(codepoint));
                            }

                            if (
                                codepoint
                                != codepoints[delimiterBegin + j]
                            )
                            {
                                foundDelimiter = false;
                                ++j;
                                break;
                            }
                        }

                        i += j;
                        codepoint = codepoints[i];
                        if (codepoint == beginQuote)
                        {
                            sb.Append(char.ConvertFromUtf32(codepoint));
                        }
                        else
                        {
                            if (!removeStringContent)
                            {
                                sb.Append(char.ConvertFromUtf32(codepoint));
                            }

                            foundDelimiter = false;
                        }
                        ++i;

                        if (foundDelimiter)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    while (i < codepoints.Count)
                    {
                        codepoint = codepoints[i];

                        if (codepoint == beginQuote)
                        {
                            sb.Append(char.ConvertFromUtf32(codepoint));
                            ++i;
                            break;
                        }

                        if (i == '\n')
                        {
                            break;
                        }

                        if (isCharLiteral || !removeStringContent)
                        {
                            sb.Append(char.ConvertFromUtf32(codepoint));
                        }
                        ++i;

                        if (codepoint == '\\')
                        {
                            if (i < codepoints.Count)
                            {
                                if (isCharLiteral || !removeStringContent)
                                {
                                    sb.Append(
                                        char.ConvertFromUtf32(
                                            codepoints[i]
                                        )
                                    );
                                }
                                ++i;
                            }
                        }
                    }
                }

                continue;
            }

            if (
                codepoint == '/'
                && codepoints.Count - i > 1
                && codepoints[i + 1] == '/'
            )
            {
                previousWasContent = false;

                if (!removeComments)
                {
                    sb.Append("//");
                }
                i += 2;

                while (i < codepoints.Count)
                {
                    codepoint = codepoints[i];

                    if (
                        codepoint == '\n'
                        && !(i > 0 && codepoints[i - 1] == '\\')
                    )
                    {
                        if (!removeComments)
                        {
                            sb.Append(char.ConvertFromUtf32(codepoint));
                            ++i;
                        }
                        break;
                    }
                    else if (!removeCommentContent)
                    {
                        sb.Append(char.ConvertFromUtf32(codepoint));
                    }
                    ++i;
                }

                continue;
            }

            if (
                codepoint == '/'
                && codepoints.Count - i > 1
                && codepoints[i + 1] == '*'
            )
            {
                previousWasContent = false;

                if (!removeComments)
                {
                    sb.Append("/*");
                }
                i += 2;

                while (i < codepoints.Count)
                {
                    codepoint = codepoints[i];

                    if (
                        codepoint == '*'
                        && codepoints.Count - i > 1
                        && codepoints[i + 1] == '/'
                    )
                    {
                        if (!removeComments)
                        {
                            sb.Append("*/");
                        }
                        i += 2;
                        break;
                    }
                    else if (!removeCommentContent)
                    {
                        sb.Append(char.ConvertFromUtf32(codepoint));
                    }
                    ++i;
                }

                continue;
            }

            bool appendChar = true;
            bool appendUnderscore = false;
            switch (contentProcessing)
            {
            case ContentProcessingOptions.KeepContent:
            default:
                break;

            case ContentProcessingOptions.ObscureAlphanumeric:
                appendUnderscore = IsAlphanumericChar(codepoint);
                break;

            case ContentProcessingOptions.ObscureContent:
                appendUnderscore = !IsWhitespace(codepoint);
                break;

            case ContentProcessingOptions.ObscureAndShortenAlphanumeric:
                if (IsAlphanumericChar(codepoint))
                {
                    appendUnderscore = true;
                    appendChar
                        = i == 0 || !IsAlphanumericChar(codepoints[i - 1]);
                }
                break;

            case ContentProcessingOptions.ObscureAndShortenContent:
                if (!IsWhitespace(codepoint))
                {
                    appendUnderscore = true;
                    appendChar = !previousWasContent;
                }
                break;

            case ContentProcessingOptions.RemoveAlphanumeric:
                appendChar = !IsAlphanumericChar(codepoint);
                break;

            case ContentProcessingOptions.RemoveContent:
                appendChar = IsWhitespace(codepoint);
                break;
            }

            if (appendChar)
            {
                sb.Append(
                    appendUnderscore ? '_' : char.ConvertFromUtf32(codepoint)
                );
            }

            previousWasContent = !IsWhitespace(codepoint);
            ++i;
        }

        code = sb.ToString();

        return code;
    }

    private IEnumerable<int> GetCodepoints(string s)
    {
        for (int i = 0; i < s.Length;)
        {
            int codepoint = char.ConvertToUtf32(s, i);
            yield return codepoint;
            i += char.IsHighSurrogate(s[i]) ? 2 : 1;
        }
    }

    // The following methods are probably not 100% correct

    private bool IsAlphanumericChar(int codepoint)
    {
        // Includes underscores as well

        string s = char.ConvertFromUtf32(codepoint);
        if (s.Length != 1)
        {
            return false;
        }

        return s[0] == '_' || char.IsLetterOrDigit(s[0]);
    }

    private bool IsDigit(int codepoint)
    {
        string s = char.ConvertFromUtf32(codepoint);
        if (s.Length != 1)
        {
            return false;
        }

        return char.IsAsciiDigit(s[0]);
    }

    private bool IsWhitespace(int codepoint)
    {
        string s = char.ConvertFromUtf32(codepoint);
        if (s.Length != 1)
        {
            return false;
        }

        return char.IsWhiteSpace(s[0]);
    }
}
