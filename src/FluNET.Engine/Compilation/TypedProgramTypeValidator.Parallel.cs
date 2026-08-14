using FluNET.Prompt;

namespace FluNET.Compilation;

public sealed partial class TypedProgramTypeValidator
{
    private static void ValidateParallelWrites(BoundProgram program, IReadOnlyList<int> stages)
    {
        Dictionary<(int Stage, string Name), int> writes = [];
        for (int index = 0; index < program.Commands.Count; index++)
        {
            foreach (OutputProducer output in OutputVariables(program.Commands[index]))
            {
                var key = (stages[index], output.Name.ToUpperInvariant());
                if (writes.TryGetValue(key, out int existing))
                {
                    throw new CommandCompilationException(
                        "FLN153",
                        $"Parallel commands {existing} and {index} both write variable '[{output.Name}]'.",
                        output.Span);
                }
                writes[key] = index;
            }
        }
    }
}
