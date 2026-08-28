using FluNET.Execution;
using FluNET.Prompt;
using FluNET.Syntax.Validation;

namespace FluNET.Tests;

/// <summary>
/// Keeps historical behavioral fixtures readable without reintroducing the removed
/// synchronous/tuple API into the shipped FluNET.Engine assembly.
/// </summary>
internal static class LegacyTestExecutionAdapters
{
    public static (ValidationResult ValidationResult, object? SourceSentence, object? Result) Run(
        this Engine engine,
        ProcessedPrompt prompt)
    {
        ExecutionResult execution = engine.ExecuteAsync(prompt).GetAwaiter().GetResult();
        return (execution.ValidationResult, null, execution.Result);
    }

    public static ExecutionResult Execute(this Engine engine, ProcessedPrompt prompt) =>
        engine.ExecuteAsync(prompt).GetAwaiter().GetResult();
}
