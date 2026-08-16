using System.Text.Json;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record StatPathCommand(IExpression<string> Path) : ICommand<JsonElement>;

public sealed class StatPathCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<StatPathCommand, JsonElement>
{
    public StatPathCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.files.stat")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source));
    }
}

public sealed class StatPathCommandHandler(
    IFluNetDirectoryOperations directories,
    IVariableResolver variables) : ICommandHandler<StatPathCommand, JsonElement>
{
    public async ValueTask<JsonElement> HandleAsync(
        StatPathCommand command,
        CancellationToken cancellationToken = default)
    {
        FluNetPathInfo info = await directories.StatAsync(
            command.Path.Evaluate(variables), cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(new
        {
            path = info.Path,
            name = info.Name,
            exists = info.Exists,
            isDirectory = info.IsDirectory,
            length = info.Length,
            createdUtc = info.CreatedUtc,
            modifiedUtc = info.ModifiedUtc
        });
    }
}
