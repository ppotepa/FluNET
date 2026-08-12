using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Prompt;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record SayCommand(TextExpression Message) : ICommand<string>;

public sealed class SayCommandBinder(LanguageSnapshot language)
    : ICommandBinder<SayCommand, string>
{
    public SayCommand? TryBind(CommandSyntax syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        CommandDescriptor? descriptor = language.FindCommand(syntax.Verb.Text);
        if (!string.Equals(descriptor?.Name, "SAY", StringComparison.Ordinal))
        {
            return null;
        }

        ClauseSyntax subject = syntax.Clauses.First(clause => clause.Kind == PromptClauseKind.Subject);
        TextPart[] parts = subject.Values.Select(BindPart).ToArray();
        return new SayCommand(new TextExpression(parts));
    }

    private TextPart BindPart(PromptToken token) => token.Kind switch
    {
        PromptTokenKind.Variable => new VariableTextPart(token.Text),
        PromptTokenKind.Reference => new LiteralTextPart(UnwrapReference(token.Text)),
        _ => new LiteralTextPart(NormalizeLiteral(token.Text))
    };

    private string NormalizeLiteral(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1]
                .Replace("\\\"", "\"")
                .Replace("\\'", "'")
                .Replace("\\\\", "\\");
        }

        // Preserve compatibility with the legacy WordFactory, which resolves
        // a command alias used as message text to its canonical command name.
        return language.FindCommand(value)?.Name ?? value;
    }

    private static string UnwrapReference(string value) =>
        value.Length >= 2 && value[0] == '{' && value[^1] == '}'
            ? value[1..^1]
            : value;
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
