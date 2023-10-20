namespace CheatingDetector;

internal class Program
{
    public static int SizeThreshold => 250;
    public static int SubmissionItemPairsToDisplay => 20;
    public static int SubmissionItemsToDisplay => 5;

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

        List<SubmissionItem> submissionItems = new();
        List<SubmissionItem> invalidSubmissionItems = new();
        foreach (
            SubmissionItem submissionItem
            in AssignmentSubmissions.GetSubmissionItems(assignmentDir)
        )
        {
            if (submissionItem.UncompressedSize < SizeThreshold)
            {
                invalidSubmissionItems.Add(submissionItem);
                continue;
            }

            submissionItems.Add(submissionItem);
        }

        Dictionary<string, int> submissionItemNameIndexes = new();
        for (int i = 0; i < submissionItems.Count; ++i)
        {
            submissionItemNameIndexes[submissionItems[i].Name] = i;
        }

        SimilarityCalculator similarityCalculator = new();
        foreach (SubmissionItem submissionItem in submissionItems)
        {
            submissionItem.CompressedData
                = similarityCalculator.CompressString(
                    submissionItem.Code
                );
        }

        List<(int i, int j)> pairIndexes = new();
        for (int i = 0; i < submissionItems.Count - 1; ++i)
        {
            for (int j = i + 1; j < submissionItems.Count; ++j)
            {
                pairIndexes.Add((i, j));
            }
        }

        int taskCount = Environment.ProcessorCount;
        Task[] tasks = new Task[taskCount];
        List<SubmissionItemPair>[] taskResults
            = new List<SubmissionItemPair>[taskCount];

        for (int i = 0; i < taskCount; ++i)
        {
            taskResults[i] = new();
        }

        const string CLEAR_LINE
            = "\r                                                  \r";

