using FluNET.Diagnostics;
using FluNET.Language;
using FluNET.Language.Metadata;
using FluNET.Syntax.Ast;

namespace FluNET.Binding;

public sealed record BoundValue(
    ExpressionNode Source,
    Type ExpectedType,
    Type ActualType,
    object? ConstantValue,
    int ConversionCost,
    ValueConversion? Conversion = null);

public sealed record BoundRole(ClauseDescriptor Descriptor, IReadOnlyList<BoundValue> Values);
public sealed record BoundSentence(VerbDescriptor Verb, ConstructorDescriptor? Constructor, IReadOnlyList<BoundRole> Roles, Type? ResultType, int BindingCost);
public sealed record BoundPipeline(IReadOnlyList<BoundSentence> Sentences, Type? ResultType);

public sealed record BindingResult<T>(T? Value, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Value != null && Diagnostics.All(x => x.Severity != DiagnosticSeverity.Error);
}

public sealed record BindingContext(
    IReadOnlyDictionary<string, Type>? VariableTypes = null,
    Type? PipelineType = null,
    IServiceProvider? Services = null);
