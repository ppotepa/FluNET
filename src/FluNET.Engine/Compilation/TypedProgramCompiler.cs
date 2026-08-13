using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Prompt;
using System.Text.Json;

namespace FluNET.Compilation;

public sealed class CommandCompilationException(
    string code,
    string message,
    SourceSpan span,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
    public SourceSpan Span { get; } = span;
}

/// <summary>Binds typed commands once and produces the IR consumed by planning.</summary>
public sealed class TypedProgramCompiler
{
    private readonly CommandDispatcher _dispatcher;
    private readonly IValueCodecRegistry _values;

    public TypedProgramCompiler(CommandDispatcher dispatcher, LanguageSnapshot language)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(language);
        _values = new ValueCodecRegistry(language, EmptyServiceProvider.Instance, [], []);
    }

    public TypedProgram Compile(BoundProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        List<CompiledCommand> compiled = [];
        foreach (BoundCommand command in program.Commands)
        {
            ValidateLiteralInputs(command);
            CompiledCommand? typed;
            try
            {
                typed = _dispatcher.TryCompile(command);
            }
            catch (ExpressionBindingException exception)
            {
                throw new CommandCompilationException(
                    exception.Code,
                    exception.Message,
                    exception.Span,
                    exception);
            }
            catch (Exception exception) when (
                exception is FormatException or InvalidCastException or JsonException)
            {
                throw new CommandCompilationException(
                    ExpressionDiagnosticCodes.ValueParseFailure,
                    exception.Message,
                    command.Syntax.Span,
                    exception);
            }

            if (typed is null)
            {
                throw new CommandCompilationException(
                    "FLN144",
                    $"No typed compiler route is registered for '{command.Command.Name}/{command.Frame.UsageName}'.",
                    command.Syntax.Span);
            }
            compiled.Add(typed);
        }
        return new TypedProgram(program, compiled);
    }

    private void ValidateLiteralInputs(BoundCommand command)
    {
        foreach (BoundArgument argument in command.Arguments.Values.Where(argument =>
            argument.Slot.Direction == SlotDirection.Input && argument.IsPresent))
        {
            TypeSymbol expected = argument.Slot.ValueTypeSymbol;
            if (expected.Id == BuiltInTypeIds.Text)
            {
                continue;
            }

            ValueCodecDescriptor? codec = _values.Codecs.FirstOrDefault(item => item.TypeId == expected.Id);
            if (codec is null)
            {
                // Extension-specific binders remain authoritative until a custom
                // codec is registered through the module value API.
                continue;
            }

            PromptToken[] literals = argument.Tokens
                .Where(token => token.Kind != PromptTokenKind.Variable)
                .ToArray();
            if (literals.Length == 0)
            {
                continue;
            }
            if (argument.Slot.Cardinality != SlotCardinality.Repeated &&
                argument.Tokens.Count != 1)
            {
                throw new CommandCompilationException(
                    ExpressionDiagnosticCodes.ShapeMismatch,
                    $"Role {argument.RoleId} expects one '{expected.Name}' value.",
                    SpanOf(argument));
            }

            foreach (PromptToken token in literals)
            {
                try
                {
                    _values.Parse(expected.Id, Literal(expected, token));
                }
                catch (Exception exception) when (
                    exception is FormatException or InvalidCastException or InvalidOperationException or JsonException)
                {
                    throw new CommandCompilationException(
                        ExpressionDiagnosticCodes.ValueParseFailure,
                        $"Cannot parse '{token.Text}' as '{expected.Name}': {exception.Message}",
                        token.Span,
                        exception);
                }
            }
        }
    }

    private static ValueLiteral Literal(TypeSymbol expected, PromptToken token)
    {
        string value = token.Text;
        if (expected.Id != BuiltInTypeIds.Json &&
            token.Kind == PromptTokenKind.Reference &&
            value.Length >= 2 && value[0] == '{' && value[^1] == '}')
        {
            value = value[1..^1];
        }
        return new ValueLiteral(value);
    }

    private static SourceSpan SpanOf(BoundArgument argument)
    {
        PromptToken first = argument.Tokens[0];
        PromptToken last = argument.Tokens[^1];
        return SourceSpan.FromBounds(first.Span.Start, last.Span.End);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();
        public object? GetService(Type serviceType) => null;
    }
}
