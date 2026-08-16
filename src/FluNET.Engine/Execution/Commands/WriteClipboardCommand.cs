using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Syntax.Validation;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record WriteClipboardCommand(IExpression<string> Value) : ICommand<string>;

public sealed class WriteClipboardCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<WriteClipboardCommand, string>
{
    public WriteClipboardCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.system.clipboard.write")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source));
    }
}

public sealed class WriteClipboardCommandHandler(
    IFluNetClipboardWriter clipboard,
    IVariableResolver variables) : ICommandHandler<WriteClipboardCommand, string>
{
    public async ValueTask<string> HandleAsync(
        WriteClipboardCommand command,
        CancellationToken cancellationToken = default)
    {
        string value = command.Value.Evaluate(variables);
        await clipboard.WriteTextAsync(value, cancellationToken).ConfigureAwait(false);
        return value;
    }
}
