using System.Text;

namespace CheatingDetector;

public static class AssignmentSubmissions
{
    public static IEnumerable<Submission> GetSubmissions(string directoryName)
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
                string minified = minifier.Minify(fileText);

                minifiedSourceCode.Append(minified);
                minifiedSourceCode.Append('\n');
            }

            yield return new Submission(
                username, minifiedSourceCode.ToString()
            );
        }
    }
}
