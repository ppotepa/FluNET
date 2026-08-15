using FluNET.Compilation.Inference;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt;
using FluNET.Prompt.Expressions;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

public sealed class SurfaceLowerer
{
    private readonly InferenceEngine inference;
    private readonly IResourceProviderRegistry providers;

    public SurfaceLowerer() : this(new BuiltInProviderRegistry()) { }

    public SurfaceLowerer(IResourceProviderRegistry providers)
        : this(new InferenceEngine(), providers) { }

    public SurfaceLowerer(
        InferenceEngine inference,
        IResourceProviderRegistry providers)
    {
        this.inference = inference;
        this.providers = providers;
    }

    public LoweringResult Lower(
        SurfaceParseResult parse,
        PromptGrammar grammar) =>
        Lower(parse, grammar, StandardLanguage.CreateSnapshot());

    public LoweringResult Lower(
        SurfaceParseResult parse,
        PromptGrammar grammar,
        LanguageSnapshot language)
    {
        List<CommandSyntax> commands = [];
        List<CommandLinkSyntax> links = [];
        List<SourceMapEntry> sourceMap = [];
        List<SurfaceDiagnostic> diagnostics = [.. parse.Diagnostics];
        InferenceTrace trace = new();

        LowerStatements(
            parse.Program.Statements,
            new LoweringContext(null, null, null, null),
            new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase),
            grammar,
            language,
            commands,
            links,
            sourceMap,
            trace,
            diagnostics);

