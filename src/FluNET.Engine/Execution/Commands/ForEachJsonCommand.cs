using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Prompt.Surface;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public interface IForEachJsonAction
{
    ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken);
}

public sealed class ForEachSayAction(IExpression<string> expression, ITextOutput output) : IForEachJsonAction
{
    public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken)
    {
        string message = expression.Evaluate(variables);
        await output.WriteLineAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record ForEachJsonCommand(
    IExpression<JsonElement[]> Source,
    string ItemName,
    int MaxConcurrency,
    IReadOnlyList<IForEachJsonAction> Actions) : ICommand<JsonElement[]>;

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
        List<IForEachJsonAction> actions = [];
        foreach (SurfaceIterationActionDescriptor action in descriptor.Actions)
        {
            if (!action.Kind.Equals("SAY", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"FOR EACH action '{action.Kind}' is not supported.");
            string text = Unquote(action.Source.Trim());
            IExpression<string> expression;
            if (!InterpolatedTextExpression.TryCreate(text, language, values, out IExpression<string>? interpolated))
                expression = new LiteralExpression<string>(text);
            else
                expression = interpolated!;
            actions.Add(new ForEachSayAction(expression, output));
        }
        return new ForEachJsonCommand(
            context.Require<JsonElement[]>(SemanticRole.Source),
            descriptor.ItemName,
            descriptor.MaxConcurrency,
            actions);
    }

    private static string Unwrap(string value) =>
        value.Length >= 2 && value[0] == '{' && value[^1] == '}' ? value[1..^1] : value;

    private static string Unquote(string value) =>
        value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;
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
                IVariableResolver iteration = new IterationVariableResolver(parent, command.ItemName, item.Clone());
                foreach (IForEachJsonAction action in command.Actions)
                    await action.ExecuteAsync(iteration, cancellationToken).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return source.Select(item => item.Clone()).ToArray();
    }

    private sealed class IterationVariableResolver(
        IVariableResolver parent,
        string itemName,
        JsonElement item) : IVariableResolver
    {
        public void Register<T>(string name, T value)
        {
            if (Normalize(name).Equals(itemName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Iteration variable '{itemName}' is read-only.");
            parent.Register(name, value);
        }

        public bool IsRegistered(string name) =>
            Normalize(name).Equals(itemName, StringComparison.OrdinalIgnoreCase) || parent.IsRegistered(name);

        public T? Resolve<T>(string tokenValue)
        {
            if (Normalize(tokenValue).Equals(itemName, StringComparison.OrdinalIgnoreCase))
            {
                object value = item;
                if (value is T typed) return typed;
                if (typeof(T) == typeof(object)) return (T)value;
                return default;
            }
            return parent.Resolve<T>(tokenValue);
        }

        public void Clear() => throw new InvalidOperationException("An iteration scope cannot clear its parent resolver.");

        public IEnumerable<string> GetVariableNames() =>
            parent.GetVariableNames().Append(itemName).Distinct(StringComparer.OrdinalIgnoreCase);

        private static string Normalize(string name) => name.Trim().TrimStart('[').TrimEnd(']');
    }
}
