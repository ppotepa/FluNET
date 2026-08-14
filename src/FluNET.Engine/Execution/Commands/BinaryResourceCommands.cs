using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Resources;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record LoadBinaryCommand(IExpression<string> Source) : ICommand<BinaryValue>;
public sealed record LoadImageCommand(IExpression<string> Source) : ICommand<ImageValue>;

public sealed class LoadBinaryCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<LoadBinaryCommand, BinaryValue>
{
    public LoadBinaryCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.load.binary")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new LoadBinaryCommand(context.RequireText(SemanticRole.Source));
    }
}

public sealed class LoadImageCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<LoadImageCommand, ImageValue>
{
    public LoadImageCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.load.image")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new LoadImageCommand(context.RequireText(SemanticRole.Source));
    }
}

public sealed class LoadBinaryCommandHandler(IVariableResolver variables, IExecutionPolicy policy, IResourceDecoderRegistry decoders, LanguageSnapshot language) : ICommandHandler<LoadBinaryCommand, BinaryValue>
{
    public async ValueTask<BinaryValue> HandleAsync(LoadBinaryCommand command, CancellationToken cancellationToken = default)
    {
        string path = command.Source.Evaluate(variables); policy.EnsureFileAccess(path);
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        ResourceDescriptor descriptor = new(new FileResourceReference(path, !Path.IsPathRooted(path)), ResourceFormat.Binary, language.Types.Get<BinaryValue>(), Path.GetFileNameWithoutExtension(path));
        return (BinaryValue)decoders.Decode(descriptor, new ResourcePayload(bytes, "application/octet-stream"));
    }
}

public sealed class LoadImageCommandHandler(IVariableResolver variables, IExecutionPolicy policy, IResourceDecoderRegistry decoders, LanguageSnapshot language) : ICommandHandler<LoadImageCommand, ImageValue>
{
    public async ValueTask<ImageValue> HandleAsync(LoadImageCommand command, CancellationToken cancellationToken = default)
    {
        string path = command.Source.Evaluate(variables); policy.EnsureFileAccess(path);
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        ResourceDescriptor descriptor = new(new FileResourceReference(path, !Path.IsPathRooted(path)), ResourceFormat.Image, language.Types.Get<ImageValue>(), Path.GetFileNameWithoutExtension(path));
        return (ImageValue)decoders.Decode(descriptor, new ResourcePayload(bytes));
    }
}
