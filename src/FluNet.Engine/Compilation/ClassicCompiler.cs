using FluNET.Binding;
using FluNET.Diagnostics;
using FluNET.Language;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Parsing;

namespace FluNET.Compilation;

public sealed record ClassicCompilation(ScriptNode? Syntax, IReadOnlyList<BoundPipeline> Pipelines, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Syntax != null && Diagnostics.All(x => x.Severity != DiagnosticSeverity.Error);
}

public sealed class ClassicCompiler
{
    private readonly ClassicParser _parser;
    private readonly SemanticBinder _binder;

    public ClassicCompiler(LanguageSnapshot language, ValueResolverRegistry? resolvers = null, ValueConversionRegistry? conversions = null)
    {
        _parser = new ClassicParser(language);
        _binder = new SemanticBinder(language, resolvers, conversions);
    }

    public ClassicCompilation Compile(string source, BindingContext? context = null)
    {
        ParseResult parse = _parser.Parse(source);
        var diagnostics = new List<Diagnostic>(parse.Diagnostics);
        var pipelines = new List<BoundPipeline>();
        if (!parse.Success || parse.Script == null) return new(parse.Script, pipelines, diagnostics);
        foreach (PipelineNode pipeline in parse.Script.Pipelines)
        {
            BindingResult<BoundPipeline> binding = _binder.BindPipeline(pipeline, context);
            diagnostics.AddRange(binding.Diagnostics);
            if (binding.Value != null) pipelines.Add(binding.Value);
        }
        return new(parse.Script, pipelines, diagnostics);
    }
}
