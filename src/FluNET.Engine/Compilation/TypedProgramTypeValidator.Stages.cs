using FluNET.Prompt;

namespace FluNET.Compilation;

public sealed partial class TypedProgramTypeValidator
{
    private static int[] BuildStages(BoundProgram program)
    {
        int[] stages = new int[program.Commands.Count];
        int stage = 0;
        for (int index = 1; index < stages.Length; index++)
        {
            CommandLinkSyntax? link = program.Program.Syntax.Links.FirstOrDefault(candidate =>
                candidate.PredecessorIndex == index - 1 && candidate.SuccessorIndex == index);
            if (link?.Kind != CommandLinkKind.Parallel)
            {
                stage++;
            }
            stages[index] = stage;
        }
        return stages;
    }
}
