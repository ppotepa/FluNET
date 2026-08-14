using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

/// <summary>Generated canonical command used by compact JSON glob lowering.</summary>
public sealed record LoadJsonGlobCommand(IExpression<string> Pattern) : ICommand<JsonElement[]>;

public sealed class LoadJsonGlobCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<LoadJsonGlobCommand, JsonElement[]>
{
    public LoadJsonGlobCommand? TryBind(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Frame.Id != new FrameId("surface.load.glob.json"))
        {
            return null;
        }
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new LoadJsonGlobCommand(context.RequireText(SemanticRole.Source));
    }
}

public sealed class LoadJsonGlobCommandHandler(
    IVariableResolver variables,
    IExecutionPolicy policy,
    IFluNetFileSystem files) : ICommandHandler<LoadJsonGlobCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(
        LoadJsonGlobCommand command,
        CancellationToken cancellationToken = default)
    {
        string pattern = command.Pattern.Evaluate(variables);
        IFluNetFileEnumerator enumerator = new PhysicalFluNetFileEnumerator(policy);
        IReadOnlyList<string> paths = await enumerator
            .EnumerateFilesAsync(pattern, cancellationToken)
            .ConfigureAwait(false);
        List<JsonElement> result = new(paths.Count);
        foreach (string path in paths)
        {
            string json = await files.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            result.Add(document.RootElement.Clone());
        }
        return result.ToArray();
    }
}
