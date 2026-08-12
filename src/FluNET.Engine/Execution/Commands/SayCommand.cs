using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Variables;
using FluNET.Language.Binding;
using FluNET.Syntax.Verbs;

namespace FluNET.Execution.Commands;

public sealed record SayCommand(TextExpression Message) : ICommand<string>;

public sealed class SayCommandBinder(LanguageSnapshot language)
    : ICommandBinder<SayCommand, string>
{
    public SayCommand? TryBind(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Frame.ImplementationType != typeof(SayText))
        {
            return null;
        }

        return new SayCommand(TextExpression.Bind(command[SemanticRole.Theme], language));
    }
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
