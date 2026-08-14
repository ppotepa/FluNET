using FluNET.Execution.Actions;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Prompt.Surface;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record ForEachJsonCommand(
    IExpression<JsonElement[]> Source,
    string ItemName,
    int MaxConcurrency,
    CompiledActionTemplate Template) : ICommand<JsonElement[]>;

public sealed class ForEachJsonCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values,
    ITextOutput output) : ICommandBinder<ForEachJsonCommand, JsonElement[]>
{
    public ForEachJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.flow.foreach.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        BoundArgument template = command[new FrameRoleId("Template")];
        string encoded = string.Join("", template.Tokens.Select(token => Unwrap(token.Text)));
        SurfaceForEachDescriptor descriptor = SurfaceForEachDescriptor.Decode(encoded);
        List<ICompiledAction> actions = [];
        foreach (SurfaceIterationActionDescriptor action in descriptor.Actions)
        {
            if (!action.Kind.Equals("SAY", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"FOR EACH action '{action.Kind}' is not supported by this compiler pass yet.");
            string text = Unquote(action.Source.Trim());
            IExpression<string> expression = InterpolatedTextExpression.TryCreate(text, language, values, out IExpression<string>? interpolated)
                ? interpolated!
                : new LiteralExpression<string>(text);
            actions.Add(new CompiledSayAction(expression, output));
        }
        return new ForEachJsonCommand(context.Require<JsonElement[]>(SemanticRole.Source), descriptor.ItemName, descriptor.MaxConcurrency, new CompiledActionTemplate(actions));
    }

    private static string Unwrap(string value) => value.Length >= 2 && value[0] == '{' && value[^1] == '}' ? value[1..^1] : value;
    private static string Unquote(string value) => value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')) ? value[1..^1] : value;
}

public sealed class ForEachJsonCommandHandler(IVariableResolver parent) : ICommandHandler<ForEachJsonCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(ForEachJsonCommand command, CancellationToken cancellationToken = default)
    {
        JsonElement[] source = command.Source.Evaluate(parent);
        using SemaphoreSlim gate = new(command.MaxConcurrency, command.MaxConcurrency);
        Task[] tasks = source.Select(async item =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ActionScopeVariableResolver iteration = new(parent,
                    [new KeyValuePair<string, object?>(command.ItemName, item.Clone())],
                    [command.ItemName]);
                await command.Template.ExecuteAsync(iteration, cancellationToken).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return source.Select(item => item.Clone()).ToArray();
    }
}