        return new(
            parse.Document,
            parse.Program,
            new PromptSyntax(commands, links),
            new SourceMap(sourceMap),
            trace,
            diagnostics);
    }

    private void LowerStatements(
        IReadOnlyList<SurfaceStatementSyntax> statements,
        LoweringContext inherited,
        IDictionary<string, Uri> namedBases,
        PromptGrammar grammar,
        LanguageSnapshot language,
        List<CommandSyntax> commands,
        List<CommandLinkSyntax> links,
        List<SourceMapEntry> sourceMap,
        InferenceTrace trace,
        List<SurfaceDiagnostic> diagnostics)
    {
        LoweringContext current = inherited;
        string? implicitOutput = null;

        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfaceContextSyntax context)
            {
                if (!TryResolveBase(
                    context.BaseResource.UnquotedText,
                    current.BaseUri,
                    out Uri? baseUri))
                {
                    diagnostics.Add(new(
                        "FLN240",
                        $"Invalid FROM base '{context.BaseResource.Text}'.",
                        context.BaseResource.Span));
                    continue;
                }

                LowerStatements(
                    context.Statements,
                    current with { BaseUri = baseUri },
                    namedBases,
                    grammar,
                    language,
                    commands,
                    links,
                    sourceMap,
                    trace,
                    diagnostics);
                implicitOutput = null;
                continue;
            }

            if (statement is SurfacePipelineSyntax pipeline)
            {
                implicitOutput = LowerPipeline(
                    pipeline,
                    current,
                    namedBases,
                    grammar,
                    language,
                    commands,
                    links,
                    sourceMap,
                    trace,
                    diagnostics);
                continue;
            }

            if (statement is not SurfaceCommandSyntax command)
                continue;

            if (ApplyDirective(command, ref current, namedBases, diagnostics))
                continue;

            SurfaceCommandSyntax effective = command;
            if (SurfaceDataLowering.IsDataStage(effective) &&
                !SurfaceDataLowering.HasExplicitInput(effective) &&
                implicitOutput is null)
            {
                diagnostics.Add(new(
                    "FLN263",
                    $"{effective.Name} requires a previous data value.",
                    effective.Span));
                continue;
            }

            if (effective.NormalizedName == "SAY" &&
                effective.Values.Count == 0 &&
                implicitOutput is not null)
            {
                effective = InjectValue(effective, implicitOutput);
            }

            IReadOnlyList<CommandSyntax> lowered = SurfaceDataLowering.IsDataStage(effective)
                ? LowerData(
                    effective,
                    implicitOutput ?? string.Empty,
                    effective.Alias ?? NextPipe(commands.Count),
                    grammar,
                    diagnostics)
                : LowerCommand(
                    effective,
                    current,
                    namedBases,
                    grammar,
                    language,
                    trace,
                    diagnostics);

            if (lowered.Count == 0)
                continue;

            Append(
                lowered,
                command.Span,
                current,
                grammar,
                commands,
                links,
                sourceMap);
            implicitOutput = ProducedVariable(effective, lowered) ?? implicitOutput;
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
        List<SourceMapEntry> sourceMap,
        InferenceTrace trace,
        List<SurfaceDiagnostic> diagnostics)
    {
        string? value = null;

        foreach (SurfaceCommandSyntax stage in pipeline.Stages)
        {
            SurfaceCommandSyntax effective = stage;
            IReadOnlyList<CommandSyntax> lowered;

            if (SurfaceDataLowering.IsDataStage(stage))
            {
                if (value is null && !SurfaceDataLowering.HasExplicitInput(stage))
                {
                    diagnostics.Add(new(
                        "FLN251",
                        $"Pipeline stage '{stage.Name}' has no previous value.",
                        stage.Span));
                    return null;
                }

                lowered = LowerData(
                    stage,
                    value ?? string.Empty,
                    stage.Alias ?? NextPipe(commands.Count),
                    grammar,
                    diagnostics);
            }
            else
            {
                if (value is not null &&
                    stage.NormalizedName == "SAY" &&
                    stage.Values.Count == 0)
                {
                    effective = InjectValue(stage, value);
                }
                else if (value is not null && stage.NormalizedName != "SAY")
                {
                    diagnostics.Add(new(
                        "FLN252",
                        $"Pipeline stage '{stage.Name}' does not declare implicit input.",
                        stage.Span));
                    return null;
                }

                lowered = LowerCommand(
                    effective,
                    context,
                    namedBases,
                    grammar,
                    language,
                    trace,
                    diagnostics);
            }

            if (lowered.Count != 1)
            {
                diagnostics.Add(new(
                    "FLN253",
                    $"Pipeline stage '{stage.Name}' must lower to one command.",
                    stage.Span));
                return null;
            }

            Append(
                lowered,
                stage.Span,
                context,
                grammar,
                commands,
                links,
                sourceMap);
            value = ProducedVariable(effective, lowered) ?? value;
        }

        return value;
    }

    private static IReadOnlyList<CommandSyntax> LowerData(
        SurfaceCommandSyntax syntax,
        string input,
        string output,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        CommandSyntax? lowered = SurfaceDataLowering.Lower(
            syntax,
            input,
            output,
            grammar,
            diagnostics);
        return lowered is null ? [] : [lowered];
    }

    private IReadOnlyList<CommandSyntax> LowerCommand(
        SurfaceCommandSyntax command,
        LoweringContext context,
        IDictionary<string, Uri> namedBases,
        PromptGrammar grammar,
        LanguageSnapshot language,
        InferenceTrace trace,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        SurfaceCommandSyntax resolved = ResolveCommandResources(
            command,
            context,
            namedBases,
            trace);

        if (resolved.NormalizedName == "SAY")
            return [LowerSay(resolved, grammar)];

        if (resolved.NormalizedName == "POST")
        {
            CommandSyntax? mutation = SurfaceMutationLowering.Post(
                resolved,
                grammar,
                diagnostics);
            return mutation is null ? [] : [mutation];
        }

        if (resolved.NormalizedName == "SAVE")
        {
            CommandSyntax? mutation = SurfaceMutationLowering.Save(
                resolved,
                grammar,
                diagnostics);
            return mutation is null ? [] : [mutation];
        }

        if (resolved.NormalizedName is "GET" or "LOAD")
        {
            return LowerResources(
                resolved,
                context,
                grammar,
                language,
                trace,
                diagnostics,
                resolved.NormalizedName == "GET"
                    ? ResourceReadIntent.Get
                    : ResourceReadIntent.Load);
        }

        diagnostics.Add(new(
            "FLN211",
            $"Surface command '{command.Name}' does not have a lowering rule yet.",
            command.Span));
        return [];
    }

    private IReadOnlyList<CommandSyntax> LowerResources(
        SurfaceCommandSyntax command,
        LoweringContext context,
        PromptGrammar grammar,
        LanguageSnapshot language,
        InferenceTrace trace,
        ICollection<SurfaceDiagnostic> diagnostics,
        ResourceReadIntent intent)
    {
        if (command.Values.Count == 0)
        {
            diagnostics.Add(new(
                "FLN230",
                $"{command.Name} requires a resource.",
                command.Span));
            return [];
        }

        List<CommandSyntax> result = [];
        foreach (SurfaceValueSyntax value in command.Values)
        {
            ResourceDescriptor descriptor;
            try
            {
                descriptor = inference.InferResource(value, language, trace);
            }
            catch (FormatException exception)
            {
                diagnostics.Add(new("FLN232", exception.Message, value.Span));
                continue;
            }

            string variable = command.Alias ?? descriptor.SuggestedVariableName;
            IResourceProvider? provider = providers.Resolve(descriptor);
            if (provider is null)
            {
                diagnostics.Add(new(
                    "FLN236",
                    $"No resource provider handles '{descriptor.Reference.DisplayName}'.",
                    value.Span));
                continue;
            }

            ResourceProviderResult lowered = provider.LowerRead(new(
                descriptor,
                variable,
                command,
                value,
                grammar,
                intent,
                context.AuthenticationSecret));
            if (!lowered.IsSuccess)
            {
                diagnostics.Add(new(
                    lowered.ErrorCode!,
                    lowered.ErrorMessage!,
                    value.Span));
                continue;
            }

            result.AddRange(lowered.Commands);
        }

        return result;
    }

    private static bool ApplyDirective(
        SurfaceCommandSyntax command,
        ref LoweringContext context,
        IDictionary<string, Uri> namedBases,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        switch (command.NormalizedName)
        {
            case "USE":
                if (command.Values.Count != 1 ||
                    command.Alias is null ||
                    !Uri.TryCreate(
                        command.Values[0].UnquotedText,
                        UriKind.Absolute,
                        out Uri? namedBase))
                {
                    diagnostics.Add(new(
                        "FLN241",
                        "USE requires absolute URI AS alias.",
                        command.Span));
                    return true;
                }

                namedBases[command.Alias] = namedBase;
                return true;

            case "RETRY":
                if (command.Values.Count == 1 &&
                    int.TryParse(command.Values[0].UnquotedText, out int retry) &&
                    retry >= 0)
                {
                    context = context with { Retry = retry };
                }
                else
                {
                    diagnostics.Add(new("FLN242", "Invalid RETRY.", command.Span));
                }
                return true;

            case "TIMEOUT":
                if (command.Values.Count == 1)
                {
                    context = context with
                    {
                        Timeout = command.Values[0].UnquotedText
                    };
                }
                else
                {
                    diagnostics.Add(new("FLN243", "Invalid TIMEOUT.", command.Span));
                }
                return true;

            case "AUTH":
                if (command.Values.Count == 1 &&
                    command.Values[0].UnquotedText.StartsWith(
                        "secret:",
                        StringComparison.OrdinalIgnoreCase) &&
                    command.Values[0].UnquotedText[7..].Trim().Length > 0)
                {
                    context = context with
                    {
                        AuthenticationSecret = command.Values[0].UnquotedText[7..].Trim()
                    };
                }
                else
                {
                    diagnostics.Add(new(
                        "FLN244",
                        "AUTH requires `AUTH secret:name`.",
                        command.Span));
                }
                return true;

            default:
                return false;
        }
    }

    private static SurfaceCommandSyntax ResolveCommandResources(
        SurfaceCommandSyntax command,
        LoweringContext context,
        IDictionary<string, Uri> namedBases,
        InferenceTrace trace)
    {
        if (command.NormalizedName != "GET")
            return command;

        return command with
        {
            Values = command.Values.Select(value =>
            {
                string text = value.UnquotedText;
                if (Uri.TryCreate(text, UriKind.Absolute, out _) || text.Contains(':'))
                    return value;

                int slash = text.IndexOf('/');
                string prefix = slash < 0 ? text : text[..slash];
                if (namedBases.TryGetValue(prefix, out Uri? namedBase))
                {
                    string resolved = new Uri(
                        namedBase.ToString().TrimEnd('/') + "/" +
                        (slash < 0 ? string.Empty : text[(slash + 1)..])).ToString();
                    trace.Add(new(
                        InferenceKind.Context,
                        text,
                        resolved,
                        "named-base-uri",
                        value.Span,
                        InferenceConfidence.Explicit));
                    return value with { Text = resolved };
                }

                if (context.BaseUri is not null)
                {
                    string resolved = new Uri(context.BaseUri, text).ToString();
                    trace.Add(new(
                        InferenceKind.Context,
                        text,
                        resolved,
                        "lexical-base-uri",
                        value.Span,
                        InferenceConfidence.Explicit));
                    return value with { Text = resolved };
                }

                return value;
            }).ToArray()
        };
    }

    private static void Append(
        IReadOnlyList<CommandSyntax> lowered,
        SourceSpan span,
        LoweringContext context,
        PromptGrammar grammar,
        List<CommandSyntax> commands,
        List<CommandLinkSyntax> links,
        List<SourceMapEntry> sourceMap)
    {
        for (int indexInStatement = 0;
             indexInStatement < lowered.Count;
             indexInStatement++)
        {
            int index = commands.Count;
            commands.Add(WithPolicies(
                lowered[indexInStatement],
                context,
                grammar,
                span.Start));
            sourceMap.Add(new(index, "command", span));

            if (indexInStatement > 0)
            {
                links.Add(new(
                    index - 1,
                    index,
                    CommandLinkKind.Parallel,
                    Token("AND", PromptTokenKind.Word, span.Start)));
            }
        }
    }

    private static CommandSyntax WithPolicies(
        CommandSyntax command,
        LoweringContext context,
        PromptGrammar grammar,
        int position)
    {
        if (context.Retry is null && context.Timeout is null)
            return command;

        List<PromptToken> tokens = [.. command.AllTokens];
        if (context.Retry is int retry)
        {
            tokens.Add(Token("WITH", PromptTokenKind.Word, position));
            tokens.Add(Token("RETRY", PromptTokenKind.Word, position));
            tokens.Add(Token(retry.ToString(), PromptTokenKind.Word, position));
        }

        if (context.Timeout is string timeout)
        {
            tokens.Add(Token("WITH", PromptTokenKind.Word, position));
            tokens.Add(Token("TIMEOUT", PromptTokenKind.Word, position));
            tokens.Add(Token(timeout, PromptTokenKind.Word, position));
        }

        return new(tokens, grammar);
    }

    private static string? ProducedVariable(
        SurfaceCommandSyntax syntax,
        IReadOnlyList<CommandSyntax> lowered)
    {
        if (lowered.Count != 1 ||
            syntax.NormalizedName is "SAY" or "POST" or "SAVE")
            return null;

        PromptToken? token = lowered[0].AllTokens
            .FirstOrDefault(item => item.Kind == PromptTokenKind.Variable);
        if (token is null)
            return null;

        string text = token.Text.TrimEnd('.');
        return text.Length >= 2 && text[0] == '[' && text[^1] == ']'
            ? text[1..^1]
            : null;
    }

    private static SurfaceCommandSyntax InjectValue(
        SurfaceCommandSyntax command,
        string variable) =>
        command with
        {
            Values = [new SurfaceValueSyntax($"[{variable}]", command.Span)]
        };

    private static string NextPipe(int commandCount) =>
        $"__pipe_{commandCount:D4}";

    private static CommandSyntax LowerSay(
        SurfaceCommandSyntax command,
        PromptGrammar grammar)
    {
        List<PromptToken> tokens =
        [
            Token("SAY", PromptTokenKind.Word, command.Span.Start)
        ];

        foreach (SurfaceValueSyntax value in command.Values)
        {
            string text = SurfacePath(value.Text)
                ? $"\"{{{value.Text}}}\""
                : value.Text;
            tokens.Add(Token(text, Classify(text), value.Span.Start));
        }

        return new(tokens, grammar);
    }

    private static bool SurfacePath(string text)
    {
        if (text.Length >= 2 &&
            text[0] is '"' or '\'' &&
            text[^1] == text[0])
            return false;

        try
        {
            return ExpressionSyntaxParser.Parse(text)
                is PropertyExpressionSyntax or IndexExpressionSyntax;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveBase(
        string text,
        Uri? parent,
        out Uri? resolved)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out resolved))
            return true;
        if (parent is not null && Uri.TryCreate(parent, text, out resolved))
            return true;
        resolved = null;
        return false;
    }

    private static PromptToken Token(
        string text,
        PromptTokenKind kind,
        int start) =>
        new(text, kind, Math.Max(0, start), 0);

    private static PromptTokenKind Classify(string text) =>
        text.Length >= 2 && text[0] == '[' && text[^1] == ']'
            ? PromptTokenKind.Variable
            : text.Length >= 2 && text[0] == '{' && text[^1] == '}'
                ? PromptTokenKind.Reference
                : PromptTokenKind.Word;

    private sealed record LoweringContext(
        Uri? BaseUri,
        int? Retry,
        string? Timeout,
        string? AuthenticationSecret);

    private sealed class BuiltInProviderRegistry : IResourceProviderRegistry
    {
        public IReadOnlyList<IResourceProvider> Providers { get; } =
        [
            new FileResourceProvider(),
            new HttpResourceProvider(),
            new EnvironmentResourceProvider(),
            new SecretResourceProvider(),
            new SqlResourceProvider()
        ];

        public IResourceProvider? Resolve(ResourceDescriptor descriptor) =>
            Providers.SingleOrDefault(provider => provider.CanHandle(descriptor));
    }
}
