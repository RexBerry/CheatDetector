using System.Collections.Immutable;

namespace CheatingDetector;

public static class SubmissionFiles
{
    public static ISet<string> SourceFileExtensions = new HashSet<string>();

    public static bool IsSourceFile(string filename)
        => SourceFileExtensions.Contains(
            Path.GetExtension(filename).ToLowerInvariant()
        );

    public static IEnumerable<string> GetSourceFiles(string directoryName)
    {
        Queue<string> dirs = new();
        dirs.Enqueue(directoryName);
        bool isTop = true;
        while (dirs.Count > 0)
        {
            string dirname = dirs.Dequeue();

            foreach (string filename in Directory.GetFiles(dirname).Order())
            {
                if (!IsSourceFile(filename))
                {
                    continue;
                }

                yield return filename;
            }

            foreach (
                string subdirname in Directory.GetDirectories(dirname).Order()
            )
            {
                if (isTop && subdirname == ".git")
                {
                    continue;
                }

                dirs.Enqueue(subdirname);
            }

            isTop = false;
        }
    }
}
