using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record PutValueCommand(IExpression<string> Key, IExpression<string> Value) : ICommand<string>;
public sealed record ReadValueCommand(IExpression<string> Key) : ICommand<string>;
public sealed record ListValuesCommand(IExpression<string> Prefix) : ICommand<JsonElement[]>;
public sealed record DeleteValueCommand(IExpression<string> Key) : ICommand<string>;

public sealed class PutValueCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<PutValueCommand, string>
{
    public PutValueCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("storage.put.value")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new PutValueCommand(context.RequireText(SemanticRole.Source), context.RequireText(SemanticRole.Theme));
    }
}

public sealed class ReadValueCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<ReadValueCommand, string>
{
    public ReadValueCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("storage.read.value")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new ReadValueCommand(context.RequireText(SemanticRole.Source));
    }
}

public sealed class PutValueCommandHandler(IFluNetKeyValueStore store, IVariableResolver variables)
    : ICommandHandler<PutValueCommand, string>
{
    public async ValueTask<string> HandleAsync(PutValueCommand command, CancellationToken cancellationToken = default)
    {
        string key = command.Key.Evaluate(variables);
        string value = command.Value.Evaluate(variables);
        await store.SetAsync(key, value, cancellationToken).ConfigureAwait(false);
        return value;
    }
}

public sealed class ReadValueCommandHandler(IFluNetKeyValueStore store, IVariableResolver variables)
    : ICommandHandler<ReadValueCommand, string>
{
    public async ValueTask<string> HandleAsync(ReadValueCommand command, CancellationToken cancellationToken = default)
    {
        string key = command.Key.Evaluate(variables);
        return await store.GetAsync(key, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Storage key '{key}' was not found.");
    }
}

public sealed class ListValuesCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<ListValuesCommand, JsonElement[]>
{
    public ListValuesCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("storage.list.values")) return null;
        return new(new CommandBindingContext(command, new ExpressionBinder(language, values))
            .RequireText(SemanticRole.Source));
    }
}

public sealed class ListValuesCommandHandler(IFluNetKeyValueStore store, IVariableResolver variables)
    : ICommandHandler<ListValuesCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(ListValuesCommand command, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KeyValuePair<string, string>> values = await store.ListAsync(
            command.Prefix.Evaluate(variables), cancellationToken).ConfigureAwait(false);
        return values.Select(pair => JsonSerializer.SerializeToElement(new { key = pair.Key, value = pair.Value })).ToArray();
    }
}

public sealed class DeleteValueCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<DeleteValueCommand, string>
{
    public DeleteValueCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("storage.delete.value")) return null;
        return new(new CommandBindingContext(command, new ExpressionBinder(language, values))
            .RequireText(SemanticRole.Source));
    }
}

public sealed class DeleteValueCommandHandler(IFluNetKeyValueStore store, IVariableResolver variables)
    : ICommandHandler<DeleteValueCommand, string>
{
    public async ValueTask<string> HandleAsync(DeleteValueCommand command, CancellationToken cancellationToken = default)
    {
        string key = command.Key.Evaluate(variables);
        return await store.DeleteAsync(key, cancellationToken).ConfigureAwait(false) ? key : string.Empty;
    }
}
