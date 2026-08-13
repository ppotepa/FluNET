using FluNET.Compilation;

namespace FluNET.Execution;

internal static class TypedExecutionContext
{
    internal const string Key = "FluNET.TypedProgram";

    public static TypedProgram? GetTypedProgram(this ExecutionContext context) =>
        context.Data.TryGetValue(Key, out object? value)
            ? value as TypedProgram
            : null;

    public static void SetTypedProgram(this ExecutionContext context, TypedProgram program) =>
        context.Data[Key] = program ?? throw new ArgumentNullException(nameof(program));
}
