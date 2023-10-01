using System.Text;

namespace CheatingDetector;

public class LevenshteinDistance
{
    public double CalculateSimilarity(string s1, string s2)
    {
        if (s1.Length == 0 && s2.Length == 0)
        {
            return 1.0;
        }

        return 1.0 - (
            (double)CalculateEditDistance(s1, s2)
            / Math.Max(s1.Length, s2.Length)
        );
    }


    public int CalculateEditDistance(string s1, string s2)
    {
        // https://en.wikipedia.org/wiki/Levenshtein_distance#Definition

        if (s1.Length == 0)
        {
            return s2.Length;
        }
        if (s2.Length == 0)
        {
            return s1.Length;
        }

        int[] prevRow = Enumerable.Range(0, s2.Length + 1).ToArray();
        int[] currRow = new int[prevRow.Length];

        for (int i = 1; i <= s1.Length; ++i)
        {
            currRow[0] = prevRow[0] + 1;

            for (int j = 1; j <= s2.Length; ++j)
            {
                ref int result = ref currRow[j];

                if (s1[^i] == s2[^j])
                {
                    result = prevRow[j - 1];
                }
                else
                {
                    result = 1 + Math.Min(
                        Math.Min(
                            prevRow[j],
                            currRow[j - 1]
                        ),
                        prevRow[j - 1]
                    );
                }
            }

            (prevRow, currRow) = (currRow, prevRow);
        }

        return prevRow[^1];
    }
}
