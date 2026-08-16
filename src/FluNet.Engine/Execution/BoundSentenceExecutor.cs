using FluNET.Binding;
using FluNET.Syntax.Core;
using System.Reflection;

namespace FluNET.Execution;

public sealed record BoundExecutionResult(IVerb Verb, object? Result);

/// <summary>
/// Transitional executor for the new bound model. New async verbs use IAsyncVerb;
/// legacy Classic verbs continue to execute through their parameterless Invoke method.
/// </summary>
public sealed class BoundSentenceExecutor
{
    private readonly VerbActivator _activator = new();

    public async ValueTask<BoundExecutionResult> ExecuteAsync(
        BoundSentence sentence,
        ActivationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        IVerb verb = _activator.Create(sentence, context);

        if (verb is IAsyncVerb asyncVerb)
        {
            object? asyncResult = await asyncVerb.InvokeAsync(cancellationToken);
            return new BoundExecutionResult(verb, asyncResult);
        }

        MethodInfo? invoke = verb.GetType().GetMethod(
            "Invoke",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (invoke == null)
            throw new InvalidOperationException($"Verb '{verb.GetType().FullName}' does not expose Invoke() or IAsyncVerb.");

        object? result = invoke.Invoke(verb, null);
        return new BoundExecutionResult(verb, result);
    }
}
