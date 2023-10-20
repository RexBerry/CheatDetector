using System.Text;

namespace CheatingDetector;

public class SubmissionItem
{
    public string Name { get; init; }
    public string AuthorName { get; init; }
    public string Code { get; init; }
    public byte[] UncompressedData { get; init; }
    public byte[] CompressedData { get; set; } = new byte[]{};
    public long UncompressedSize => UncompressedData.Length;
    public long CompressedSize => CompressedData.Length;
    public double CompressionRatio
        => (double)UncompressedSize / CompressedSize;
    public double HighestSimilarity { get; private set; } = 0.0;

    public SubmissionItem(string name, string authorName, string code)
    {
        Name = name;
        AuthorName = authorName;
        Code = code;
        UncompressedData = Encoding.UTF8.GetBytes(code);
    }

    public void AddSimilarityScore(double similarityScore)
    {
        if (similarityScore > HighestSimilarity)
        {
            HighestSimilarity = similarityScore;
        }
    }
}
