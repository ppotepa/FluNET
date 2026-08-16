using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record ScanFilesJsonCommand(IExpression<string> Pattern, IExpression<int>? Limit = null) : ICommand<JsonElement[]>;

public sealed class ScanFilesJsonCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<ScanFilesJsonCommand, JsonElement[]>
{
    public ScanFilesJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.files.scan.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new ScanFilesJsonCommand(
            context.RequireText(SemanticRole.Source),
            context.Optional<int>(new FrameRoleId("Limit")));
    }
}

public sealed class ScanFilesJsonCommandHandler(
    IFluNetFileEnumerator files,
    IVariableResolver variables) : ICommandHandler<ScanFilesJsonCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(
        ScanFilesJsonCommand command,
        CancellationToken cancellationToken = default)
    {
        string pattern = command.Pattern.Evaluate(variables);
        bool recursive = pattern.StartsWith("__flunet_recursive__:", StringComparison.Ordinal) ||
            pattern.EndsWith("/**", StringComparison.Ordinal) ||
            pattern.EndsWith("/**/*", StringComparison.Ordinal);
        if (pattern.StartsWith("__flunet_recursive__:", StringComparison.Ordinal))
            pattern = pattern["__flunet_recursive__:".Length..];
        if (pattern.EndsWith("/**/*", StringComparison.Ordinal))
            pattern = pattern[..^5];
        else if (pattern.EndsWith("/**", StringComparison.Ordinal))
            pattern = pattern[..^3];

        string scanRoot = Path.GetDirectoryName(Path.GetFullPath(pattern)) ?? Directory.GetCurrentDirectory();
        if (Directory.Exists(pattern))
        {
            scanRoot = Path.GetFullPath(pattern);
            pattern = Path.Combine(pattern, "*");
            recursive = true;
        }

        int limit = command.Limit?.Evaluate(variables) ?? 0;
        if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit), "SCAN LIMIT cannot be negative.");
        IReadOnlyList<string> paths = await files.EnumerateFilesAsync(
            pattern,
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly,
            limit,
            cancellationToken);
        return paths.Select(path =>
        {
            FileInfo info = new(path);
            bool hidden = info.Name.StartsWith(".", StringComparison.Ordinal) ||
                info.Attributes.HasFlag(FileAttributes.Hidden);
            return JsonSerializer.SerializeToElement(new
            {
                path = info.FullName,
                name = info.Name,
                nameWithoutExtension = Path.GetFileNameWithoutExtension(info.Name),
                extension = info.Extension,
                directory = info.DirectoryName,
                relativePath = Path.GetRelativePath(scanRoot, info.FullName),
                length = info.Length,
                createdUtc = info.CreationTimeUtc,
                modifiedUtc = info.LastWriteTimeUtc,
                accessedUtc = info.LastAccessTimeUtc,
                isHidden = hidden,
                isReadOnly = info.IsReadOnly
            });
        }).ToArray();
    }
}
