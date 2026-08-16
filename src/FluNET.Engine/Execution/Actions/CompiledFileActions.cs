using FluNET.Capabilities;
using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Variables;

namespace FluNET.Execution.Actions;

public sealed class CompiledCreateDirectoryAction(
    IExpression<string> path,
    IFluNetDirectoryOperations directories) : ICompiledAction
{
    public string Kind => "MKDIR";

    public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default) =>
        _ = await directories.CreateAsync(path.Evaluate(variables), cancellationToken).ConfigureAwait(false);
}

public sealed class CompiledFileTransferAction(
    IExpression<string> source,
    IExpression<string> target,
    IFluNetFileOperations files,
    bool move) : ICompiledAction
{
    public string Kind => move ? "MOVE" : "COPY";

    public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default)
    {
        string sourcePath = source.Evaluate(variables);
        string targetPath = target.Evaluate(variables);
        if (move)
            _ = await files.MoveAsync(sourcePath, targetPath, cancellationToken).ConfigureAwait(false);
        else
            _ = await files.CopyAsync(sourcePath, targetPath, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class CompiledTrashAction(
    IExpression<string> source,
    IFluNetFileTrash trash) : ICompiledAction
{
    public string Kind => "TRASH";

    public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default) =>
        _ = await trash.MoveToTrashAsync(source.Evaluate(variables), cancellationToken).ConfigureAwait(false);
}

public sealed class CompiledArchiveAction(
    IExpression<string> source,
    IExpression<string> target,
    IFluNetArchive archive,
    bool extract) : ICompiledAction
{
    public string Kind => extract ? "UNPACK" : "PACK";

    public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default)
    {
        string sourcePath = source.Evaluate(variables);
        string targetPath = target.Evaluate(variables);
        if (extract)
            _ = await archive.ExtractAsync(sourcePath, targetPath, cancellationToken).ConfigureAwait(false);
        else
            _ = await archive.CreateAsync(sourcePath, targetPath, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class CompiledPublishAction(
    IExpression<string> payload,
    IExpression<string> topic,
    IFluNetMessageBus bus) : ICompiledAction
{
    public string Kind => "PUBLISH";

    public ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default) =>
        bus.PublishAsync(topic.Evaluate(variables), payload.Evaluate(variables), cancellationToken);
}

public sealed class CompiledNotifyAction(
    IExpression<string> message,
    IFluNetNotifier notifier) : ICompiledAction
{
    public string Kind => "NOTIFY";

    public ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default) =>
        notifier.NotifyAsync(message.Evaluate(variables), cancellationToken);
}
