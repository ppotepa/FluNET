using System.Text.RegularExpressions;

namespace FluNET.Capabilities;

public sealed record FluNetFileSearchMatch(
    string Path,
    int Line,
    int Column,
    string Text);

public interface IFluNetFileSearcher
{
    ValueTask<IReadOnlyList<FluNetFileSearchMatch>> SearchAsync(
        string root,
        string query,
        bool recursive,
        bool regex,
        int maxMatches = 0,
        CancellationToken cancellationToken = default);
}

public sealed class PhysicalFluNetFileSearcher(IExecutionPolicy policy) : IFluNetFileSearcher
{
    public ValueTask<IReadOnlyList<FluNetFileSearchMatch>> SearchAsync(
        string root,
        string query,
        bool recursive,
        bool regex,
        int maxMatches = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (maxMatches < 0) throw new ArgumentOutOfRangeException(nameof(maxMatches));
        string fullRoot = Path.GetFullPath(root);
        policy.EnsureFileAccess(fullRoot);
        Regex? expression = regex
            ? new Regex(query, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)
            : null;
        IEnumerable<string> files = File.Exists(fullRoot)
            ? [fullRoot]
            : Directory.EnumerateFiles(fullRoot, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        List<FluNetFileSearchMatch> matches = [];
        foreach (string file in files.Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            policy.EnsureFileAccess(file);
            if (LooksBinary(file, cancellationToken)) continue;
            int lineNumber = 0;
            foreach (string line in File.ReadLines(file))
            {
                cancellationToken.ThrowIfCancellationRequested();
                lineNumber++;
                Match? match = expression?.Match(line);
                if (expression is not null)
                {
                    if (match is { Success: true })
                        matches.Add(new(file, lineNumber, match.Index + 1, line));
                }
                else
                {
                    int column = line.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                    if (column >= 0) matches.Add(new(file, lineNumber, column + 1, line));
                }
                if (maxMatches > 0 && matches.Count >= maxMatches)
                    return ValueTask.FromResult<IReadOnlyList<FluNetFileSearchMatch>>(matches);
            }
        }
        return ValueTask.FromResult<IReadOnlyList<FluNetFileSearchMatch>>(matches);
    }

    private static bool LooksBinary(string path, CancellationToken cancellationToken)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] buffer = new byte[4096];
        int read = stream.Read(buffer, 0, buffer.Length);
        cancellationToken.ThrowIfCancellationRequested();
        return Array.IndexOf(buffer, (byte)0, 0, read) >= 0;
    }
}

public sealed class FileSearchCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "filesystem.search",
        "1.0",
        [FluNetPlatform.Any],
        ["filesystem.read"]);

    public bool IsAvailable => true;
}
