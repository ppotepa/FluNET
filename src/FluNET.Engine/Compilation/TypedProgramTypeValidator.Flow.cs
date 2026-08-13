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
                foreach (PromptToken token in argument.Tokens.Where(token =>
                    token.Kind == PromptTokenKind.Variable))
                {
                    string name = VariableName(token);
                    if (producers.TryGetValue(name, out Producer? producer))
                    {
                        if (producer.Stage >= stages[index])
                        {
                            throw new CommandCompilationException(
                                "FLN150",
                                $"Variable '[{name}]' is produced in the same parallel stage.",
                                token.Span);
                        }
                        ValidateType(
                            name,
                            producer.Type,
                            argument.Slot.ValueTypeSymbol,
                            token.Span);
                        continue;
                    }

                    if (_variables is null || _language is null)
                    {
                        continue;
                    }
                    if (!_variables.IsRegistered(token.Text))
                    {
                        throw new CommandCompilationException(
                            "FLN150",
                            $"Variable '[{name}]' has no producer and is not registered by the host.",
                            token.Span);
                    }

                    object? hostValue = _variables.Resolve<object>(token.Text);
                    if (hostValue is null)
                    {
                        throw new CommandCompilationException(
                            "FLN150",
                            $"Host variable '[{name}]' has no runtime value.",
                            token.Span);
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
                            $"Host variable '[{name}]' uses undeclared CLR type " +
                            $"'{hostValue.GetType()}'.",
                            token.Span,
                            exception);
                    }
                    ValidateType(
                        name,
                        hostType,
                        argument.Slot.ValueTypeSymbol,
                        token.Span);
                }
            }

            foreach ((string name, TypeSymbol type, SourceSpan span) in OutputVariables(command))
            {
                if (producers.TryGetValue(name, out Producer? existing) && existing.Type.Id != type.Id)
                {
                    throw new CommandCompilationException(
                        "FLN151",
                        $"Variable '[{name}]' cannot change type from '{existing.Type}' to '{type}'.",
                        span);
                }
                producers[name] = new Producer(type, stages[index]);
            }
        }
    }

    private static IEnumerable<(string Name, TypeSymbol Type, SourceSpan Span)> OutputVariables(
        BoundCommand command)
    {
        foreach (BoundArgument argument in command.Arguments.Values.Where(argument =>
            argument.Slot.Direction == SlotDirection.Output && argument.IsPresent))
        {
            foreach (PromptToken token in argument.Tokens.Where(token =>
                token.Kind == PromptTokenKind.Variable))
            {
                yield return (VariableName(token), argument.Slot.ValueTypeSymbol, token.Span);
            }
        }
    }

    private static string VariableName(PromptToken token) =>
        token.Text.Length >= 2 && token.Text[0] == '[' && token.Text[^1] == ']'
            ? token.Text[1..^1]
            : token.Text;

    private sealed record Producer(TypeSymbol Type, int Stage);
}
