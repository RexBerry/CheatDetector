namespace CheatingDetector;

internal class Program
{
    public static int SizeThreshold => 250;
    public static int SubmissionPairsToDisplay => 20;
    public static int SubmissionsToDisplay => 5;

    private static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine(
                "Usage: CheatingDetector <directory> <file extensions>"
            );
            Console.WriteLine(
                "- <directory>: a directory whose subdirectories are the"
                + " submission Git repos"
            );
            Console.WriteLine(
                "- <file extensions>: a list of file extensions to analyze."
                + " Example: .cpp .c .hpp .h"
            );

            return;
        }

        string assignmentDir = args[0];
        string assignmentName = new DirectoryInfo(assignmentDir).Name;

        foreach (string fileExtension in args.Skip(1))
        {
            SubmissionFiles.SourceFileExtensions.Add(fileExtension);
        }

        List<SubmissionData> submissions = new();
        List<SubmissionData> invalidSubmissions = new();
        foreach (
            Submission submission
            in AssignmentSubmissions.GetSubmissions(assignmentDir)
        )
        {
            SubmissionData submissionData = new(submission);
            if (submissionData.UncompressedSize < SizeThreshold)
            {
                invalidSubmissions.Add(submissionData);
                continue;
            }

            submissions.Add(submissionData);
        }

        // Don't feel good about this but whatever
        Dictionary<string, int> usernameIndexes = new();
        for (int i = 0; i < submissions.Count; ++i)
        {
            usernameIndexes[submissions[i].Submission.Username] = i;
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

        const string CLEAR_LINE
            = "\r                                                  \r";

        const int CHUNK_SIZE = 4;
        int chunkIdx = 0;
        int itemsComplete = 0;
        for (int i = 0; i < taskCount; ++i)
        {
            int taskIdx = i;
            tasks[taskIdx] = new Task(()
            => {
                List<SubmissionPair> result = taskResults[taskIdx];
                SimilarityCalculator similarityCalculator = new();
                while (true)
                {
                    int begin
                        = Interlocked.Add(ref chunkIdx, CHUNK_SIZE)
                        - CHUNK_SIZE;
                    if (begin >= pairIndexes.Count)
                    {
                        break;
                    }

                    if (itemsComplete % (taskCount * CHUNK_SIZE) == 0)
                    {
                        double pctComplete
                            = (double)itemsComplete / pairIndexes.Count * 100.0;
                        Console.Write(
                            CLEAR_LINE
                            + $"Checked {itemsComplete}/{pairIndexes.Count}"
                            + $" Submission Pairs"
                            + $" ({pctComplete.ToString("F1")}%)"
                        );
                        Console.Out.Flush();
                    }

                    int end = Math.Min(begin + CHUNK_SIZE, pairIndexes.Count);

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

                    Interlocked.Add(ref itemsComplete, end - begin);
                }
            });
            tasks[taskIdx].Start();
        }

        foreach (Task task in tasks)
        {
            task.Wait();
        }

        Console.Write(CLEAR_LINE);
        Console.Out.Flush();

        List<SubmissionPair> submissionPairs = new();
        foreach (List<SubmissionPair> taskResult in taskResults)
        {
            submissionPairs.AddRange(taskResult);
        }

        foreach (SubmissionPair submissionPair in submissionPairs)
        {
            double similarity = submissionPair.Similarity;
            submissions[usernameIndexes[submissionPair.Username1]]
                .AddSimilarityScore(similarity);
            submissions[usernameIndexes[submissionPair.Username2]]
                .AddSimilarityScore(similarity);
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
                var lhsValue = lhs.CompressionRatio;
                var rhsValue = rhs.CompressionRatio;

                if (lhsValue < rhsValue)
                {
                    return -1;
                }
                else if (rhsValue < lhsValue)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        );

        Console.WriteLine();
        PrintSummary(
            assignmentName,
            submissions,
            invalidSubmissions,
            submissionPairs
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
    }

    private static void SaveSubmissionData(
        string filename,
        IEnumerable<SubmissionData> submissions,
        IEnumerable<SubmissionData> invalidSubmissions
    )
    {
        using StreamWriter writer = new(filename);

        writer.WriteLine(
            "Username,Highest Similarity,Compression Ratio"
            + ",Pseudo-Minified Code Size,Compressed Size"
        );

        foreach (SubmissionData submissionData in submissions)
        {
            string username = submissionData.Submission.Username;
            double highestSimilarity = submissionData.HighestSimilarity;
            double compressionRatio = submissionData.CompressionRatio;
            long minifiedSize = submissionData.UncompressedSize;
            long compressedSize = submissionData.CompressedSize;
            writer.WriteLine(
                $"{username},{highestSimilarity},{compressionRatio}"
                + $",{minifiedSize},{compressedSize}"
            );
        }

        foreach (SubmissionData invalidSubmission in invalidSubmissions)
        {
            string username = invalidSubmission.Submission.Username;
            writer.WriteLine(
                $"{username},---,---,---,---"
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
        Console.WriteLine($"Submissions Checked: {submissions.Count}");
        Console.WriteLine(
            $"Submissions Skipped: {invalidSubmissions.Count}"
        );

        if (invalidSubmissions.Count > 0)
        {
            Console.WriteLine(
                "  The following submissions were too short to check"
            );
            Console.Write("  for similarity or were unable to be decoded:");

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

        Console.WriteLine(
            $"Submission Pairs by Highest Similarity"
        );
        foreach (
            SubmissionPair submissionPair
            in submissionPairs.Take(SubmissionPairsToDisplay)
        )
        {
            string username1 = PadUsername(submissionPair.Username1);
            string username2 = PadUsername(submissionPair.Username2);
            double similarity = submissionPair.Similarity;
            Console.WriteLine(
                $"  ( {username1} {username2} ) : {similarity.ToString("F4")}"
            );
        }
        Console.WriteLine();

        Console.WriteLine(
            $"Submissions by Lowest Compression Ratio"
        );
        foreach (
            SubmissionData submissionData
            in submissions.Take(SubmissionsToDisplay)
        )
        {
            string username = PadUsername(submissionData.Submission.Username);
            double compressionRatio = submissionData.CompressionRatio;
            Console.WriteLine(
                $"  {username} : {compressionRatio.ToString("F4")}"
            );
        }
        Console.WriteLine();
    }

    private static string PadUsername(string username)
        => username.PadLeft(6, ' ');
}

public record struct SubmissionPair(
    string Username1, string Username2, double Similarity
);
