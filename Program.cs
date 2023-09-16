using System.Threading;

namespace CheatingDetector;

internal class Program
{
    public const int SIZE_THRESHOLD = 50;

    private static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine(
                "Please provide a directory containing the submission repos."
            );
            return;
        }

        List<SubmissionData> submissions = new();
        List<SubmissionData> invalidSubmissions = new();
        foreach (
            Submission submission
            in AssignmentSubmissions.GetSubmissions(args[0])
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
            tasks[taskIdx] = new Task(() => {
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
            (SubmissionPair lhs, SubmissionPair rhs) => {
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
            (SubmissionData lhs, SubmissionData rhs) => {
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

        using (
            StreamWriter writer = new(
                Path.Join(args[0], "submission_pairs.csv")
            )
        )
        {
            writer.WriteLine("Username 1,Username 2,Similarity");

            foreach (SubmissionPair submissionPair in submissionPairs)
            {
                string username1 = submissionPair.Name1;
                string username2 = submissionPair.Name2;
                double similarity = submissionPair.Similarity;
                writer.WriteLine($"{username1},{username2},{similarity}");
            }
        }

        using (
            StreamWriter writer = new(
                Path.Join(args[0], "submissions.csv")
            )
        )
        {
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
    }
}

public record struct SubmissionPair(
    string Name1, string Name2, double Similarity
);
