using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record SayCommand(IExpression<string> Message) : ICommand<string>;

public sealed class SayCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<SayCommand, string>(language, values)
{
    protected override SayCommand Bind(BoundCommand command) =>
        new(Context(command).RequireText(SemanticRole.Theme));
}

public sealed class SayCommandHandler(
    IVariableResolver variables,
    ITextOutput output) : ICommandHandler<SayCommand, string>
{
    public async ValueTask<string> HandleAsync(
        SayCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        string message = command.Message.Evaluate(variables);
        await output.WriteLineAsync(message, cancellationToken).ConfigureAwait(false);
        return message;
    }
}
