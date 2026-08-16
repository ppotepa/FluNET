using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record GetBlobCommand(IExpression<string> Key) : ICommand<string>;

public sealed class GetBlobCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<GetBlobCommand, string>
{
    public GetBlobCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("storage.blob.get")) return null;
        return new(new CommandBindingContext(command, new ExpressionBinder(language, values))
            .RequireText(SemanticRole.Source));
    }
}

public sealed class GetBlobCommandHandler(IFluNetBlobStore store, IVariableResolver variables)
    : ICommandHandler<GetBlobCommand, string>
{
    public async ValueTask<string> HandleAsync(GetBlobCommand command, CancellationToken cancellationToken = default)
    {
        string key = command.Key.Evaluate(variables);
        return await store.GetAsync(key, cancellationToken).ConfigureAwait(false)
            ?? throw new FileNotFoundException($"Blob '{key}' was not found.");
    }
}

public sealed record PutBlobCommand(IExpression<string> Key, IExpression<string> Value) : ICommand<string>;

public sealed class PutBlobCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<PutBlobCommand, string>
{
    public PutBlobCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("storage.blob.put")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source), context.RequireText(SemanticRole.Theme));
    }
}

public sealed class PutBlobCommandHandler(IFluNetBlobStore store, IVariableResolver variables)
    : ICommandHandler<PutBlobCommand, string>
{
    public async ValueTask<string> HandleAsync(PutBlobCommand command, CancellationToken cancellationToken = default)
    {
        string key = command.Key.Evaluate(variables);
        await store.PutAsync(key, command.Value.Evaluate(variables), cancellationToken).ConfigureAwait(false);
        return key;
    }
}

public sealed record DeleteBlobCommand(IExpression<string> Key) : ICommand<string>;

public sealed record ListBlobCommand(IExpression<string> Prefix) : ICommand<JsonElement[]>;

public sealed class DeleteBlobCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<DeleteBlobCommand, string>
{
    public DeleteBlobCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("storage.blob.delete")) return null;
        return new(new CommandBindingContext(command, new ExpressionBinder(language, values))
            .RequireText(SemanticRole.Source));
    }
}

public sealed class DeleteBlobCommandHandler(IFluNetBlobStore store, IVariableResolver variables)
    : ICommandHandler<DeleteBlobCommand, string>
{
    public async ValueTask<string> HandleAsync(DeleteBlobCommand command, CancellationToken cancellationToken = default)
    {
        string key = command.Key.Evaluate(variables);
        bool deleted = await store.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
        return deleted ? key : string.Empty;
    }
}

public sealed class ListBlobCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<ListBlobCommand, JsonElement[]>
{
    public ListBlobCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("storage.blob.list")) return null;
        return new(new CommandBindingContext(command, new ExpressionBinder(language, values))
            .RequireText(SemanticRole.Source));
    }
}

public sealed class ListBlobCommandHandler(IFluNetBlobStore store, IVariableResolver variables)
    : ICommandHandler<ListBlobCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(ListBlobCommand command, CancellationToken cancellationToken = default)
    {
        string prefix = command.Prefix.Evaluate(variables);
        IReadOnlyList<string> keys = await store.ListAsync(prefix, cancellationToken).ConfigureAwait(false);
        return keys.Select(key => JsonSerializer.SerializeToElement(new { key })).ToArray();
    }
}
