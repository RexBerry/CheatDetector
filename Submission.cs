using System.Text;

namespace CheatingDetector;

public record struct Submission(string Username, string MinifiedSourceCode);

public class SubmissionData
{
    public Submission Submission { get; set; }
    public byte[] UncompressedData { get; init; }
    public byte[] CompressedData { get; set; } = new byte[]{};
    public long UncompressedSize => UncompressedData.Length;
    public long CompressedSize => CompressedData.Length;
    public double CompressionRatio
        => (double)UncompressedSize / CompressedSize;
    public double HighestSimilarity { get; private set; } = 0.0;

    public SubmissionData(Submission submission)
    {
        Submission = submission;
        UncompressedData = Encoding.UTF8.GetBytes(
            Submission.MinifiedSourceCode
        );
    }

    public void AddSimilarityScore(double similarityScore)
    {
        if (similarityScore > HighestSimilarity)
        {
            HighestSimilarity = similarityScore;
        }
    }
}
