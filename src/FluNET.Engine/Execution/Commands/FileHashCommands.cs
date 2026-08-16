using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record HashFileCommand(IExpression<string> Source) : ICommand<string>;

public sealed class HashFileCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<HashFileCommand, string>
{
    public HashFileCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.files.hash")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new HashFileCommand(context.RequireText(SemanticRole.Source));
    }
}

public sealed class HashFileCommandHandler(
    IFluNetFileHasher hasher,
    IVariableResolver variables) : ICommandHandler<HashFileCommand, string>
{
    public ValueTask<string> HandleAsync(
        HashFileCommand command,
        CancellationToken cancellationToken = default) =>
        hasher.ComputeSha256Async(command.Source.Evaluate(variables), cancellationToken);
}
