using FluNET.Prompt;

namespace FluNET.Compilation;

public sealed partial class TypedProgramTypeValidator
{
    private static void ValidateParallelWrites(BoundProgram program, IReadOnlyList<int> stages)
    {
        Dictionary<(int Stage, string Name), int> writes = [];
        for (int index = 0; index < program.Commands.Count; index++)
        {
            foreach ((string name, _, SourceSpan span) in OutputVariables(program.Commands[index]))
            {
                var key = (stages[index], name.ToUpperInvariant());
                if (writes.TryGetValue(key, out int existing))
                {
                    throw new CommandCompilationException(
                        "FLN153",
                        $"Parallel commands {existing} and {index} both write variable '[{name}]'.",
                        span);
                }
                writes[key] = index;
            }
        }
    }
}
