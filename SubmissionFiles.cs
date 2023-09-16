using System.Collections.Immutable;

namespace CheatingDetector;

public static class SubmissionFiles
{
    public static IReadOnlySet<string> SourceFileExtensions
        => _sourceFileExtensions;
    private static readonly IReadOnlySet<string> _sourceFileExtensions
        = new[]{ ".cpp", ".c", ".hpp", ".h", ".txt" }.ToImmutableHashSet();

    public static bool IsSourceFile(string filename)
        => SourceFileExtensions.Contains(
            Path.GetExtension(filename).ToLowerInvariant()
        );

    public static IEnumerable<string> GetSourceFiles(string directoryName)
    {
        Queue<string> dirs = new();
        dirs.Enqueue(directoryName);
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
                dirs.Enqueue(subdirname);
            }
        }
    }
}
