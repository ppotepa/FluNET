using FluNET.Binding;
using FluNET.Compilation;
using FluNET.Diagnostics;
using FluNET.Execution.Capabilities;
using FluNET.Language;

namespace FluNET.Execution;

public sealed record ClassicScriptResult(
    ClassicCompilation Compilation,
    IReadOnlyList<BoundPipelineExecutionResult> Executions,
    IReadOnlyDictionary<string, object?> Variables)
{
    public bool Success => Compilation.Success;
    public object? Result => Executions.LastOrDefault()?.Result;
}

/// <summary>
/// New opt-in FluNET.Classic execution path. It does not replace the legacy Engine yet;
/// it proves source -> AST -> binding -> capability check -> execution as one coherent API.
/// </summary>
public sealed class ClassicScriptEngine
{
    private readonly ClassicCompiler _compiler;
    private readonly BoundPipelineExecutor _executor;
    private readonly IServiceProvider? _services;

    public ClassicScriptEngine(
        LanguageSnapshot language,
        IServiceProvider? services = null,
        ValueResolverRegistry? resolvers = null,
        ValueConversionRegistry? conversions = null,
        ICapabilityPolicy? capabilities = null)
    {
        _compiler = new ClassicCompiler(language, resolvers, conversions);
        _executor = new BoundPipelineExecutor(capabilities: capabilities);
        _services = services;
    }

    public async ValueTask<ClassicScriptResult> RunAsync(
        string source,
        IReadOnlyDictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var typeMap = variables?.Where(x => x.Value != null)
            .ToDictionary(x => x.Key, x => x.Value!.GetType(), StringComparer.OrdinalIgnoreCase);
        ClassicCompilation compilation = _compiler.Compile(source, new BindingContext(typeMap, Services: _services));
        if (!compilation.Success)
            return new(compilation, [], variables ?? new Dictionary<string, object?>());

        var runtimeVariables = variables != null
            ? new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var executions = new List<BoundPipelineExecutionResult>();

        foreach (BoundPipeline pipeline in compilation.Pipelines)
        {
            BoundPipelineExecutionResult execution = await _executor.ExecuteAsync(
                pipeline,
                runtimeVariables,
                _services,
                cancellationToken);
            executions.Add(execution);
            foreach (KeyValuePair<string, object?> pair in execution.Variables)
                runtimeVariables[pair.Key] = pair.Value;
        }

        return new(compilation, executions, runtimeVariables);
    }
}
