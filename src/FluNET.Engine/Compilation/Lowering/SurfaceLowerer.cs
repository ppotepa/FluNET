using FluNET.Compilation.Inference;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt;
using FluNET.Prompt.Expressions;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

public sealed record LoweringResult(
    SourceDocument Document,
    SurfaceProgramSyntax SurfaceProgram,
    PromptSyntax CanonicalSyntax,
    SourceMap SourceMap,
    InferenceTrace InferenceTrace,
    IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public sealed class SurfaceLowerer
{
    private readonly InferenceEngine _inference;

    public SurfaceLowerer() : this(new InferenceEngine()) { }
    public SurfaceLowerer(InferenceEngine inference) =>
        _inference = inference ?? throw new ArgumentNullException(nameof(inference));

    public LoweringResult Lower(
        SurfaceParseResult parse,
        PromptGrammar grammar,
        LanguageSnapshot? language = null)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(grammar);
        language ??= StandardLanguage.CreateSnapshot();
        List<CommandSyntax> commands = [];
        List<CommandLinkSyntax> links = [];
        List<SourceMapEntry> map = [];
        List<SurfaceDiagnostic> diagnostics = [.. parse.Diagnostics];
        InferenceTrace trace = new();
        Dictionary<string, Uri> namedBases = new(StringComparer.OrdinalIgnoreCase);
        LowerStatements(parse.Program.Statements, new LoweringContext(null, null, null), namedBases,
            grammar, language, commands, links, map, trace, diagnostics);
        return new LoweringResult(parse.Document, parse.Program, new PromptSyntax(commands, links),
            new SourceMap(map), trace, diagnostics);
    }

    private void LowerStatements(
        IReadOnlyList<SurfaceStatementSyntax> statements,
        LoweringContext inherited,
        IDictionary<string, Uri> namedBases,
        PromptGrammar grammar,
        LanguageSnapshot language,
        List<CommandSyntax> commands,
        List<CommandLinkSyntax> links,
        List<SourceMapEntry> map,
        InferenceTrace trace,
        List<SurfaceDiagnostic> diagnostics)
    {
        LoweringContext current = inherited;
        string? implicitOutput = null;
        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfaceContextSyntax context)
            {
                if (!TryResolveBase(context.BaseResource.UnquotedText, current.BaseUri, out Uri? baseUri))
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN240",
                        $"FROM context base '{context.BaseResource.Text}' is not a valid absolute or inherited URI.",
                        context.BaseResource.Span));
                    continue;
                }
                trace.Add(new InferenceDecision(InferenceKind.Context, context.BaseResource.Text,
                    baseUri!.ToString(), "lexical-FROM-base", context.BaseResource.Span));
                LowerStatements(context.Statements, current with { BaseUri = baseUri }, namedBases,
                    grammar, language, commands, links, map, trace, diagnostics);
                implicitOutput = null;
                continue;
            }

            if (statement is SurfacePipelineSyntax pipeline)
            {
                implicitOutput = LowerPipeline(pipeline, current, namedBases, grammar, language,
                    commands, links, map, trace, diagnostics);
                continue;
            }

            if (statement is not SurfaceCommandSyntax command) continue;
            if (ApplyDirective(command, ref current, namedBases, trace, diagnostics)) continue;

            SurfaceCommandSyntax effective = command;
            if (effective.NormalizedName == "SAY" && effective.Values.Count == 0 && implicitOutput is not null)
            {
                effective = InjectValue(effective, implicitOutput);
                trace.Add(new InferenceDecision(InferenceKind.Dependency, "implicit-pipeline-value",
                    implicitOutput, "previous-statement-output", command.Span));
            }

            IReadOnlyList<CommandSyntax> lowered = LowerCommand(effective, current, namedBases,
                grammar, language, trace, diagnostics);
            if (lowered.Count == 0) continue;
            Append(lowered, command.Span, current, grammar, commands, links, map);
            implicitOutput = ProducedVariable(effective, lowered);
        }
    }

    private string? LowerPipeline(
        SurfacePipelineSyntax pipeline,
        LoweringContext context,
        IDictionary<string, Uri> namedBases,
        PromptGrammar grammar,
        LanguageSnapshot language,
        List<CommandSyntax> commands,
        List<CommandLinkSyntax> links,
        List<SourceMapEntry> map,
        InferenceTrace trace,
        List<SurfaceDiagnostic> diagnostics)
    {
        string? value = null;
        for (int stageIndex = 0; stageIndex < pipeline.Stages.Count; stageIndex++)
        {
            SurfaceCommandSyntax stage = pipeline.Stages[stageIndex];
            LoweringContext local = context;
            if (ApplyDirective(stage, ref local, namedBases, trace, diagnostics))
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN250",
                    $"Directive '{stage.Name}' cannot be used as a pipeline stage.", stage.Span));
                return null;
            }

            SurfaceCommandSyntax effective = stage;
            if (stageIndex > 0)
            {
                if (value is null)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN251",
                        $"Pipeline stage '{stage.Name}' has no previous value to consume.", stage.Span));
                    return null;
                }
                if (stage.NormalizedName == "SAY" && stage.Values.Count == 0)
                {
                    effective = InjectValue(stage, value);
                }
                else
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN252",
                        $"Pipeline stage '{stage.Name}' does not yet declare an implicit input role.", stage.Span));
                    return null;
                }
            }

            IReadOnlyList<CommandSyntax> lowered = LowerCommand(effective, context, namedBases,
                grammar, language, trace, diagnostics);
            if (lowered.Count != 1)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN253",
                    $"Pipeline stage '{stage.Name}' must lower to exactly one command; found {lowered.Count}.", stage.Span));
                return null;
            }
            Append(lowered, stage.Span, context, grammar, commands, links, map);
            string? produced = ProducedVariable(effective, lowered);
            if (stageIndex < pipeline.Stages.Count - 1 && produced is null &&
                effective.NormalizedName != "SAY")
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN254",
                    $"Pipeline stage '{stage.Name}' does not expose a value for the next stage.", stage.Span));
                return null;
            }
            value = produced;
        }
        return value;
    }

    private IReadOnlyList<CommandSyntax> LowerCommand(
        SurfaceCommandSyntax command,
        LoweringContext context,
        IReadOnlyDictionary<string, Uri> namedBases,
        PromptGrammar grammar,
        LanguageSnapshot language,
        InferenceTrace trace,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        SurfaceCommandSyntax resolved = ResolveCommandResources(command, context, namedBases, trace);
        IReadOnlyList<CommandSyntax> lowered = resolved.NormalizedName switch
        {
            "SAY" => [LowerSay(resolved, grammar)],
            "LOAD" => LowerLoad(resolved, grammar, language, trace, diagnostics),
            "GET" => LowerGet(resolved, grammar, language, trace, diagnostics),
            _ => []
        };
        if (lowered.Count == 0 && !diagnostics.Any(item => item.Span == command.Span))
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN211",
                $"Surface command '{command.Name}' does not have a lowering rule yet.", command.Span));
        }
        return lowered;
    }

    private static void Append(
        IReadOnlyList<CommandSyntax> lowered,
        SourceSpan sourceSpan,
        LoweringContext context,
        PromptGrammar grammar,
        List<CommandSyntax> commands,
        List<CommandLinkSyntax> links,
        List<SourceMapEntry> map)
    {
        for (int offset = 0; offset < lowered.Count; offset++)
        {
            int commandIndex = commands.Count;
            commands.Add(WithPolicies(lowered[offset], context, grammar, sourceSpan.Start));
            map.Add(new SourceMapEntry(commandIndex, "command", sourceSpan));
            if (offset > 0)
            {
                links.Add(Link(commandIndex - 1, commandIndex, CommandLinkKind.Parallel, sourceSpan.Start, "AND"));
            }
        }
    }

    private static string? ProducedVariable(
        SurfaceCommandSyntax source,
        IReadOnlyList<CommandSyntax> lowered)
    {
        if (lowered.Count != 1 || source.NormalizedName is not ("GET" or "LOAD")) return null;
        PromptToken? token = lowered[0].AllTokens.FirstOrDefault(item => item.Kind == PromptTokenKind.Variable);
        if (token is null) return null;
        string text = token.Text.TrimEnd('.');
        return text.Length >= 2 && text[0] == '[' && text[^1] == ']' ? text[1..^1] : null;
    }

    private static SurfaceCommandSyntax InjectValue(SurfaceCommandSyntax command, string variable) =>
        command with
        {
            Values = [new SurfaceValueSyntax($"[{variable}]", command.Span)]
        };

    private static bool ApplyDirective(
        SurfaceCommandSyntax command,
        ref LoweringContext context,
        IDictionary<string, Uri> namedBases,
        InferenceTrace trace,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        switch (command.NormalizedName)
        {
            case "USE":
                if (command.Values.Count != 1 || string.IsNullOrWhiteSpace(command.Alias) ||
                    !Uri.TryCreate(command.Values[0].UnquotedText, UriKind.Absolute, out Uri? namedBase) ||
                    namedBase.Scheme is not ("http" or "https"))
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN241",
                        "USE requires one absolute HTTP(S) base and an AS alias.", command.Span));
                    return true;
                }
                namedBases[command.Alias] = namedBase;
                trace.Add(new InferenceDecision(InferenceKind.Context, command.Values[0].Text,
                    command.Alias, "named-USE-base", command.Span, InferenceConfidence.Explicit));
                return true;
            case "RETRY":
                if (command.Values.Count != 1 || !int.TryParse(command.Values[0].UnquotedText, out int retries) || retries < 0)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN242", "RETRY requires a non-negative integer.", command.Span));
                    return true;
                }
                context = context with { Retry = retries };
                return true;
            case "TIMEOUT":
                if (command.Values.Count != 1 || string.IsNullOrWhiteSpace(command.Values[0].UnquotedText))
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN243", "TIMEOUT requires one duration.", command.Span));
                    return true;
                }
                context = context with { Timeout = command.Values[0].UnquotedText };
                return true;
            case "AUTH":
                diagnostics.Add(new SurfaceDiagnostic("FLN244",
                    "AUTH is reserved for the secret/capability provider batch.", command.Span));
                return true;
            default:
                return false;
        }
    }

    private static SurfaceCommandSyntax ResolveCommandResources(
        SurfaceCommandSyntax command,
        LoweringContext context,
        IReadOnlyDictionary<string, Uri> namedBases,
        InferenceTrace trace)
    {
        if (command.NormalizedName != "GET") return command;
        SurfaceValueSyntax[] resolved = command.Values.Select(value =>
        {
            string text = value.UnquotedText;
            if (Uri.TryCreate(text, UriKind.Absolute, out _)) return value;
            int slash = text.IndexOf('/');
            string prefix = slash < 0 ? text : text[..slash];
            if (namedBases.TryGetValue(prefix, out Uri? named))
            {
                string relative = slash < 0 ? string.Empty : text[(slash + 1)..];
                Uri uri = new(named.ToString().TrimEnd('/') + "/" + relative);
                trace.Add(new InferenceDecision(InferenceKind.Context, value.Text, uri.ToString(),
                    $"named-base:{prefix}", value.Span));
                return new SurfaceValueSyntax(uri.ToString(), value.Span);
            }
            if (context.BaseUri is not null && !text.StartsWith("env:", StringComparison.OrdinalIgnoreCase) &&
                !text.StartsWith("secret:", StringComparison.OrdinalIgnoreCase) &&
                !text.StartsWith("sql:", StringComparison.OrdinalIgnoreCase))
            {
                Uri uri = new(context.BaseUri, text);
                trace.Add(new InferenceDecision(InferenceKind.Context, value.Text, uri.ToString(),
                    "lexical-base-uri", value.Span));
                return new SurfaceValueSyntax(uri.ToString(), value.Span);
            }
            return value;
        }).ToArray();
        return command with { Values = resolved };
    }

    private IReadOnlyList<CommandSyntax> LowerGet(
        SurfaceCommandSyntax command, PromptGrammar grammar, LanguageSnapshot language,
        InferenceTrace trace, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count == 0)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN230", "GET requires at least one resource.", command.Span));
            return [];
        }
        if (command.Values.Count > 1 && command.Alias is not null)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN231", "AS can name only one explicit GET resource.", command.Span));
            return [];
        }
        List<CommandSyntax> result = [];
        foreach (SurfaceValueSyntax value in command.Values)
        {
            ResourceDescriptor descriptor;
            try { descriptor = _inference.InferResource(value, language, trace); }
            catch (FormatException exception)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN232", exception.Message, value.Span));
                continue;
            }
            string variable = OutputName(command, descriptor, value, trace);
            switch (descriptor.Reference)
            {
                case FileResourceReference:
                    result.AddRange(LowerLoad(new SurfaceCommandSyntax("LOAD", [value], command.Alias, command.Span),
                        grammar, language, trace, diagnostics));
                    break;
                case HttpResourceReference http when descriptor.Format == ResourceFormat.Json:
                    result.Add(new CommandSyntax([
                        Token("GETHTTP", PromptTokenKind.Word, command.Span.Start, Math.Min(7, command.Span.Length)),
                        Token($"[{variable}]", PromptTokenKind.Variable, value.Span.Start, value.Span.Length),
                        Token("FROM", PromptTokenKind.Word, value.Span.Start, 0),
                        Token($"{{{http.Uri}}}", PromptTokenKind.Reference, value.Span.Start, value.Span.Length)
                    ], grammar));
                    break;
                case HttpResourceReference:
                    diagnostics.Add(new SurfaceDiagnostic("FLN233",
                        $"Compact HTTP GET currently has a Json contract; inferred format was '{descriptor.Format}'.", value.Span));
                    break;
                case EnvironmentResourceReference environment:
                    result.Add(new CommandSyntax([
                        Token("GETENV", PromptTokenKind.Word, command.Span.Start, Math.Min(6, command.Span.Length)),
                        Token($"[{variable}]", PromptTokenKind.Variable, value.Span.Start, value.Span.Length),
                        Token("FROM", PromptTokenKind.Word, value.Span.Start, 0),
                        Token($"{{{environment.Name}}}", PromptTokenKind.Reference, value.Span.Start, value.Span.Length)
                    ], grammar));
                    break;
                case SecretResourceReference:
                    diagnostics.Add(new SurfaceDiagnostic("FLN234", "secret: resources require the secret capability/provider module.", value.Span));
                    break;
                case SqlResourceReference:
                    diagnostics.Add(new SurfaceDiagnostic("FLN235", "sql: resources require the SQL provider module.", value.Span));
                    break;
            }
        }
        return result;
    }

    private IReadOnlyList<CommandSyntax> LowerLoad(
        SurfaceCommandSyntax command, PromptGrammar grammar, LanguageSnapshot language,
        InferenceTrace trace, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count == 0)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN220", "LOAD requires at least one resource.", command.Span));
            return [];
        }
        if (command.Values.Count > 1 && command.Alias is not null)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN221",
                "AS on multiple explicit LOAD resources is reserved for collection/glob lowering.", command.Span));
            return [];
        }
        List<CommandSyntax> result = [];
        foreach (SurfaceValueSyntax value in command.Values)
        {
            ResourceDescriptor descriptor;
            try { descriptor = _inference.InferResource(value, language, trace); }
            catch (FormatException exception)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN222", exception.Message, value.Span));
                continue;
            }
            if (descriptor.Reference is not FileResourceReference file)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN223",
                    $"LOAD currently accepts local files; '{descriptor.Reference.Kind}' belongs to GET/resource providers.", value.Span));
                continue;
            }
            string variable = OutputName(command, descriptor, value, trace);
            if (file.IsPattern)
            {
                if (descriptor.Format != ResourceFormat.Json)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN225",
                        $"Glob LOAD currently supports JSON patterns; '{descriptor.Format}' needs a collection codec.", value.Span));
                    continue;
                }
                result.Add(new CommandSyntax([
                    Token("LOADGLOB", PromptTokenKind.Word, command.Span.Start, Math.Min(8, command.Span.Length)),
                    Token($"[{variable}]", PromptTokenKind.Variable, value.Span.Start, value.Span.Length),
                    Token("FROM", PromptTokenKind.Word, value.Span.Start, 0),
                    Token($"{{{file.Path}}}", PromptTokenKind.Reference, value.Span.Start, value.Span.Length)
                ], grammar));
                continue;
            }
            string qualifier = descriptor.Format switch
            {
                ResourceFormat.Json => "CONFIG",
                ResourceFormat.Text => "TEXT",
                _ => string.Empty
            };
            if (qualifier.Length == 0)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN224",
                    $"LOAD cannot infer a canonical decoder for format '{descriptor.Format}'. Use an explicit canonical command.", value.Span));
                continue;
            }
            result.Add(new CommandSyntax([
                Token("LOAD", PromptTokenKind.Word, command.Span.Start, 4),
                Token(qualifier, PromptTokenKind.Word, value.Span.Start, Math.Min(qualifier.Length, value.Span.Length)),
                Token($"[{variable}]", PromptTokenKind.Variable, value.Span.Start, value.Span.Length),
                Token("FROM", PromptTokenKind.Word, value.Span.Start, 0),
                Token($"{{{file.Path}}}", PromptTokenKind.Reference, value.Span.Start, value.Span.Length)
            ], grammar));
        }
        return result;
    }

    private static CommandSyntax WithPolicies(
        CommandSyntax command, LoweringContext context, PromptGrammar grammar, int position)
    {
        if (context.Retry is null && context.Timeout is null) return command;
        List<PromptToken> tokens = [.. command.AllTokens];
        if (context.Retry is int retries)
        {
            tokens.Add(Token("WITH", PromptTokenKind.Word, position, 0));
            tokens.Add(Token("RETRY", PromptTokenKind.Word, position, 0));
            tokens.Add(Token(retries.ToString(System.Globalization.CultureInfo.InvariantCulture), PromptTokenKind.Word, position, 0));
        }
        if (context.Timeout is string timeout)
        {
            tokens.Add(Token("WITH", PromptTokenKind.Word, position, 0));
            tokens.Add(Token("TIMEOUT", PromptTokenKind.Word, position, 0));
            tokens.Add(Token(timeout, PromptTokenKind.Word, position, 0));
        }
        return new CommandSyntax(tokens, grammar);
    }

    private static string OutputName(
        SurfaceCommandSyntax command, ResourceDescriptor descriptor,
        SurfaceValueSyntax value, InferenceTrace trace)
    {
        string variable = command.Alias ?? descriptor.SuggestedVariableName;
        if (command.Alias is not null)
        {
            trace.Add(new InferenceDecision(InferenceKind.VariableName, value.Text, variable,
                "explicit-AS", value.Span, InferenceConfidence.Explicit));
        }
        return variable;
    }

    private static CommandSyntax LowerSay(SurfaceCommandSyntax command, PromptGrammar grammar)
    {
        List<PromptToken> tokens = [Token("SAY", PromptTokenKind.Word, command.Span.Start, Math.Min(3, command.Span.Length))];
        foreach (SurfaceValueSyntax value in command.Values)
        {
            string text = SurfacePath(value.Text) ? $"\"{{{value.Text}}}\"" : value.Text;
            tokens.Add(Token(text, Classify(text), value.Span.Start, value.Span.Length));
        }
        return new CommandSyntax(tokens, grammar);
    }

    private static bool SurfacePath(string text)
    {
        if (text.Length >= 2 && (text[0] is '"' or '\'') && text[^1] == text[0]) return false;
        try
        {
            ExpressionSyntax expression = ExpressionSyntaxParser.Parse(text);
            return expression is PropertyExpressionSyntax or IndexExpressionSyntax;
        }
        catch (FormatException) { return false; }
    }

    private static bool TryResolveBase(string text, Uri? parent, out Uri? uri)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out uri) && uri.Scheme is "http" or "https") return true;
        if (parent is not null && Uri.TryCreate(parent, text, out uri)) return true;
        uri = null;
        return false;
    }

    private static CommandLinkSyntax Link(
        int predecessor, int successor, CommandLinkKind kind, int position, string text) =>
        new(predecessor, successor, kind, Token(text, PromptTokenKind.Word, position, 0));
    private static PromptToken Token(string text, PromptTokenKind kind, int start, int length) =>
        new(text, kind, Math.Max(0, start), Math.Max(0, length));
    private static PromptTokenKind Classify(string text) =>
        text.Length >= 2 && text[0] == '[' && text[^1] == ']' ? PromptTokenKind.Variable :
        text.Length >= 2 && text[0] == '{' && text[^1] == '}' ? PromptTokenKind.Reference : PromptTokenKind.Word;

    private sealed record LoweringContext(Uri? BaseUri, int? Retry, string? Timeout);
}
