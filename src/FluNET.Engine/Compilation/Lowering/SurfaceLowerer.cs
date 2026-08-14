using FluNET.Compilation.Inference;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt;
using FluNET.Prompt.Expressions;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

public sealed class SurfaceLowerer
{
    private readonly InferenceEngine _inference;
    private readonly IResourceProviderRegistry _providers;

    public SurfaceLowerer(IResourceProviderRegistry providers) : this(new InferenceEngine(), providers) { }
    public SurfaceLowerer(InferenceEngine inference, IResourceProviderRegistry providers)
    { _inference = inference ?? throw new ArgumentNullException(nameof(inference)); _providers = providers ?? throw new ArgumentNullException(nameof(providers)); }

    public LoweringResult Lower(SurfaceParseResult parse, PromptGrammar grammar, LanguageSnapshot language)
    {
        ArgumentNullException.ThrowIfNull(parse); ArgumentNullException.ThrowIfNull(grammar); ArgumentNullException.ThrowIfNull(language);
        List<CommandSyntax> commands = []; List<CommandLinkSyntax> links = []; List<SourceMapEntry> map = [];
        List<SurfaceDiagnostic> diagnostics = [.. parse.Diagnostics]; InferenceTrace trace = new();
        Dictionary<string, Uri> namedBases = new(StringComparer.OrdinalIgnoreCase);
        LowerStatements(parse.Program.Statements, new LoweringContext(null, null, null), namedBases, grammar, language, commands, links, map, trace, diagnostics);
        return new LoweringResult(parse.Document, parse.Program, new PromptSyntax(commands, links), new SourceMap(map), trace, diagnostics);
    }

