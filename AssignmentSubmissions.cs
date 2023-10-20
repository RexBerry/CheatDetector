using System.Text;

namespace CheatingDetector;

public static class AssignmentSubmissions
{
    public static IEnumerable<SubmissionItem> GetSubmissionItems(
        string directoryName
    )
    {
        CppMinifier minifier = new();

        foreach (
            string submissionDirName
            in Directory.GetDirectories(directoryName).Order()
        )
        {
            string username = new DirectoryInfo(submissionDirName).Name;

            StringBuilder minifiedSourceCode = new();
            foreach (
                string filename
                in SubmissionFiles.GetSourceFiles(submissionDirName)
            )
            {
                using StreamReader reader = new(filename, true);
                string fileText = reader.ReadToEnd();
                string minified;
                try
                {
                    minified = minifier.Minify(fileText);
                }
                catch (ArgumentException)
                {
                    // fileText doesn't contain valid Unicode
                    Console.WriteLine($"Warning: Unable to decode {filename}");
                    minified = string.Empty;
                }

                minifiedSourceCode.Append(minified);
                minifiedSourceCode.Append('\n');
            }

            yield return new SubmissionItem(
                username, username, minifiedSourceCode.ToString()
            );
        }
    }
}
