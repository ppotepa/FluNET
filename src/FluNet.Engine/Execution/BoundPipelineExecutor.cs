using FluNET.Binding;
using FluNET.Execution.Capabilities;

namespace FluNET.Execution;

public sealed record BoundPipelineExecutionResult(
    object? Result,
    IReadOnlyDictionary<string, object?> Variables,
    IReadOnlyList<BoundExecutionResult> Sentences);

/// <summary>
/// Executes a semantically bound pipeline. Values flow through THEN implicitly and output
/// bindings are projected from each CLR result by name or position.
/// </summary>
public sealed class BoundPipelineExecutor
{
    private readonly BoundSentenceExecutor _sentenceExecutor;
    private readonly ICapabilityPolicy _capabilities;
    private readonly OutputBindingProjector _outputs;

    public BoundPipelineExecutor(
        BoundSentenceExecutor? sentenceExecutor = null,
        ICapabilityPolicy? capabilities = null,
        OutputBindingProjector? outputs = null)
    {
        _sentenceExecutor = sentenceExecutor ?? new BoundSentenceExecutor();
        _capabilities = capabilities ?? AllowAllCapabilityPolicy.Instance;
        _outputs = outputs ?? new OutputBindingProjector();
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

            foreach (KeyValuePair<string, object?> binding in _outputs.Project(sentence, execution.Result))
                variables[binding.Key] = binding.Value;
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
}
