using System.Threading;

namespace CheatingDetector;

internal class Program
{
    public const int SIZE_THRESHOLD = 50;
    public const double SUSPICIOUS_SIMILARITY_THRESHOLD = 0.35;
    public const double SUSPICIOUS_COMPRESSION_RATIO_THRESHOLD = 1.4;

    private static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine(
                "Please provide a directory containing the submission repos."
            );
            return;
        }

        string assignmentDir = args[0];
        string assignmentName = new DirectoryInfo(assignmentDir).Name;

        List<SubmissionData> submissions = new();
        List<SubmissionData> invalidSubmissions = new();
        foreach (
            Submission submission
            in AssignmentSubmissions.GetSubmissions(assignmentDir)
        )
        {
            SubmissionData submissionData = new(submission);
            if (submissionData.UncompressedSize < SIZE_THRESHOLD)
            {
                invalidSubmissions.Add(submissionData);
                continue;
            }

            submissions.Add(submissionData);
        }

        SimilarityCalculator similarityCalculator = new();

        foreach (SubmissionData submissionData in submissions)
        {
            submissionData.CompressedData
                = similarityCalculator.CompressString(
                    submissionData.Submission.MinifiedSourceCode
                );
        }

        List<(int i, int j)> pairIndexes = new();
        for (int i = 0; i < submissions.Count - 1; ++i)
        {
            for (int j = i + 1; j < submissions.Count; ++j)
            {
                pairIndexes.Add((i, j));
            }
        }

        int taskCount = Environment.ProcessorCount;
        Task[] tasks = new Task[taskCount];
        List<SubmissionPair>[] taskResults
            = new List<SubmissionPair>[taskCount];

        for (int i = 0; i < taskCount; ++i)
        {
            taskResults[i] = new();
        }

        for (int i = 0; i < taskCount; ++i)
        {
            int taskIdx = i;
            tasks[taskIdx] = new Task(()
            => {
                int begin = taskIdx * pairIndexes.Count / taskCount;
                int end = (taskIdx + 1) * pairIndexes.Count / taskCount;
                var result = taskResults[taskIdx];

                for (int i = begin; i < end; ++i)
                {
                    (int i, int j) pair = pairIndexes[i];
                    SubmissionData submissionData1 = submissions[pair.i];
                    SubmissionData submissionData2 = submissions[pair.j];
                    double similarity
                        = similarityCalculator.CalculateSimilarity(
                            submissionData1.Submission.MinifiedSourceCode,
                            submissionData2.Submission.MinifiedSourceCode,
                            submissionData1.CompressedSize,
                            submissionData2.CompressedSize
                        );

                    result.Add(
                        new(
                            submissionData1.Submission.Username,
                            submissionData2.Submission.Username,
                            similarity
                        )
                    );
                }
            });
            tasks[taskIdx].Start();
        }

        foreach (Task task in tasks)
        {
            task.Wait();
        }

        List<SubmissionPair> submissionPairs = new();
        foreach (List<SubmissionPair> taskResult in taskResults)
        {
            submissionPairs.AddRange(taskResult);
        }

        // Reverse sort by similarity
        submissionPairs.Sort(
            (SubmissionPair lhs, SubmissionPair rhs)
            => {
                if (lhs.Similarity < rhs.Similarity)
                {
                    return 1;
                }
                else if (rhs.Similarity < lhs.Similarity)
                {
                    return -1;
                }
                else
                {
                    return 0;
                }
            }
        );

        // Sort by compression ratio
        submissions.Sort(
            (SubmissionData lhs, SubmissionData rhs)
            => {
                if (lhs.CompressionRatio < rhs.CompressionRatio)
                {
                    return -1;
                }
                else if (rhs.CompressionRatio < lhs.CompressionRatio)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        );

        SaveSubmissionData(
            Path.Join(args[0], "submissions.csv"),
            submissions,
            invalidSubmissions
        );
        SaveSubmissionPairData(
            Path.Join(args[0], "submission_pairs.csv"),
            submissionPairs
        );

        PrintSummary(
            assignmentName,
            submissions,
            invalidSubmissions,
            submissionPairs
        );
    }

    private static void SaveSubmissionData(
        string filename,
        IEnumerable<SubmissionData> submissions,
        IEnumerable<SubmissionData> invalidSubmissions
    )
    {
        using StreamWriter writer = new(filename);

        writer.WriteLine(
            "Username,Compression Ratio,Pseudo-Minified Code Size,Compressed Size"
        );

        foreach (SubmissionData submissionData in submissions)
        {
            string username = submissionData.Submission.Username;
            double compressionRatio = submissionData.CompressionRatio;
            long minifiedSize = submissionData.UncompressedSize;
            long compressedSize = submissionData.CompressedSize;
            writer.WriteLine(
                $"{username},{compressionRatio},{minifiedSize},{compressedSize}"
            );
        }

        foreach (SubmissionData invalidSubmission in invalidSubmissions)
        {
            string username = invalidSubmission.Submission.Username;
            writer.WriteLine(
                $"{username},---,---,---"
            );
        }
    }

    private static void SaveSubmissionPairData(
        string filename,
        IEnumerable<SubmissionPair> submissionPairs
    )
    {
        using StreamWriter writer = new(filename);

        writer.WriteLine("Username 1,Username 2,Similarity");

        foreach (SubmissionPair submissionPair in submissionPairs)
        {
            string username1 = submissionPair.Username1;
            string username2 = submissionPair.Username2;
            double similarity = submissionPair.Similarity;
            writer.WriteLine($"{username1},{username2},{similarity}");
        }
    }

    private static void PrintSummary(
        string assignmentName,
        IList<SubmissionData> submissions,
        IList<SubmissionData> invalidSubmissions,
        IList<SubmissionPair> submissionPairs
    )
    {
        Console.WriteLine($"Summary for {assignmentName}");
        Console.WriteLine();

        int totalSubmissionCount
            = submissions.Count + invalidSubmissions.Count;
        Console.WriteLine($"Total Submissions: {totalSubmissionCount}");
        Console.WriteLine($"Analyzed Submissions: {submissions.Count}");
        Console.WriteLine(
            $"Unanalyzed Submissions: {invalidSubmissions.Count}"
        );

        if (invalidSubmissions.Count > 0)
        {
            Console.WriteLine(
                "  The following submissions were too short to analyze"
            );
            Console.Write("  or unable to be decoded:");

            int lineLength = 0;
            foreach (SubmissionData submissionData in invalidSubmissions)
            {
                string username
                    = PadUsername(submissionData.Submission.Username);

                if (lineLength == 0)
                {
                    Console.WriteLine();
                    Console.Write($"    {username}");
                }
                else
                {
                    Console.Write($" {username}");
                }

                ++lineLength;
                if (lineLength == 10)
                {
                    lineLength = 0;
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine();

        int countMatching;

        Console.WriteLine(
            $"Suspicious Submission Pairs (Similarity > {SUSPICIOUS_SIMILARITY_THRESHOLD})"
        );
        countMatching = 0;
        foreach (SubmissionPair submissionPair in submissionPairs)
        {
            double similarity = submissionPair.Similarity;
            if (similarity <= SUSPICIOUS_SIMILARITY_THRESHOLD)
            {
                continue;
            }

            ++countMatching;

            string username1 = PadUsername(submissionPair.Username1);
            string username2 = PadUsername(submissionPair.Username2);
            Console.WriteLine(
                $"  ( {username1} {username2} ) : {similarity.ToString("F4")}"
            );
        }

        if (countMatching == 0)
        {
            Console.WriteLine("  None Found");
        }

        Console.WriteLine();

        Console.WriteLine(
            $"Suspicious Submissions (Compression Ratio < {SUSPICIOUS_COMPRESSION_RATIO_THRESHOLD})"
        );
        countMatching = 0;
        foreach (SubmissionData submissionData in submissions)
        {
            double compressionRatio = submissionData.CompressionRatio;
            if (compressionRatio >= SUSPICIOUS_COMPRESSION_RATIO_THRESHOLD)
            {
                continue;
            }

            ++countMatching;

            string username = PadUsername(submissionData.Submission.Username);
            Console.WriteLine(
                $"  {username} : {compressionRatio.ToString("F4")}"
            );
        }

        if (countMatching == 0)
        {
            Console.WriteLine("  None Found");
        }

        Console.WriteLine();
    }

    private static string PadUsername(string username)
        => username.PadLeft(6, ' ');
}

public record struct SubmissionPair(
    string Username1, string Username2, double Similarity
);