        const int CHUNK_SIZE = 4;
        int chunkIdx = 0;
        int pairsComplete = 0;
        for (int i = 0; i < taskCount; ++i)
        {
            int taskIdx = i;
            tasks[taskIdx] = new Task(()
            =>
            {
                List<SubmissionItemPair> result = taskResults[taskIdx];
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

                    if (pairsComplete % (taskCount * CHUNK_SIZE) == 0)
                    {
                        double pctComplete
                            = (double)pairsComplete / pairIndexes.Count
                            * 100.0;
                        Console.Write(
                            CLEAR_LINE
                            + $"Checked {pairsComplete}/{pairIndexes.Count}"
                            + $" Submission Pairs"
                            + $" ({pctComplete.ToString("F1")}%)"
                        );
                        Console.Out.Flush();
                    }

                    int end = Math.Min(begin + CHUNK_SIZE, pairIndexes.Count);

                    for (int i = begin; i < end; ++i)
                    {
                        (int i, int j) pair = pairIndexes[i];
                        SubmissionItem submissionItem1
                            = submissionItems[pair.i];
                        SubmissionItem submissionItem2
                            = submissionItems[pair.j];
                        double similarity
                            = similarityCalculator.CalculateSimilarity(
                                submissionItem1.Code,
                                submissionItem2.Code,
                                submissionItem1.CompressedSize,
                                submissionItem2.CompressedSize
                            );

                        result.Add(
                            new(
                                submissionItem1.Name,
                                submissionItem2.Name,
                                similarity
                            )
                        );
                    }

                    Interlocked.Add(ref pairsComplete, end - begin);
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

        List<SubmissionItemPair> submissionItemPairs = new();
        foreach (List<SubmissionItemPair> taskResult in taskResults)
        {
            submissionItemPairs.AddRange(taskResult);
        }

        foreach (SubmissionItemPair submissionItemPair in submissionItemPairs)
        {
            double similarity = submissionItemPair.Similarity;
            submissionItems[
                submissionItemNameIndexes[submissionItemPair.ItemName1]
            ].AddSimilarityScore(similarity);
            submissionItems[
                submissionItemNameIndexes[submissionItemPair.ItemName2]
            ].AddSimilarityScore(similarity);
        }

        // Reverse sort by similarity
        submissionItemPairs.Sort(
            (SubmissionItemPair lhs, SubmissionItemPair rhs)
            =>
            {
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

        // Reverse sort by compression ratio
        submissionItems.Sort(
            (SubmissionItem lhs, SubmissionItem rhs)
            =>
            {
                var lhsValue = lhs.CompressionRatio;
                var rhsValue = rhs.CompressionRatio;

                if (lhsValue < rhsValue)
                {
                    return 1;
                }
                else if (rhsValue < lhsValue)
                {
                    return -1;
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
            submissionItems,
            invalidSubmissionItems,
            submissionItemPairs
        );

        SaveSubmissionItemData(
            Path.Join(args[0], "submission_items.csv"),
            submissionItems,
            invalidSubmissionItems
        );
        SaveSubmissionItemPairData(
            Path.Join(args[0], "submission_item_pairs.csv"),
            submissionItemPairs
        );
    }

    private static void SaveSubmissionItemData(
        string filename,
        IEnumerable<SubmissionItem> submissionItems,
        IEnumerable<SubmissionItem> invalidSubmissionItems
    )
    {
        using StreamWriter writer = new(filename);

        writer.WriteLine(
            "Username,Highest Similarity,Compression Ratio"
            + ",Pseudo-Minified Code Size,Compressed Size"
        );

        foreach (SubmissionItem submissionItem in submissionItems)
        {
            string itemName = submissionItem.Name;
            double highestSimilarity = submissionItem.HighestSimilarity;
            double compressionRatio = submissionItem.CompressionRatio;
            long minifiedSize = submissionItem.UncompressedSize;
            long compressedSize = submissionItem.CompressedSize;
            writer.WriteLine(
                $"{itemName},{highestSimilarity},{compressionRatio}"
                + $",{minifiedSize},{compressedSize}"
            );
        }

        foreach (SubmissionItem submissionItem in invalidSubmissionItems)
        {
            string itemName = submissionItem.Name;
            writer.WriteLine(
                $"{itemName},---,---,---,---"
            );
        }
    }

    private static void SaveSubmissionItemPairData(
        string filename,
        IEnumerable<SubmissionItemPair> submissionItemPairs
    )
    {
        using StreamWriter writer = new(filename);

        writer.WriteLine("Username 1,Username 2,Similarity");

        foreach (SubmissionItemPair submissionItemPair in submissionItemPairs)
        {
            string itemName1 = submissionItemPair.ItemName1;
            string itemName2 = submissionItemPair.ItemName2;
            double similarity = submissionItemPair.Similarity;
            writer.WriteLine($"{itemName1},{itemName2},{similarity}");
        }
    }

    private static void PrintSummary(
        string assignmentName,
        IList<SubmissionItem> submissionItems,
        IList<SubmissionItem> invalidSubmissionItems,
        IList<SubmissionItemPair> submissionItemPairs
    )
    {
        Console.WriteLine($"Summary for {assignmentName}");
        Console.WriteLine();

        int totalSubmissionItemCount
            = submissionItems.Count + invalidSubmissionItems.Count;
        Console.WriteLine(
            $"Total Submission Items: {totalSubmissionItemCount}"
        );
        Console.WriteLine(
            $"Submissions Items Checked: {submissionItems.Count}"
        );
        Console.WriteLine(
            $"Submissions Items Skipped: {invalidSubmissionItems.Count}"
        );

        // TODO: Update padding to work with strings other than usernames

        if (invalidSubmissionItems.Count > 0)
        {
            Console.WriteLine(
                "  The following submission items were too short to"
            );
            Console.Write(
                "  check for similarity or were unable to be decoded:"
            );

            int lineLength = 0;
            foreach (SubmissionItem submissionItem in invalidSubmissionItems)
            {
                string itemName
                    = PadUsername(submissionItem.Name);

                if (lineLength == 0)
                {
                    Console.WriteLine();
                    Console.Write($"    {itemName}");
                }
                else
                {
                    Console.Write($" {itemName}");
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
            $"Submission Item Pairs by Highest Similarity"
        );
        foreach (
            SubmissionItemPair submissionItemPair
            in submissionItemPairs.Take(SubmissionItemPairsToDisplay)
        )
        {
            string itemName1 = PadUsername(submissionItemPair.ItemName1);
            string itemName2 = PadUsername(submissionItemPair.ItemName2);
            double similarity = submissionItemPair.Similarity;
            Console.WriteLine(
                $"  ( {itemName1} {itemName2} ) : {similarity.ToString("F4")}"
            );
        }
        Console.WriteLine();

        Console.WriteLine(
            $"Submission Items by Highest Compression Ratio"
        );
        foreach (
            SubmissionItem submissionItem
            in submissionItems.Take(SubmissionItemsToDisplay)
        )
        {
            string itemName = PadUsername(submissionItem.Name);
            double compressionRatio = submissionItem.CompressionRatio;
            Console.WriteLine(
                $"  {itemName} : {compressionRatio.ToString("F4")}"
            );
        }
        Console.WriteLine();
    }

    private static string PadUsername(string username)
        => username.PadLeft(6, ' ');
}

public record struct SubmissionItemPair(
    string ItemName1, string ItemName2, double Similarity
);