    private void LowerStatements(IReadOnlyList<SurfaceStatementSyntax> statements, LoweringContext inherited,
        IDictionary<string, Uri> namedBases, PromptGrammar grammar, LanguageSnapshot language,
        List<CommandSyntax> commands, List<CommandLinkSyntax> links, List<SourceMapEntry> map,
        InferenceTrace trace, List<SurfaceDiagnostic> diagnostics)
    {
        LoweringContext current = inherited; string? implicitOutput = null;
        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfaceContextSyntax context)
            {
                if (!TryResolveBase(context.BaseResource.UnquotedText, current.BaseUri, out Uri? baseUri)) { diagnostics.Add(new SurfaceDiagnostic("FLN240", $"FROM context base '{context.BaseResource.Text}' is not a valid URI.", context.BaseResource.Span)); continue; }
                trace.Add(new InferenceDecision(InferenceKind.Context, context.BaseResource.Text, baseUri!.ToString(), "lexical-FROM-base", context.BaseResource.Span));
                LowerStatements(context.Statements, current with { BaseUri = baseUri }, namedBases, grammar, language, commands, links, map, trace, diagnostics); implicitOutput = null; continue;
            }
            if (statement is SurfacePipelineSyntax pipeline) { implicitOutput = LowerPipeline(pipeline, current, namedBases, grammar, language, commands, links, map, trace, diagnostics); continue; }
            if (statement is not SurfaceCommandSyntax command) continue;
            if (ApplyDirective(command, ref current, namedBases, trace, diagnostics)) continue;
            SurfaceCommandSyntax effective = command;
            if (SurfaceDataLowering.IsDataStage(effective) && effective.NormalizedName != "JOIN" && effective.NormalizedName != "MATCH" && implicitOutput is null)
            { diagnostics.Add(new SurfaceDiagnostic("FLN263", $"{effective.Name} requires a previous pipeline/data value.", effective.Span)); continue; }
            if (effective.NormalizedName == "SAY" && effective.Values.Count == 0 && implicitOutput is not null) effective = InjectValue(effective, implicitOutput);
            IReadOnlyList<CommandSyntax> lowered = SurfaceDataLowering.IsDataStage(effective)
                ? LowerData(effective, implicitOutput ?? string.Empty, NextPipe(commands.Count), grammar, trace, diagnostics)
                : LowerCommand(effective, current, namedBases, grammar, language, trace, diagnostics);
            if (lowered.Count == 0) continue;
            Append(lowered, command.Span, current, grammar, commands, links, map);
            implicitOutput = ProducedVariable(effective, lowered) ?? implicitOutput;
        }
    }

    private string? LowerPipeline(SurfacePipelineSyntax pipeline, LoweringContext context, IDictionary<string, Uri> namedBases,
        PromptGrammar grammar, LanguageSnapshot language, List<CommandSyntax> commands, List<CommandLinkSyntax> links,
        List<SourceMapEntry> map, InferenceTrace trace, List<SurfaceDiagnostic> diagnostics)
    {
        string? value = null;
        foreach (SurfaceCommandSyntax stage in pipeline.Stages)
        {
            SurfaceCommandSyntax effective = stage;
            IReadOnlyList<CommandSyntax> lowered;
            if (SurfaceDataLowering.IsDataStage(stage))
            {
                if (value is null && stage.NormalizedName is not ("JOIN" or "MATCH")) { diagnostics.Add(new SurfaceDiagnostic("FLN251", $"Pipeline stage '{stage.Name}' has no previous value.", stage.Span)); return null; }
                string output = stage.Alias ?? NextPipe(commands.Count);
                lowered = LowerData(stage, value ?? string.Empty, output, grammar, trace, diagnostics);
            }
            else
            {
                if (value is not null && stage.NormalizedName == "SAY" && stage.Values.Count == 0) effective = InjectValue(stage, value);
                else if (value is not null && stage.NormalizedName is not "SAY") { diagnostics.Add(new SurfaceDiagnostic("FLN252", $"Pipeline stage '{stage.Name}' does not declare an implicit input role.", stage.Span)); return null; }
                lowered = LowerCommand(effective, context, namedBases, grammar, language, trace, diagnostics);
            }
            if (lowered.Count != 1) { diagnostics.Add(new SurfaceDiagnostic("FLN253", $"Pipeline stage '{stage.Name}' must lower to exactly one command; found {lowered.Count}.", stage.Span)); return null; }
            Append(lowered, stage.Span, context, grammar, commands, links, map);
            value = ProducedVariable(effective, lowered) ?? value;
        }
        return value;
    }

    private static IReadOnlyList<CommandSyntax> LowerData(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, InferenceTrace trace, ICollection<SurfaceDiagnostic> diagnostics)
    {
        CommandSyntax? command = SurfaceDataLowering.Lower(stage, input, output, grammar, diagnostics);
        if (command is null) return [];
        trace.Add(new InferenceDecision(InferenceKind.Dependency, stage.Name, output, "synthetic-pipeline-output", stage.Span));
        return [command];
    }

    private IReadOnlyList<CommandSyntax> LowerCommand(SurfaceCommandSyntax command, LoweringContext context,
        IReadOnlyDictionary<string, Uri> namedBases, PromptGrammar grammar, LanguageSnapshot language,
        InferenceTrace trace, ICollection<SurfaceDiagnostic> diagnostics)
    {
        SurfaceCommandSyntax resolved = ResolveCommandResources(command, context, namedBases, trace);
        if (resolved.NormalizedName == "SAY") return [LowerSay(resolved, grammar)];
        if (resolved.NormalizedName is "GET" or "LOAD") return LowerResources(resolved, grammar, language, trace, diagnostics,
            resolved.NormalizedName == "GET" ? ResourceReadIntent.Get : ResourceReadIntent.Load);
        diagnostics.Add(new SurfaceDiagnostic("FLN211", $"Surface command '{command.Name}' does not have a lowering rule yet.", command.Span));
        return [];
    }

    private IReadOnlyList<CommandSyntax> LowerResources(SurfaceCommandSyntax command, PromptGrammar grammar, LanguageSnapshot language,
        InferenceTrace trace, ICollection<SurfaceDiagnostic> diagnostics, ResourceReadIntent intent)
    {
        if (command.Values.Count == 0) { diagnostics.Add(new SurfaceDiagnostic(intent == ResourceReadIntent.Get ? "FLN230" : "FLN220", $"{command.Name} requires at least one resource.", command.Span)); return []; }
        if (command.Values.Count > 1 && command.Alias is not null) { diagnostics.Add(new SurfaceDiagnostic("FLN231", "AS can name only one explicit resource.", command.Span)); return []; }
        List<CommandSyntax> result = [];
        foreach (SurfaceValueSyntax value in command.Values)
        {
            ResourceDescriptor descriptor;
            try { descriptor = _inference.InferResource(value, language, trace); }
            catch (FormatException exception) { diagnostics.Add(new SurfaceDiagnostic("FLN232", exception.Message, value.Span)); continue; }
            string variable = command.Alias ?? descriptor.SuggestedVariableName;
            if (command.Alias is not null) trace.Add(new InferenceDecision(InferenceKind.VariableName, value.Text, variable, "explicit-AS", value.Span, InferenceConfidence.Explicit));
            IResourceProvider? provider;
            try { provider = _providers.Resolve(descriptor); }
            catch (LanguageDefinitionException exception) { diagnostics.Add(new SurfaceDiagnostic("FLN236", exception.Message, value.Span)); continue; }
            if (provider is null) { diagnostics.Add(new SurfaceDiagnostic("FLN236", $"No resource provider handles '{descriptor.Reference.DisplayName}'.", value.Span)); continue; }
            ResourceProviderResult lowered = provider.LowerRead(new ResourceProviderContext(descriptor, variable, command, value, grammar, intent));
            if (!lowered.IsSuccess) { diagnostics.Add(new SurfaceDiagnostic(lowered.ErrorCode!, lowered.ErrorMessage!, value.Span)); continue; }
            result.AddRange(lowered.Commands);
        }
        return result;
    }

    private static bool ApplyDirective(SurfaceCommandSyntax command, ref LoweringContext context, IDictionary<string, Uri> namedBases, InferenceTrace trace, ICollection<SurfaceDiagnostic> diagnostics)
    {
        switch (command.NormalizedName)
        {
            case "USE":
                if (command.Values.Count != 1 || string.IsNullOrWhiteSpace(command.Alias) || !Uri.TryCreate(command.Values[0].UnquotedText, UriKind.Absolute, out Uri? namedBase) || namedBase.Scheme is not ("http" or "https")) { diagnostics.Add(new SurfaceDiagnostic("FLN241", "USE requires one absolute HTTP(S) base and an AS alias.", command.Span)); return true; }
                namedBases[command.Alias] = namedBase; trace.Add(new InferenceDecision(InferenceKind.Context, command.Values[0].Text, command.Alias, "named-USE-base", command.Span, InferenceConfidence.Explicit)); return true;
            case "RETRY":
                if (command.Values.Count != 1 || !int.TryParse(command.Values[0].UnquotedText, out int retries) || retries < 0) { diagnostics.Add(new SurfaceDiagnostic("FLN242", "RETRY requires a non-negative integer.", command.Span)); return true; }
                context = context with { Retry = retries }; return true;
            case "TIMEOUT":
                if (command.Values.Count != 1 || string.IsNullOrWhiteSpace(command.Values[0].UnquotedText)) { diagnostics.Add(new SurfaceDiagnostic("FLN243", "TIMEOUT requires one duration.", command.Span)); return true; }
                context = context with { Timeout = command.Values[0].UnquotedText }; return true;
            case "AUTH": diagnostics.Add(new SurfaceDiagnostic("FLN244", "AUTH requires a secret provider/capability.", command.Span)); return true;
            default: return false;
        }
    }

    private static SurfaceCommandSyntax ResolveCommandResources(SurfaceCommandSyntax command, LoweringContext context, IReadOnlyDictionary<string, Uri> namedBases, InferenceTrace trace)
    {
        if (command.NormalizedName != "GET") return command;
        SurfaceValueSyntax[] resolved = command.Values.Select(value =>
        {
            string text = value.UnquotedText;
            if (Uri.TryCreate(text, UriKind.Absolute, out _) || text.Contains(':')) return value;
            int slash = text.IndexOf('/'); string prefix = slash < 0 ? text : text[..slash];
            if (namedBases.TryGetValue(prefix, out Uri? named)) { string relative = slash < 0 ? string.Empty : text[(slash + 1)..]; Uri uri = new(named.ToString().TrimEnd('/') + "/" + relative); trace.Add(new InferenceDecision(InferenceKind.Context, value.Text, uri.ToString(), $"named-base:{prefix}", value.Span)); return new SurfaceValueSyntax(uri.ToString(), value.Span); }
            if (context.BaseUri is not null) { Uri uri = new(context.BaseUri, text); trace.Add(new InferenceDecision(InferenceKind.Context, value.Text, uri.ToString(), "lexical-base-uri", value.Span)); return new SurfaceValueSyntax(uri.ToString(), value.Span); }
            return value;
        }).ToArray();
        return command with { Values = resolved };
    }

    private static void Append(IReadOnlyList<CommandSyntax> lowered, SourceSpan sourceSpan, LoweringContext context, PromptGrammar grammar,
        List<CommandSyntax> commands, List<CommandLinkSyntax> links, List<SourceMapEntry> map)
    {
        for (int offset = 0; offset < lowered.Count; offset++)
        {
            int index = commands.Count; commands.Add(WithPolicies(lowered[offset], context, grammar, sourceSpan.Start)); map.Add(new SourceMapEntry(index, "command", sourceSpan));
            if (offset > 0) links.Add(new CommandLinkSyntax(index - 1, index, CommandLinkKind.Parallel, Token("AND", PromptTokenKind.Word, sourceSpan.Start)));
        }
    }

    private static CommandSyntax WithPolicies(CommandSyntax command, LoweringContext context, PromptGrammar grammar, int position)
    {
        if (context.Retry is null && context.Timeout is null) return command;
        List<PromptToken> tokens = [.. command.AllTokens];
        if (context.Retry is int retries) { tokens.Add(Token("WITH", PromptTokenKind.Word, position)); tokens.Add(Token("RETRY", PromptTokenKind.Word, position)); tokens.Add(Token(retries.ToString(System.Globalization.CultureInfo.InvariantCulture), PromptTokenKind.Word, position)); }
        if (context.Timeout is string timeout) { tokens.Add(Token("WITH", PromptTokenKind.Word, position)); tokens.Add(Token("TIMEOUT", PromptTokenKind.Word, position)); tokens.Add(Token(timeout, PromptTokenKind.Word, position)); }
        return new CommandSyntax(tokens, grammar);
    }

    private static string? ProducedVariable(SurfaceCommandSyntax source, IReadOnlyList<CommandSyntax> lowered)
    {
        if (lowered.Count != 1 || source.NormalizedName == "SAY") return null;
        PromptToken? token = lowered[0].AllTokens.FirstOrDefault(item => item.Kind == PromptTokenKind.Variable);
        if (token is null) return null; string text = token.Text.TrimEnd('.');
        return text.Length >= 2 && text[0] == '[' && text[^1] == ']' ? text[1..^1] : null;
    }

    private static SurfaceCommandSyntax InjectValue(SurfaceCommandSyntax command, string variable) => command with { Values = [new SurfaceValueSyntax($"[{variable}]", command.Span)] };
    private static string NextPipe(int commandCount) => $"__pipe_{commandCount:D4}";
    private static CommandSyntax LowerSay(SurfaceCommandSyntax command, PromptGrammar grammar)
    {
        List<PromptToken> tokens = [Token("SAY", PromptTokenKind.Word, command.Span.Start)];
        foreach (SurfaceValueSyntax value in command.Values) { string text = SurfacePath(value.Text) ? $"\"{{{value.Text}}}\"" : value.Text; tokens.Add(Token(text, Classify(text), value.Span.Start)); }
        return new CommandSyntax(tokens, grammar);
    }
    private static bool SurfacePath(string text)
    {
        if (text.Length >= 2 && (text[0] is '"' or '\'') && text[^1] == text[0]) return false;
        try { ExpressionSyntax expression = ExpressionSyntaxParser.Parse(text); return expression is PropertyExpressionSyntax or IndexExpressionSyntax; } catch (FormatException) { return false; }
    }
    private static bool TryResolveBase(string text, Uri? parent, out Uri? uri)
    { if (Uri.TryCreate(text, UriKind.Absolute, out uri) && uri.Scheme is "http" or "https") return true; if (parent is not null && Uri.TryCreate(parent, text, out uri)) return true; uri = null; return false; }
    private static PromptToken Token(string text, PromptTokenKind kind, int start) => new(text, kind, Math.Max(0, start), 0);
    private static PromptTokenKind Classify(string text) => text.Length >= 2 && text[0] == '[' && text[^1] == ']' ? PromptTokenKind.Variable : text.Length >= 2 && text[0] == '{' && text[^1] == '}' ? PromptTokenKind.Reference : PromptTokenKind.Word;
    private sealed record LoweringContext(Uri? BaseUri, int? Retry, string? Timeout);
}
