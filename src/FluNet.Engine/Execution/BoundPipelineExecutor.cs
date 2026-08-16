using FluNET.Binding;
using FluNET.Execution.Capabilities;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Core;

namespace FluNET.Execution;

public sealed record BoundPipelineExecutionResult(
    object? Result,
    IReadOnlyDictionary<string, object?> Variables,
    IReadOnlyList<BoundExecutionResult> Sentences);

/// <summary>
/// Executes a semantically bound pipeline. Values flow through THEN implicitly and output
/// WHAT variables are populated from each sentence result for Classic compatibility.
/// </summary>
public sealed class BoundPipelineExecutor
{
    private readonly BoundSentenceExecutor _sentenceExecutor;
    private readonly ICapabilityPolicy _capabilities;

    public BoundPipelineExecutor(
        BoundSentenceExecutor? sentenceExecutor = null,
        ICapabilityPolicy? capabilities = null)
    {
        _sentenceExecutor = sentenceExecutor ?? new BoundSentenceExecutor();
        _capabilities = capabilities ?? AllowAllCapabilityPolicy.Instance;
    }

    public async ValueTask<BoundPipelineExecutionResult> ExecuteAsync(
        BoundPipeline pipeline,
        IReadOnlyDictionary<string, object?>? initialVariables = null,
        IServiceProvider? services = null,
        CancellationToken cancellationToken = default)
    {
        var variables = initialVariables != null
            ? new Dictionary<string, object?>(initialVariables, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var executions = new List<BoundExecutionResult>();
        object? pipelineValue = null;

        foreach (BoundSentence sentence in pipeline.Sentences)
        {
            EnsureCapabilities(sentence);

            var activation = new ActivationContext(variables, pipelineValue, services);
            BoundExecutionResult execution = await _sentenceExecutor.ExecuteAsync(sentence, activation, cancellationToken);
            executions.Add(execution);
            pipelineValue = execution.Result;
            StoreOutputBindings(sentence, execution.Result, variables);
        }

        return new(pipelineValue, variables, executions);
    }

    private void EnsureCapabilities(BoundSentence sentence)
    {
        foreach (string capability in sentence.Verb.Capabilities)
        {
            if (!_capabilities.IsAllowed(capability, sentence.Verb))
                throw new CapabilityDeniedException(capability, sentence.Verb);
        }
    }

    private static void StoreOutputBindings(
        BoundSentence sentence,
        object? result,
        IDictionary<string, object?> variables)
    {
        foreach (BoundRole role in sentence.Roles.Where(x =>
            x.Descriptor.Direction is RoleDirection.Output or RoleDirection.InputOutput))
        {
            foreach (BoundValue value in role.Values)
            {
                if (value.Source is VariableExpression variable)
                    variables[variable.Name] = result;
            }
        }
    }
}
