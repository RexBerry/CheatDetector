using System.Text;

namespace CheatingDetector;

public static class AssignmentSubmissions
{
    public static IEnumerable<SubmissionItem> GetSubmissionItems(
        string directoryName
    )
    {
        CodeProcessor processor = new();
        using StreamWriter writer = new("processed_code.cpp");

        foreach (
            string submissionDirName
            in Directory.GetDirectories(directoryName).Order()
        )
        {
            string username = new DirectoryInfo(submissionDirName).Name;

            StringBuilder processedSourceCode = new();
            foreach (
                string filename
                in SubmissionFiles.GetSourceFiles(submissionDirName)
            )
            {
                using StreamReader reader = new(filename, true);
                string fileText = reader.ReadToEnd();
                string processed;
                try
                {
                    processed = processor.Process(fileText);
                }
                catch (ArgumentException)
                {
                    // fileText doesn't contain valid Unicode
                    Console.WriteLine($"Warning: Unable to decode {filename}");
                    processed = string.Empty;
                }

                writer.WriteLine("/*--------------------------------------*/");
                writer.WriteLine();
                writer.Write(processed);
                writer.WriteLine();

                processedSourceCode.Append(processed);
                processedSourceCode.Append('\n');
            }

            yield return new SubmissionItem(
                username, username, processedSourceCode.ToString()
            );
        }
    }
}
