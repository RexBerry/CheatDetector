using EasyCompressor;
using System.Text;

namespace CheatingDetector;

public interface ISimilarityCalculator
{
    public double CalculateSimilarity(string s1, string s2);

    public double CalculateSimilarity(
        string s1, string s2, long length1, long length2
    );

    public byte[] CompressString(string s);
}
