using EasyCompressor;
using System.Text;

namespace CheatingDetector;

public class SimilarityCalculator
{
    private readonly ICompressor _compressor = new LZMACompressor();

    public double CalculateSimilarity(string s1, string s2)
    {
        if (s1.Length == 0)
        {
            s1 = " ";
        }
        if (s2.Length == 0)
        {
            s2 = " ";
        }

        long length1 = CompressString(s1).Length;
        long length2 = CompressString(s2).Length;

        return CalculateSimilarity(s1, s2, length1, length2);
    }

    public double CalculateSimilarity(
        string s1, string s2, long length1, long length2
    )
    {
        if (s1.Length == 0)
        {
            s1 = " ";
        }
        if (s2.Length == 0)
        {
            s2 = " ";
        }

        long combinedLength1 = CompressString(s1 + s2).Length;
        long combinedLength2 = CompressString(s2 + s1).Length;
        double avgCombinedLength
            = ((double)combinedLength1 + combinedLength2) / 2.0;

        long minLength = Math.Min(length1, length2);
        long maxLength = Math.Max(length1, length2);

        // This is somewhat arbitrary
        double mult = Math.Sqrt((double)minLength / maxLength);

        return mult * InverseLerp(
            length1 + length2, maxLength, avgCombinedLength
        );
    }

    public byte[] CompressString(string s)
        => _compressor.Compress(Encoding.UTF8.GetBytes(s));

    private static double InverseLerp(double x, double y, double s)
        => (s - x) / (y - x);
}
