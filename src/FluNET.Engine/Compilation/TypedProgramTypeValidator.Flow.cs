using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;

namespace FluNET.Compilation;

public sealed partial class TypedProgramTypeValidator
{
    public void Validate(TypedProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        int[] stages = BuildStages(program.BoundProgram);
        ValidateParallelWrites(program.BoundProgram, stages);
        Dictionary<string, Producer> producers = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < program.BoundProgram.Commands.Count; index++)
        {
            BoundCommand command = program.BoundProgram.Commands[index];
            foreach (BoundArgument argument in command.Arguments.Values.Where(argument =>
                argument.Slot.Direction == SlotDirection.Input && argument.IsPresent))
            {
                foreach (PromptToken token in argument.Tokens)
                {
                    if (token.Kind == PromptTokenKind.Variable)
                    {
                        string name = VariableName(token);
                        ValidateInputVariable(name, token.Text, argument.Slot.ValueTypeSymbol,
                            token.Span, stages[index], producers);
                        continue;
                    }

                    if (token.Kind == PromptTokenKind.Reference)
                    {
                        continue;
                    }

                    foreach (string name in InterpolationVariableNames(token.Text))
                    {
                        ValidateInterpolationVariable(name, token.Span, stages[index], producers);
                    }
                }
            }

            ValidateConditionVariables(command, stages[index], producers);

            foreach (OutputProducer output in OutputVariables(command))
            {
                producers[output.Name] = new Producer(
                    output.Type,
                    stages[index],
                    output.IsRuntimeTyped);
            }
        }
    }

    private void ValidateInputVariable(
        string name,
        string reference,
        TypeSymbol expectedType,
        SourceSpan span,
        int stage,
        IReadOnlyDictionary<string, Producer> producers)
    {
        if (producers.TryGetValue(name, out Producer? producer))
        {
            if (producer.Stage > stage)
            {
                throw new CommandCompilationException(
                    "FLN150",
                    $"Variable '[{name}]' is produced in a later dependency stage.",
                    span);
            }
            if (!producer.IsRuntimeTyped)
            {
                ValidateType(name, producer.Type, expectedType, span);
            }
            return;
        }
        ValidateHostVariable(name, reference, expectedType, span);
    }

    private void ValidateInterpolationVariable(
        string name,
        SourceSpan span,
        int stage,
        IReadOnlyDictionary<string, Producer> producers)
    {
        if (producers.TryGetValue(name, out Producer? producer))
        {
            if (producer.Stage > stage)
            {
                throw new CommandCompilationException(
                    "FLN150",
                    $"Interpolation variable '[{name}]' is produced in a later dependency stage.",
                    span);
            }
            return;
        }
        if (_variables is null)
        {
            return;
        }
        string reference = $"[{name}]";
        if (!_variables.IsRegistered(reference) || _variables.Resolve<object>(reference) is null)
        {
            throw new CommandCompilationException(
                "FLN150",
                $"Interpolation variable '[{name}]' has no producer and is not registered by the host.",
                span);
        }
    }

    private void ValidateHostVariable(
        string name,
        string reference,
        TypeSymbol expectedType,
        SourceSpan span)
    {
        if (_variables is null || _language is null)
        {
            return;
        }
        if (!_variables.IsRegistered(reference))
        {
            throw new CommandCompilationException("FLN150", $"Variable [{name}] not found.", span);
        }
        object? hostValue = _variables.Resolve<object>(reference);
        if (hostValue is null)
        {
            throw new CommandCompilationException("FLN150", $"Host variable '[{name}]' has no runtime value.", span);
        }

        TypeSymbol hostType;
        try
        {
            hostType = _language.Types.Get(hostValue.GetType());
        }
        catch (LanguageDefinitionException exception)
        {
            throw new CommandCompilationException(
                "FLN151",
                $"Host variable '[{name}]' uses undeclared CLR type '{hostValue.GetType()}'.",
                span,
                exception);
        }
        ValidateType(name, hostType, expectedType, span);
    }

    private void ValidateConditionVariables(
        BoundCommand command,
        int stage,
        IReadOnlyDictionary<string, Producer> producers)
    {
        foreach (CommandModifierSyntax modifier in command.Syntax.Modifiers.Where(modifier =>
            modifier.Kind == CommandModifierKind.Condition))
        {
            if (modifier.Values.Count == 0)
            {
                throw new CommandCompilationException("FLN154",
                    "IF must be followed by a condition expression.", modifier.Introducer.Span);
            }

            string source = string.Join(" ", modifier.Values.Select(token => token.Text));
            CompiledCondition condition;
            try
            {
                condition = ConditionExpressionCache.GetOrCompile(source);
            }
            catch (Exception exception) when (
                exception is FormatException or NotSupportedException or InvalidOperationException)
            {
                throw new CommandCompilationException("FLN154",
                    $"Invalid condition expression: {exception.Message}", modifier.Values[0].Span, exception);
            }

            foreach (string name in condition.VariableReferences)
            {
                SourceSpan span = modifier.Values[0].Span;
                if (producers.TryGetValue(name, out Producer? producer))
                {
                    if (producer.Stage > stage)
                    {
                        throw new CommandCompilationException("FLN150",
                            $"Condition variable '[{name}]' is produced in a later dependency stage.", span);
                    }
                    continue;
                }
                if (_variables is null) continue;
                string reference = $"[{name}]";
                if (!_variables.IsRegistered(reference) || _variables.Resolve<object>(reference) is null)
                {
                    throw new CommandCompilationException("FLN150",
                        $"Condition variable '[{name}]' has no producer and is not registered by the host.", span);
                }
            }
        }
    }

    private static IEnumerable<string> InterpolationVariableNames(string text)
    {
        int cursor = 0;
        while (cursor < text.Length)
        {
            int open = text.IndexOf('{', cursor);
            if (open < 0) yield break;
            if (open + 1 < text.Length && text[open + 1] == '{')
            {
                cursor = open + 2;
                continue;
            }
            int close = text.IndexOf('}', open + 1);
            if (close < 0) yield break;
            string expression = text[(open + 1)..close].Trim();
            int end = expression.IndexOfAny(['.', '[']);
            string root = (end < 0 ? expression : expression[..end]).Trim();
            if (root.Length > 0 && (char.IsLetter(root[0]) || root[0] == '_') &&
                root.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_'))
            {
                yield return root;
            }
            cursor = close + 1;
        }
    }

    private static IEnumerable<OutputProducer> OutputVariables(BoundCommand command)
    {
        foreach (BoundArgument argument in command.Arguments.Values.Where(argument =>
            argument.Slot.Direction == SlotDirection.Output && argument.IsPresent))
        {
            foreach (PromptToken token in argument.Tokens.Where(token => token.Kind == PromptTokenKind.Variable))
            {
                string[] destructured = DestructuredNames(token.Text);
                if (destructured.Length > 0)
                {
                    foreach (string name in destructured)
                    {
                        yield return new OutputProducer(name, argument.Slot.ValueTypeSymbol, token.Span, true);
                    }
                    continue;
                }
                yield return new OutputProducer(VariableName(token), argument.Slot.ValueTypeSymbol, token.Span, false);
            }
        }
    }

    private static string[] DestructuredNames(string reference)
    {
        string normalized = reference.TrimEnd('.').Trim();
        if (normalized.Length < 4 ||
            !normalized.StartsWith("[{", StringComparison.Ordinal) ||
            !normalized.EndsWith("}]", StringComparison.Ordinal))
        {
            return [];
        }
        return normalized[2..^2]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
    }

    private static string VariableName(PromptToken token) =>
        token.Text.Length >= 2 && token.Text[0] == '[' && token.Text[^1] == ']'
            ? token.Text[1..^1]
            : token.Text;

    private sealed record OutputProducer(string Name, TypeSymbol Type, SourceSpan Span, bool IsRuntimeTyped);
    private sealed record Producer(TypeSymbol Type, int Stage, bool IsRuntimeTyped = false);
}
