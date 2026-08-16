using FluNET.Compilation.Inference;
using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt;
using FluNET.Prompt.Expressions;
using FluNET.Prompt.Surface;
using System.Text.RegularExpressions;

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
            new LoweringContext(null, null, null, null, null, null),
            new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase),
            grammar,
            language,
            commands,
            links,
            sourceMap,
            trace,
            diagnostics,
            parse.Document.Text);

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
        List<SurfaceDiagnostic> diagnostics,
        string source)
    {
        LoweringContext current = inherited;
        string? implicitOutput = null;
        int? previousStatementLast = null;
        int previousStatementWidth = 0;
        int previousStatementEnd = 0;
        CommandLinkKind? pendingConnector = null;

        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfaceIfSyntax conditional)
            {
                int trueStart = commands.Count;
                LowerStatements(
                    conditional.WhenTrue,
                    current with { Condition = CombineConditions(current.Condition, conditional.Condition) },
                    namedBases,
                    grammar,
                    language,
                    commands,
                    links,
                    sourceMap,
                    trace,
                    diagnostics,
                    source);

                if (previousStatementLast is int predecessor && trueStart < commands.Count)
                    links.Add(new(predecessor, trueStart, CommandLinkKind.Sequence, Token("THEN", PromptTokenKind.Word, conditional.Span.Start)));

                int falseStart = commands.Count;
                if (conditional.WhenFalse.Count > 0)
                {
                    LowerStatements(
                        conditional.WhenFalse,
                        current with { Condition = CombineConditions(current.Condition, $"NOT ({conditional.Condition})") },
                        namedBases,
                        grammar,
                        language,
                        commands,
                        links,
                        sourceMap,
                        trace,
                        diagnostics,
                        source);
                    if (trueStart < falseStart)
                        links.Add(new(falseStart - 1, falseStart, CommandLinkKind.Sequence, Token("THEN", PromptTokenKind.Word, conditional.Span.Start)));
                }

                if (commands.Count > trueStart)
                {
                    previousStatementLast = commands.Count - 1;
                    previousStatementWidth = commands.Count - trueStart;
                    previousStatementEnd = conditional.Span.End;
                    pendingConnector = null;
                    implicitOutput = null;
                }
                continue;
            }

            if (statement is SurfaceWhileSyntax whileLoop)
            {
                int loopStart = commands.Count;
                CommandSyntax loop = new([
                    Token("WHILELOOP", PromptTokenKind.Word, whileLoop.Span.Start),
                    Token("[loop]", PromptTokenKind.Variable, whileLoop.Span.Start),
                    Token("WHERE", PromptTokenKind.Word, whileLoop.Span.Start),
                    Token(whileLoop.Descriptor.Condition, PromptTokenKind.Word, whileLoop.Span.Start),
                    Token("FROM", PromptTokenKind.Word, whileLoop.Span.Start),
                    Token(whileLoop.Descriptor.Encode(), PromptTokenKind.Word, whileLoop.Span.Start)
                ], grammar);
                if (previousStatementLast is int predecessor)
                {
                    links.Add(new(predecessor, loopStart, CommandLinkKind.Sequence,
                        Token("THEN", PromptTokenKind.Word, whileLoop.Span.Start)));
                }
                Append([loop], whileLoop.Span, current, grammar, commands, links, sourceMap);
                previousStatementLast = commands.Count - 1;
                previousStatementWidth = 1;
                previousStatementEnd = whileLoop.Span.End;
                pendingConnector = null;
                implicitOutput = "loop";
                continue;
            }

            if (statement is SurfaceRepeatSyntax repeat)
            {
                int? previousRepeatLast = previousStatementLast;
                for (int iteration = 0; iteration < repeat.Count; iteration++)
                {
                    int repeatStart = commands.Count;
                    LowerStatements(
                        repeat.Statements,
                        current,
                        namedBases,
                        grammar,
                        language,
                        commands,
                        links,
                        sourceMap,
                        trace,
                        diagnostics,
                        source);

                    if (repeatStart >= commands.Count)
                        continue;

                    if (previousRepeatLast is int predecessor)
                    {
                        links.Add(new(
                            predecessor,
                            repeatStart,
                            CommandLinkKind.Sequence,
                            Token("THEN", PromptTokenKind.Word, repeat.Span.Start)));
                    }

                    previousRepeatLast = commands.Count - 1;
                }

                if (previousRepeatLast is int last)
                {
                    previousStatementLast = last;
                    previousStatementWidth = 2;
                    previousStatementEnd = repeat.Span.End;
                    pendingConnector = null;
                    implicitOutput = null;
                }
                continue;
            }

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
                    diagnostics,
                    source);
                implicitOutput = null;
                continue;
            }

            if (statement is SurfacePipelineSyntax pipeline)
            {
                int pipelineStart = commands.Count;
                string? pipelineOutput = LowerPipeline(
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

                if (pipelineOutput is null)
                    continue;

                int pipelineSeparatorStart = Math.Clamp(previousStatementEnd, 0, source.Length);
                int pipelineSeparatorEnd = Math.Clamp(pipeline.Span.Start, 0, source.Length);
                bool pipelineSeparatedBySemicolon = previousStatementLast is int &&
                    pipelineSeparatorEnd >= pipelineSeparatorStart &&
                    source[pipelineSeparatorStart..pipelineSeparatorEnd].Contains(';');
                if (previousStatementLast is int pipelinePrevious &&
                    pipelineStart < commands.Count &&
                    (pendingConnector is not null || previousStatementWidth > 1 && !pipelineSeparatedBySemicolon))
                {
                    links.Add(new(
                        pipelinePrevious,
                        pipelineStart,
                        pendingConnector ?? CommandLinkKind.Sequence,
                        Token(pendingConnector switch
                        {
                            CommandLinkKind.Parallel => "AND",
                            CommandLinkKind.Alternative => "ELSE",
                            _ => "THEN"
                        }, PromptTokenKind.Word, pipeline.Span.Start)));
                }

                previousStatementLast = commands.Count - 1;
                previousStatementWidth = commands.Count - pipelineStart;
                previousStatementEnd = pipeline.Span.End;
                pendingConnector = null;
                implicitOutput = pipelineOutput;
                continue;
            }

            if (statement is not SurfaceCommandSyntax command)
                continue;

            if (command.NormalizedName is "THEN" or "SEQUENCE" or "AND" or "PARALLEL" or "ELSE" or "OTHERWISE")
            {
                if (command.Values.Count > 0)
                {
                    diagnostics.Add(new("FLN247", $"{command.Name} cannot have values in a compact block.", command.Span));
                }
                else
                {
                    pendingConnector = command.NormalizedName switch
                    {
                        "THEN" or "SEQUENCE" => CommandLinkKind.Sequence,
                        "AND" or "PARALLEL" => CommandLinkKind.Parallel,
                        _ => CommandLinkKind.Alternative
                    };
                }
                continue;
            }

            if (ApplyDirective(command, ref current, namedBases, diagnostics))
                continue;

            SurfaceCommandSyntax effective = NormalizeSugar(command, implicitOutput);
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
                ? LowerDataWithTrace(
                    effective,
                    implicitOutput ?? string.Empty,
                    effective.Alias ?? NextPipe(commands.Count),
                    grammar,
                    trace,
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

            int separatorStart = Math.Clamp(previousStatementEnd, 0, source.Length);
            int separatorEnd = Math.Clamp(command.Span.Start, 0, source.Length);
            bool separatedBySemicolon = previousStatementLast is int &&
                separatorEnd >= separatorStart && source[separatorStart..separatorEnd].Contains(';');
            if (previousStatementLast is int previous &&
                (pendingConnector is not null || previousStatementWidth > 1 && !separatedBySemicolon))
            {
                links.Add(new(
                    previous,
                    commands.Count,
                    pendingConnector ?? CommandLinkKind.Sequence,
                    Token(pendingConnector switch
                    {
                        CommandLinkKind.Parallel => "AND",
                        CommandLinkKind.Alternative => "ELSE",
                        _ => "THEN"
                    }, PromptTokenKind.Word, command.Span.Start)));
            }

            Append(
                lowered,
                command.Span,
                current,
                grammar,
                commands,
                links,
                sourceMap);
            previousStatementLast = commands.Count - 1;
            previousStatementWidth = lowered.Count;
            previousStatementEnd = statement.Span.End;
            pendingConnector = null;
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
            SurfaceCommandSyntax effective = NormalizeSugar(stage, value);
            IReadOnlyList<CommandSyntax> lowered;

            if (SurfaceDataLowering.IsDataStage(effective))
            {
                if (value is null && !SurfaceDataLowering.HasExplicitInput(stage))
                {
                    diagnostics.Add(new(
                        "FLN251",
                        $"Pipeline stage '{stage.Name}' has no previous value.",
                        stage.Span));
                    return null;
                }

                lowered = LowerDataWithTrace(
                    effective,
                    value ?? string.Empty,
                    stage.Alias ?? NextPipe(commands.Count),
                    grammar,
                    trace,
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

    private static IReadOnlyList<CommandSyntax> LowerDataWithTrace(
        SurfaceCommandSyntax syntax,
        string input,
        string output,
        PromptGrammar grammar,
        InferenceTrace trace,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        trace.Add(new(
            InferenceKind.VariableName,
            syntax.Name,
            output,
            "synthetic-pipeline-output",
            syntax.Span));
        return LowerData(syntax, input, output, grammar, diagnostics);
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

        if (resolved.NormalizedName is "CAPABILITIES" or "PACKAGES" or "DOCTOR")
        {
            string output = resolved.Alias ?? resolved.Values.FirstOrDefault()?.UnquotedText ??
                (resolved.NormalizedName == "CAPABILITIES" ? "capabilities" :
                    resolved.NormalizedName == "PACKAGES" ? "packages" : "doctor");
            output = output.Trim().TrimStart('[').TrimEnd(']');
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN211", $"{resolved.Name} requires a valid output identifier.", resolved.Span));
                return [];
            }

            return [new CommandSyntax([
                Token(resolved.NormalizedName, PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "INDEX")
        {
            string phrase = string.Join(" ", resolved.Values.Select(value => value.UnquotedText)).Trim();
            Match indexMatch = Regex.Match(
                phrase,
                @"^(?:FILES\s+)?(?<output>\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_]*)\s+FROM\s+(?<root>.+?)(?:\s+RECURSIVE)?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!indexMatch.Success)
            {
                diagnostics.Add(new("FLN211", "INDEX requires `FILES [result] FROM path [RECURSIVE]`.", resolved.Span));
                return [];
            }

            string output = resolved.Alias ?? indexMatch.Groups["output"].Value.TrimStart('[').TrimEnd(']');
            string root = indexMatch.Groups["root"].Value.Trim();
            bool recursive = phrase.EndsWith(" RECURSIVE", StringComparison.OrdinalIgnoreCase);
            if (!IsIdentifier(output) || root.Length == 0)
            {
                diagnostics.Add(new("FLN211", "INDEX requires a valid output identifier and non-empty root.", resolved.Span));
                return [];
            }

            List<PromptToken> tokens = [
                Token("INDEX", PromptTokenKind.Word, resolved.Span.Start),
                Token("FILES", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(root.Trim().Trim('{', '}').Trim('"', '\''), resolved.Span.Start)
            ];
            if (recursive) tokens.Add(Token("RECURSIVE", PromptTokenKind.Word, resolved.Span.Start));
            return [new CommandSyntax(tokens, grammar)];
        }

        if (resolved.NormalizedName == "SAY")
            return [LowerSay(resolved, grammar)];

        if (resolved.NormalizedName is "TRIM" or "UPPER" or "UPPERCASE" or "LOWER" or "LOWERCASE" or "REPLACE" or "SPLIT" or "COMBINE" or "CONCATENATE" or "LINES" or "EXPECT")
        {
            CommandSyntax? text = LowerText(resolved, grammar, diagnostics);
            return text is null ? [] : [text];
        }

        if (resolved.NormalizedName == "NOTIFY")
        {
            CommandSyntax? notify = LowerNotify(resolved, grammar, diagnostics);
            return notify is null ? [] : [notify];
        }

        if (resolved.NormalizedName == "PUBLISH")
        {
            CommandSyntax? publish = SurfaceMutationLowering.Publish(resolved, grammar, diagnostics);
            return publish is null ? [] : [publish];
        }

        if (resolved.NormalizedName == "RECEIVE")
        {
            CommandSyntax? receive = LowerReceive(resolved, grammar, diagnostics);
            return receive is null ? [] : [receive];
        }

        if (resolved.NormalizedName == "SCAN")
        {
            return LowerScan(resolved, grammar, diagnostics);
        }

        if (resolved.NormalizedName == "SEARCH")
        {
            CommandSyntax? search = LowerSearch(resolved, grammar, diagnostics);
            return search is null ? [] : [search];
        }

        if (resolved.NormalizedName == "LIST")
        {
            string listPhrase = string.Join(" ", resolved.Values.Select(value => value.UnquotedText)).Trim();
            if (Regex.IsMatch(listPhrase, "^ARCHIVE(?:\\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return LowerArchiveListing(resolved, grammar, diagnostics);
            }
            if (Regex.IsMatch(listPhrase, "^BLOB(?:S)?(?:\\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return LowerBlobListing(resolved, grammar, diagnostics);
            }
            if (Regex.IsMatch(listPhrase, "^STORE(?:S)?(?:\\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return LowerKeyValueListing(resolved, grammar, diagnostics);
            }
            return LowerList(resolved, grammar, diagnostics);
        }

        if (resolved.NormalizedName == "STAT")
        {
            CommandSyntax? stat = LowerStat(resolved, grammar, diagnostics);
            return stat is null ? [] : [stat];
        }

        if (resolved.NormalizedName == "HASH")
        {
            CommandSyntax? hash = LowerHash(resolved, grammar, diagnostics);
            return hash is null ? [] : [hash];
        }

        if (resolved.NormalizedName == "SYSTEM" &&
            resolved.Values.Count == 1 &&
            string.Equals(resolved.Values[0].UnquotedText.Trim(), "INFO", StringComparison.OrdinalIgnoreCase))
        {
            return [new CommandSyntax([
                Token("SYSTEMINFO", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{resolved.Alias ?? "system"}]", PromptTokenKind.Variable, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "METRICS")
        {
            return [new CommandSyntax([
                Token("SYSTEMMETRICS", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{resolved.Alias ?? "metrics"}]", PromptTokenKind.Variable, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "PATH")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN347", "PATH requires a special path name, for example `PATH TEMP AS temp`.", resolved.Span));
                return [];
            }

            string name = resolved.Values[0].UnquotedText.Trim().Trim('"', '\'');
            string output = resolved.Alias ?? "path";
            if (name.Length == 0 || !IsIdentifier(output))
            {
                diagnostics.Add(new("FLN348", "PATH requires a non-empty name and a valid output identifier.", resolved.Span));
                return [];
            }

            return [new CommandSyntax([
                Token("PATHVALUE", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(name, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "TEMP")
        {
            if (resolved.Values.Count is < 1 or > 2)
            {
                diagnostics.Add(new("FLN349", "TEMP expects FILE or DIRECTORY, optionally followed by a file suffix, for example `TEMP FILE .json AS artifact`.", resolved.Span));
                return [];
            }

            string tempPhrase = string.Join(" ", resolved.Values.Select(value => value.UnquotedText)).Trim();
            Match tempMatch = Regex.Match(tempPhrase, @"^(FILE|DIRECTORY)(?:\s+(.+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            bool directory = tempMatch.Success && string.Equals(tempMatch.Groups[1].Value, "DIRECTORY", StringComparison.OrdinalIgnoreCase);
            bool file = tempMatch.Success && string.Equals(tempMatch.Groups[1].Value, "FILE", StringComparison.OrdinalIgnoreCase);
            if (!tempMatch.Success || directory && tempMatch.Groups[2].Success)
            {
                diagnostics.Add(new("FLN349", "TEMP expects FILE or DIRECTORY; only FILE may have a suffix.", resolved.Span));
                return [];
            }

            string output = resolved.Alias ?? (directory ? "workspace" : "artifact");
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN350", $"TEMP output '{output}' is not a valid identifier.", resolved.Span));
                return [];
            }

            string verb = directory ? "CREATETEMPDIR" : "CREATETEMPFILE";
            List<PromptToken> tokens = [
                Token(verb, PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start)
            ];
            if (file && tempMatch.Groups[2].Success)
            {
                string suffix = tempMatch.Groups[2].Value.Trim().Trim('"', '\'');
                if (suffix.Length == 0)
                {
                    diagnostics.Add(new("FLN350", "TEMP FILE suffix cannot be empty.", resolved.Span));
                    return [];
                }
                tokens.Add(Token("FROM", PromptTokenKind.Word, resolved.Span.Start));
                tokens.Add(Token(suffix, PromptTokenKind.Word, resolved.Values[^1].Span.Start));
            }
            return [new CommandSyntax(tokens, grammar)];
        }

        if (resolved.NormalizedName == "CLEANUP")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN351", "CLEANUP requires one path produced by TEMP, for example `CLEANUP [artifact]`.", resolved.Span));
                return [];
            }

            string target = resolved.Values[0].UnquotedText.Trim();
            if (target.Length == 0)
            {
                diagnostics.Add(new("FLN351", "CLEANUP target cannot be empty.", resolved.Span));
                return [];
            }

            string output = resolved.Alias ?? "cleanup";
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN352", $"CLEANUP output '{output}' is not a valid identifier.", resolved.Span));
                return [];
            }

            return [new CommandSyntax([
                Token("CLEANUPTEMP", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(target, resolved.Values[0].Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "DELETE")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN343", "DELETE requires a blob key, for example `DELETE blob:reports/latest AS removed`.", resolved.Span));
                return [];
            }

            string target = resolved.Values[0].UnquotedText.Trim().Trim('"', '\'');
            if (target.StartsWith("store:", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("STORE ", StringComparison.OrdinalIgnoreCase))
            {
                string storeKey = target.StartsWith("store:", StringComparison.OrdinalIgnoreCase)
                    ? target["store:".Length..].Trim()
                    : target["STORE ".Length..].Trim().Trim('"', '\'');
                return [new CommandSyntax([
                    Token("DELETEVALUE", PromptTokenKind.Word, resolved.Span.Start),
                    Token($"[{resolved.Alias ?? "deleted"}]", PromptTokenKind.Variable, resolved.Span.Start),
                    Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                    SurfaceReferenceToken(storeKey, resolved.Span.Start)
                ], grammar)];
            }
            if (!target.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
                {
                    List<PromptToken> deleteTokens = [
                        Token("DELETEHTTP", PromptTokenKind.Word, resolved.Span.Start),
                        Token($"[{resolved.Alias ?? "deleted"}]", PromptTokenKind.Variable, resolved.Span.Start),
                        Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                        Token($"{{{uri}}}", PromptTokenKind.Reference, resolved.Span.Start)
                    ];
                    deleteTokens.AddRange(CredentialTokens(context.AuthenticationSecret, resolved.Span.Start));
                    return [new CommandSyntax(deleteTokens, grammar)];
                }

                diagnostics.Add(new("FLN344", "DELETE supports blob: or HTTP(S) targets; use TRASH for local files.", resolved.Span));
                return [];
            }

            string key = target["blob:".Length..].Trim();
            string output = resolved.Alias ?? "deleted";
            if (key.Length == 0 || !IsIdentifier(output))
            {
                diagnostics.Add(new("FLN343", "DELETE requires a non-empty blob key and a valid output name.", resolved.Span));
                return [];
            }

            return [new CommandSyntax([
                Token("DELETEBLOB", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                Token($"{{{key}}}", PromptTokenKind.Reference, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "NOW")
        {
            if (resolved.Values.Count != 0)
            {
                diagnostics.Add(new("FLN346", "NOW does not take arguments; use `NOW AS timestamp`.", resolved.Span));
                return [];
            }

            string output = resolved.Alias ?? "now";
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN347", $"NOW output '{output}' is not a valid identifier.", resolved.Span));
                return [];
            }

            return [new CommandSyntax([
                Token("NOW", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "WAIT")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN348", "WAIT requires a duration, for example `WAIT 250ms`.", resolved.Span));
                return [];
            }

            string output = resolved.Alias ?? "waited";
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN349", $"WAIT output '{output}' is not a valid identifier.", resolved.Span));
                return [];
            }

            SurfaceValueSyntax value = resolved.Values[0];
            string duration = value.UnquotedText.Trim();
            PromptToken durationToken = duration.StartsWith("[", StringComparison.Ordinal) && duration.EndsWith(']')
                ? Token(duration, PromptTokenKind.Variable, value.Span.Start)
                : Token(duration, PromptTokenKind.Word, value.Span.Start);
            return [new CommandSyntax([
                Token("WAIT", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                durationToken
            ], grammar)];
        }

        if (resolved.NormalizedName is "COPY" or "MOVE")
        {
            if (resolved.NormalizedName == "COPY" &&
                resolved.Values.Count == 1)
            {
                string copyText = resolved.Values[0].UnquotedText.Trim();
                int clipboardSeparator = copyText.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
                if (clipboardSeparator > 0 &&
                    copyText[(clipboardSeparator + 4)..].Trim().Trim('"', '\'')
                        .Equals("CLIPBOARD", StringComparison.OrdinalIgnoreCase))
                {
                    string output = resolved.Alias ?? "clipboard";
                    if (!IsIdentifier(output))
                    {
                        diagnostics.Add(new("FLN313", $"COPY output '{output}' is not a valid identifier.", resolved.Span));
                        return [];
                    }

                    string source = copyText[..clipboardSeparator].Trim();
                    PromptToken sourceToken = source.StartsWith("[", StringComparison.Ordinal) && source.EndsWith(']')
                        ? Token(source, PromptTokenKind.Variable, resolved.Span.Start)
                        : Token(source, Classify(source), resolved.Span.Start);
                    return [new CommandSyntax([
                        Token("WRITECLIPBOARD", PromptTokenKind.Word, resolved.Span.Start),
                        Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                        Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                        sourceToken
                    ], grammar)];
                }
            }

            CommandSyntax? transfer = LowerFileTransfer(resolved, grammar, diagnostics);
            return transfer is null ? [] : [transfer];
        }

        if (resolved.NormalizedName == "TRASH")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN293", "TRASH requires a file path or `DIRECTORY` path, for example `TRASH DIRECTORY \"./old\" AS item`.", resolved.Span));
                return [];
            }

            string output = resolved.Alias ?? "trashed";
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN294", $"TRASH output '{output}' is not a valid identifier.", resolved.Span));
                return [];
            }

            string rawSource = resolved.Values[0].UnquotedText.Trim();
            bool directory = Regex.IsMatch(rawSource, @"^DIRECTORY\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            string source = (directory ? rawSource["DIRECTORY".Length..] : rawSource).Trim().Trim('"', '\'');
            return [new CommandSyntax([
                Token(directory ? "TRASHDIRECTORY" : "TRASHFILE", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                Token($"{{{source}}}", PromptTokenKind.Reference, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "RESTORE")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN355", "RESTORE requires `trash-item TO destination AS result`.", resolved.Span));
                return [];
            }

            string phrase = resolved.Values[0].UnquotedText.Trim();
            bool directory = Regex.IsMatch(phrase, @"^DIRECTORY\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (directory) phrase = phrase["DIRECTORY".Length..].Trim();
            int separator = phrase.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
            if (separator <= 0 || separator + 4 >= phrase.Length)
            {
                diagnostics.Add(new("FLN355", "RESTORE requires `trash-item TO destination AS result`.", resolved.Span));
                return [];
            }

            string source = phrase[..separator].Trim().Trim('"', '\'');
            string destination = phrase[(separator + 4)..].Trim().Trim('"', '\'');
            string output = resolved.Alias ?? (directory ? "restoredDirectory" : "restored");
            if (source.Length == 0 || destination.Length == 0 || !IsIdentifier(output))
            {
                diagnostics.Add(new("FLN356", "RESTORE requires non-empty paths and a valid output identifier.", resolved.Span));
                return [];
            }

            return [new CommandSyntax([
                Token(directory ? "RESTOREDIRECTORY" : "RESTOREFILE", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(source, resolved.Span.Start),
                Token("TO", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(destination, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "STORE")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN295", "STORE requires `key = value AS result`.", resolved.Span));
                return [];
            }

            string source = resolved.Values[0].UnquotedText.Trim();
            int equals = source.IndexOf('=');
            if (equals <= 0 || equals == source.Length - 1)
            {
                diagnostics.Add(new("FLN295", "STORE requires `key = value AS result`.", resolved.Span));
                return [];
            }

            string key = source[..equals].Trim().Trim('"', '\'');
            string value = source[(equals + 1)..].Trim().Trim('"', '\'');
            string output = resolved.Alias ?? "stored";
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN296", $"STORE output '{output}' is not a valid identifier.", resolved.Span));
                return [];
            }

            return [new CommandSyntax([
                Token("PUTVALUE", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(key, resolved.Span.Start),
                Token("USING", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(value, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "READ")
        {
            if (resolved.Values.Count == 1 &&
                resolved.Values[0].UnquotedText.Trim().Trim('"', '\'')
                    .Equals("CLIPBOARD", StringComparison.OrdinalIgnoreCase))
            {
                string clipboardOutput = resolved.Alias ?? "clipboard";
                if (!IsIdentifier(clipboardOutput))
                {
                    diagnostics.Add(new("FLN312", $"READ output '{clipboardOutput}' is not a valid identifier.", resolved.Span));
                    return [];
                }

                return [new CommandSyntax([
                    Token("READCLIPBOARD", PromptTokenKind.Word, resolved.Span.Start),
                    Token($"[{clipboardOutput}]", PromptTokenKind.Variable, resolved.Span.Start)
                ], grammar)];
            }

            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN297", "READ requires a storage key, for example `READ \"theme\" AS theme`.", resolved.Span));
                return [];
            }

            string key = resolved.Values[0].UnquotedText.Trim().Trim('"', '\'');
            string output = resolved.Alias ?? "value";
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN298", $"READ output '{output}' is not a valid identifier.", resolved.Span));
                return [];
            }

            return [new CommandSyntax([
                Token("READVALUE", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(key, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "EXECUTE")
        {
            if (resolved.Values.Count is < 1 or > 2)
            {
                diagnostics.Add(new("FLN299", "EXECUTE accepts a command and optional working directory, for example `EXECUTE \"dotnet --info\" IN \"./tools\" AS result`.", resolved.Span));
                return [];
            }

            string commandLine = resolved.Values[0].UnquotedText.Trim();
            string? workingDirectory = resolved.Values.Count == 2
                ? resolved.Values[1].UnquotedText.Trim()
                : null;
            string? environment = null;
            if (workingDirectory is null && TrySplitTrailingProcessEnvironment(commandLine, out string commandWithoutEnvironment, out string environmentText))
            {
                commandLine = commandWithoutEnvironment;
                environment = environmentText;
            }
            if (workingDirectory is null && TrySplitTrailingProcessDirectory(commandLine, out string commandWithoutDirectory, out string directory))
            {
                commandLine = commandWithoutDirectory;
                workingDirectory = directory;
            }
            if (workingDirectory is not null && TrySplitTrailingProcessEnvironment(workingDirectory, out string directoryWithoutEnvironment, out string directoryEnvironment))
            {
                workingDirectory = directoryWithoutEnvironment;
                environment = directoryEnvironment;
            }
            string[] parts = SplitProcessCommandLine(commandLine);
            if (parts.Length == 0)
            {
                diagnostics.Add(new("FLN299", "EXECUTE requires a non-empty executable.", resolved.Span));
                return [];
            }

            string output = resolved.Alias ?? "process";
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN300", $"EXECUTE output '{output}' is not a valid identifier.", resolved.Span));
                return [];
            }

            List<PromptToken> tokens = [
                Token("RUNPROCESS", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(parts[0], resolved.Span.Start)
            ];
            if (parts.Length > 1)
            {
                tokens.Add(Token("USING", PromptTokenKind.Word, resolved.Span.Start));
                tokens.Add(SurfaceReferenceToken(string.Join(' ', parts.Skip(1)), resolved.Span.Start));
            }
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                tokens.Add(Token("IN", PromptTokenKind.Word, resolved.Span.Start));
                tokens.Add(SurfaceReferenceToken(workingDirectory, resolved.Span.Start));
            }
            if (!string.IsNullOrWhiteSpace(environment))
            {
                tokens.Add(Token("ENV", PromptTokenKind.Word, resolved.Span.Start));
                tokens.Add(SurfaceReferenceToken(environment, resolved.Span.Start));
            }
            return [new CommandSyntax(tokens, grammar)];
        }

        if (resolved.NormalizedName == "START")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN301", "START requires a quoted executable and optional arguments, for example `START \"dotnet watch\" AS session`.", resolved.Span));
                return [];
            }
            string commandLine = resolved.Values[0].UnquotedText.Trim();
            string? workingDirectory = null;
            string? environment = null;
            if (TrySplitTrailingProcessEnvironment(commandLine, out string commandWithoutEnvironment, out string environmentText))
            {
                commandLine = commandWithoutEnvironment;
                environment = environmentText;
            }
            if (TrySplitTrailingProcessDirectory(commandLine, out string commandWithoutDirectory, out string directory))
            {
                commandLine = commandWithoutDirectory;
                workingDirectory = directory;
            }
            string[] parts = SplitProcessCommandLine(commandLine);
            if (parts.Length == 0)
            {
                diagnostics.Add(new("FLN301", "START requires a non-empty executable.", resolved.Span));
                return [];
            }
            string output = resolved.Alias ?? "session";
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN302", $"START output '{output}' is not a valid identifier.", resolved.Span));
                return [];
            }
            List<PromptToken> tokens = [
                Token("STARTPROCESS", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(parts[0], resolved.Span.Start)
            ];
            if (parts.Length > 1)
            {
                tokens.Add(Token("USING", PromptTokenKind.Word, resolved.Span.Start));
                tokens.Add(SurfaceReferenceToken(string.Join(' ', parts.Skip(1)), resolved.Span.Start));
            }
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                tokens.Add(Token("IN", PromptTokenKind.Word, resolved.Span.Start));
                tokens.Add(SurfaceReferenceToken(workingDirectory, resolved.Span.Start));
            }
            if (!string.IsNullOrWhiteSpace(environment))
            {
                tokens.Add(Token("ENV", PromptTokenKind.Word, resolved.Span.Start));
                tokens.Add(SurfaceReferenceToken(environment, resolved.Span.Start));
            }
            return [new CommandSyntax(tokens, grammar)];
        }

        if (resolved.NormalizedName == "SEND")
        {
            string phrase = string.Join(" ", resolved.Values.Select(value => value.UnquotedText)).Trim();
            int separator = phrase.LastIndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
            if (separator <= 0 || separator + 4 >= phrase.Length)
            {
                diagnostics.Add(new("FLN303", "SEND requires input and a session, for example `SEND \"status\" TO session AS response`.", resolved.Span));
                return [];
            }
            string input = phrase[..separator].Trim().Trim('"', '\'');
            string session = phrase[(separator + 4)..].Trim().Trim('"', '\'');
            string output = resolved.Alias ?? "response";
            if (input.Length == 0 || session.Length == 0 || !IsIdentifier(output))
            {
                diagnostics.Add(new("FLN304", "SEND requires non-empty input, session and a valid output identifier.", resolved.Span));
                return [];
            }
            PromptToken sessionToken = session.StartsWith("[", StringComparison.Ordinal) && session.EndsWith(']')
                ? Token(session, PromptTokenKind.Variable, resolved.Span.Start)
                : Token($"[{session}]", PromptTokenKind.Variable, resolved.Span.Start);
            return [new CommandSyntax([
                Token("SENDPROCESS", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                sessionToken,
                Token("USING", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(input, resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "STOP")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN305", "STOP requires a process session, for example `STOP session AS result`.", resolved.Span));
                return [];
            }
            string session = resolved.Values[0].UnquotedText.Trim().Trim('"', '\'');
            string output = resolved.Alias ?? "stopped";
            if (session.Length == 0 || !IsIdentifier(output))
            {
                diagnostics.Add(new("FLN306", "STOP requires a session and a valid output identifier.", resolved.Span));
                return [];
            }
            PromptToken sessionToken = session.StartsWith("[", StringComparison.Ordinal) && session.EndsWith(']')
                ? Token(session, PromptTokenKind.Variable, resolved.Span.Start)
                : Token($"[{session}]", PromptTokenKind.Variable, resolved.Span.Start);
            return [new CommandSyntax([
                Token("STOPPROCESS", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                sessionToken
            ], grammar)];
        }

        if (resolved.NormalizedName is "PACK" or "UNPACK")
        {
            CommandSyntax? archive = LowerArchive(resolved, grammar, diagnostics);
            return archive is null ? [] : [archive];
        }

        if (resolved.NormalizedName == "MKDIR")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN303", "MKDIR requires a directory path, for example `MKDIR \"./reports\" AS directory`.", resolved.Span));
                return [];
            }

            string output = resolved.Alias ?? "directory";
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN304", $"MKDIR output '{output}' is not a valid identifier.", resolved.Span));
                return [];
            }

            return [new CommandSyntax([
                Token("CREATEDIRECTORY", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(resolved.Values[0].UnquotedText.Trim().Trim('"', '\''), resolved.Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "LET")
        {
            CommandSyntax? let = LowerLet(resolved, grammar, diagnostics);
            return let is null ? [] : [let];
        }

        if (resolved.NormalizedName == "SET")
        {
            string setPhrase = string.Join(" ", resolved.Values.Select(value => value.UnquotedText)).Trim();
            Match environmentMatch = Regex.Match(
                setPhrase,
                @"^ENV\s+(.+?)\s+TO\s+(.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (environmentMatch.Success)
            {
                string name = environmentMatch.Groups[1].Value.Trim().Trim('"', '\'');
                string value = environmentMatch.Groups[2].Value.Trim();
                string output = resolved.Alias ?? "environment";
                if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
                {
                    diagnostics.Add(new("FLN353", $"Environment name '{name}' is not valid.", resolved.Span));
                    return [];
                }
                if (value.Length == 0 || !IsIdentifier(output))
                {
                    diagnostics.Add(new("FLN354", "SET ENV requires a non-empty value and a valid output identifier.", resolved.Span));
                    return [];
                }
                return [new CommandSyntax([
                    Token("SETENV", PromptTokenKind.Word, resolved.Span.Start),
                    Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                    Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                    Token($"{{{name}}}", PromptTokenKind.Reference, resolved.Span.Start),
                    Token("TO", PromptTokenKind.Word, resolved.Span.Start),
                    Token(value, Classify(value), resolved.Span.Start)
                ], grammar)];
            }
        }

        if (resolved.NormalizedName == "POST")
        {
            CommandSyntax? mutation = SurfaceMutationLowering.Post(
                resolved,
                grammar,
                diagnostics,
                context.AuthenticationSecret);
            return mutation is null ? [] : [mutation];
        }

        if (resolved.NormalizedName == "PARSE")
        {
            if (TryLowerBatchJsonParse(
                resolved,
                grammar,
                language,
                trace,
                diagnostics,
                out IReadOnlyList<CommandSyntax>? batch))
            {
                return batch;
            }

            if (resolved.Values.Count != 4 ||
                !resolved.Values[0].UnquotedText.Equals("JSON", StringComparison.OrdinalIgnoreCase) ||
                !resolved.Values[2].UnquotedText.Equals("FROM", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new(
                    "FLN356",
                    "PARSE expects `PARSE source AS result` or `PARSE JSON source AS result`.",
                    resolved.Span));
                return [];
            }

            string output = resolved.Values[1].UnquotedText.Trim();
            if (!IsIdentifier(output.Trim('[', ']')))
            {
                diagnostics.Add(new("FLN357", "PARSE requires a valid result identifier.", resolved.Values[1].Span));
                return [];
            }

            return [new CommandSyntax([
                Token("PARSE", PromptTokenKind.Word, resolved.Span.Start),
                Token("JSON", PromptTokenKind.Word, resolved.Span.Start),
                Token(output.StartsWith("[") ? output : $"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(resolved.Values[3].UnquotedText.Trim(), resolved.Values[3].Span.Start)
            ], grammar)];
        }

        if (resolved.NormalizedName == "EMIT")
        {
            CommandSyntax? emission = SurfaceMutationLowering.Emit(
                resolved, grammar, diagnostics, context.AuthenticationSecret);
            return emission is null ? [] : [emission];
        }

        if (resolved.NormalizedName == "PAGINATE")
        {
            CommandSyntax? pagination = LowerPagination(resolved, grammar, diagnostics, context.AuthenticationSecret);
            return pagination is null ? [] : [pagination];
        }

        if (resolved.NormalizedName == "APPLY")
        {
            CommandSyntax? sql = LowerSqlMutation(resolved, grammar, diagnostics);
            return sql is null ? [] : [sql];
        }

        if (resolved.NormalizedName is "PUT" or "PATCH")
        {
            CommandSyntax? mutation = SurfaceMutationLowering.HttpJson(
                resolved,
                resolved.NormalizedName,
                grammar,
                diagnostics,
                context.AuthenticationSecret);
            return mutation is null ? [] : [mutation];
        }

        if (resolved.NormalizedName is "SAVE" or "SAVE_TO")
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

        if (resolved.NormalizedName == "REQUEST")
        {
            if (resolved.Values.Count != 1)
            {
                diagnostics.Add(new("FLN376", "REQUEST requires one HTTP URL and an optional AS alias.", resolved.Span));
                return [];
            }
            string output = resolved.Alias ?? "response";
            if (!IsIdentifier(output))
            {
                diagnostics.Add(new("FLN377", "REQUEST output must be a valid identifier.", resolved.Span));
                return [];
            }
            string source = resolved.Values[0].UnquotedText.Trim();
            if (!Uri.TryCreate(source.Trim('{', '}'), UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
            {
                diagnostics.Add(new("FLN378", "REQUEST requires an absolute HTTP(S) URL.", resolved.Values[0].Span));
                return [];
            }
            return [new CommandSyntax([
                Token("REQUESTJSON", PromptTokenKind.Word, resolved.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, resolved.Span.Start),
                Token("FROM", PromptTokenKind.Word, resolved.Span.Start),
                SurfaceReferenceToken(source, resolved.Values[0].Span.Start)
            ], grammar)];
        }

        diagnostics.Add(new(
            "FLN211",
            $"Surface command '{command.Name}' does not have a lowering rule yet.",
            command.Span));
        return [];
    }

    private bool TryLowerBatchJsonParse(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        LanguageSnapshot language,
        InferenceTrace trace,
        ICollection<SurfaceDiagnostic> diagnostics,
        out IReadOnlyList<CommandSyntax> commands)
    {
        commands = [];
        if (command.Values.Count < 2)
            return false;

        List<SurfaceValueSyntax> sources = [.. command.Values];
        string first = sources[0].UnquotedText.Trim();
        string? prefix = first.StartsWith("JSON FILES ", StringComparison.OrdinalIgnoreCase)
            ? "JSON FILES "
            : first.StartsWith("FILES ", StringComparison.OrdinalIgnoreCase)
                ? "FILES "
                : null;
        if (prefix is null)
            return false;

        string firstText = sources[0].Text.Trim();
        int prefixLength = firstText.IndexOf(' ', StringComparison.Ordinal);
        prefixLength = prefixLength >= 0
            ? firstText.IndexOf(' ', prefixLength + 1)
            : -1;
        if (prefix.Equals("FILES ", StringComparison.OrdinalIgnoreCase))
            prefixLength = firstText.IndexOf(' ', StringComparison.Ordinal);
        if (prefixLength < 0 || prefixLength + 1 >= firstText.Length)
        {
            diagnostics.Add(new("FLN358", "PARSE JSON FILES requires at least two sources.", command.Span));
            return true;
        }

        sources[0] = sources[0] with { Text = firstText[(prefixLength + 1)..].Trim() };
        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);
        List<CommandSyntax> lowered = [];
        foreach (SurfaceValueSyntax source in sources)
        {
            ResourceDescriptor descriptor;
            try
            {
                descriptor = inference.InferResource(source, language, trace);
            }
            catch (FormatException exception)
            {
                diagnostics.Add(new("FLN358", exception.Message, source.Span));
                continue;
            }

            string baseName = descriptor.SuggestedVariableName;
            string output = baseName;
            int suffix = 2;
            while (!usedNames.Add(output))
                output = $"{baseName}_{suffix++}";

            lowered.Add(new CommandSyntax([
                Token("PARSE", PromptTokenKind.Word, command.Span.Start),
                Token("JSON", PromptTokenKind.Word, command.Span.Start),
                Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
                Token("FROM", PromptTokenKind.Word, command.Span.Start),
                SurfaceReferenceToken(source.UnquotedText.Trim(), source.Span.Start)
            ], grammar));
        }

        commands = lowered;
        return true;
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

            case "WITH":
                string withPolicy = string.Join(" ", command.Values.Select(value => value.UnquotedText));
                System.Text.RegularExpressions.Match retryMatch =
                    System.Text.RegularExpressions.Regex.Match(withPolicy, @"RETRY\s*\{?([0-9]+)\}?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                System.Text.RegularExpressions.Match timeoutMatch =
                    System.Text.RegularExpressions.Regex.Match(withPolicy, @"TIMEOUT\s*\{?([^}\s]+)\}?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (retryMatch.Success && int.TryParse(retryMatch.Groups[1].Value, out int retries) && retries >= 0)
                    context = context with { Retry = retries };
                else if (withPolicy.Contains("RETRY", StringComparison.OrdinalIgnoreCase))
                    diagnostics.Add(new("FLN242", "Invalid RETRY policy.", command.Span));
                if (timeoutMatch.Success)
                    context = context with { Timeout = timeoutMatch.Groups[1].Value };
                else if (withPolicy.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase))
                    diagnostics.Add(new("FLN243", "Invalid TIMEOUT policy.", command.Span));
                return true;

            case "ON":
                string policy = string.Join(" ", command.Values.Select(value => value.UnquotedText)).Trim();
                if (!policy.Equals("ERROR CONTINUE", StringComparison.OrdinalIgnoreCase) &&
                    !policy.Equals("ERROR FAIL", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new("FLN245", "ON requires `ON ERROR CONTINUE` or `ON ERROR FAIL`.", command.Span));
                }
                else
                {
                    context = context with { ErrorPolicy = policy[("ERROR ".Length)..].Trim().ToUpperInvariant() };
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
        if (context.Retry is null && context.Timeout is null && context.ErrorPolicy is null && context.Condition is null)
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

        if (context.ErrorPolicy is string errorPolicy)
        {
            tokens.Add(Token("ON", PromptTokenKind.Word, position));
            tokens.Add(Token("ERROR", PromptTokenKind.Word, position));
            tokens.Add(Token(errorPolicy, PromptTokenKind.Word, position));
        }

        if (context.Condition is string condition &&
            !command.Modifiers.Any(modifier => modifier.Kind == CommandModifierKind.Condition))
        {
            tokens.Add(Token("IF", PromptTokenKind.Word, position));
            tokens.Add(Token(ConditionExpressionCompiler.NormalizeNaturalCondition(condition), PromptTokenKind.Word, position));
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

    private static SurfaceCommandSyntax NormalizeSugar(
        SurfaceCommandSyntax command,
        string? implicitInput)
    {
        SurfaceCommandSyntax normalized = command.NormalizedName switch
        {
            "WHERE" => command with { Name = "FILTER" },
            "ORDER" => command with { Name = "SORT" },
            "PRINT" or "ECHO" => command with { Name = "SAY" },
            "OUTPUT" => command with { Name = "SAY" },
            "FETCH" or "RETRIEVE" or "LOOKUP" => command with { Name = "GET" },
            // READ FILES is deliberately distinct from the key/value READ
            // command. It lets one sentence load several resources and keeps
            // the inferred variable names for each file.
            "READ" or "LOAD" or "GET" when command.Values.Count > 1 &&
                command.Values[0].UnquotedText.TrimStart().StartsWith("FILES ", StringComparison.OrdinalIgnoreCase)
                => command with
                {
                    Name = command.NormalizedName == "READ" ? "GET" : command.Name,
                    Values = [
                        command.Values[0] with
                        {
                            Text = command.Values[0].Text.TrimStart()["FILES".Length..].TrimStart()
                        },
                        .. command.Values.Skip(1)
                    ],
                    Alias = null
                },
            "FIND" => command with
            {
                Name = "SCAN",
                Values = command.Values.Select(MarkRecursive).ToArray()
            },
            "WRITE" => command with { Name = "SAVE" },
            "STORE" when !command.Values.Any(value => value.UnquotedText.Contains('=')) => command with { Name = "SAVE" },
            "OTHERWISE" => command with { Name = "ELSE" },
            _ => command
        };

        if (normalized.NormalizedName == "PARSE" &&
            normalized.Alias is not null &&
            normalized.Values.Count is 1 or 2)
        {
            string sourceText = string.Join(
                " ",
                normalized.Values.Select(value => value.UnquotedText)).Trim();
            if (sourceText.StartsWith("JSON ", StringComparison.OrdinalIgnoreCase))
                sourceText = sourceText["JSON".Length..].TrimStart();
            SurfaceValueSyntax source = normalized.Values[0] with { Text = sourceText };
            normalized = normalized with
            {
                Name = "PARSE",
                Values = [
                    new SurfaceValueSyntax("JSON", source.Span),
                    new SurfaceValueSyntax($"[{normalized.Alias}]", normalized.Span),
                    new SurfaceValueSyntax("FROM", source.Span),
                    source
                ],
                Alias = null
            };
        }

        if (normalized.NormalizedName == "REMOVE" &&
            normalized.Values.Count == 1 &&
            normalized.Values[0].UnquotedText.TrimStart().StartsWith("DUPLICATES", StringComparison.OrdinalIgnoreCase))
        {
            string remainder = normalized.Values[0].UnquotedText.Trim()["DUPLICATES".Length..].Trim();
            normalized = normalized with
            {
                Name = "DISTINCT",
                Values = string.IsNullOrWhiteSpace(remainder)
                    ? []
                    : [new SurfaceValueSyntax(remainder, normalized.Values[0].Span)]
            };
        }

        if (normalized.NormalizedName is "KEEP" or "KEEPING")
        {
            string phrase = string.Join(" ", normalized.Values.Select(value => value.UnquotedText)).Trim();
            Match? count = Regex.Match(
                phrase,
                @"^(?:THE\s+)?(?:FIRST|TOP)?\s*(?<count>\d+)(?:\s+.*)?$",
                RegexOptions.IgnoreCase);
            if (count.Success)
            {
                normalized = normalized with
                {
                    Name = "TAKE",
                    Values = [new SurfaceValueSyntax(count.Groups["count"].Value, normalized.Values[0].Span)]
                };
            }
        }

        if (normalized.NormalizedName == "SKIP")
        {
            string phrase = string.Join(" ", normalized.Values.Select(value => value.UnquotedText)).Trim();
            Match count = Regex.Match(phrase, @"^(?:THE\s+)?(?:FIRST)?\s*(?<count>\d+)(?:\s+.*)?$", RegexOptions.IgnoreCase);
            if (count.Success)
                normalized = normalized with { Values = [new SurfaceValueSyntax(count.Groups["count"].Value, normalized.Values[0].Span)] };
        }

        if (normalized.NormalizedName is not "SAY" and not "LET")
        {
            normalized = normalized with
            {
                Values = normalized.Values
                    .Select(value => value with { Text = RemoveNaturalLeadIn(value.Text) })
                    .ToArray()
            };
        }

        if (implicitInput is not null && normalized.NormalizedName is not "SAY")
        {
            normalized = normalized with
            {
                Values = normalized.Values
                    .Select(value => value with { Text = ReplaceContextPronouns(value.Text, implicitInput) })
                    .ToArray()
            };
        }

        if (implicitInput is not null &&
            normalized.NormalizedName is "SAVE" or "POST" &&
            normalized.Values.Count == 1 &&
            normalized.Alias is not null)
        {
            normalized = normalized with
            {
                Name = normalized.NormalizedName == "SAVE" ? "SAVE_TO" : normalized.Name,
                Values = [new SurfaceValueSyntax(
                    $"{normalized.Values[0].UnquotedText} TO {normalized.Alias}",
                    normalized.Values[0].Span)],
                Alias = null
            };
        }

        if (implicitInput is not null &&
            normalized.NormalizedName is "SAVE" or "POST" &&
            normalized.Values.Count == 1)
        {
            string value = normalized.Values[0].UnquotedText.Trim();
            if (value.StartsWith("TO ", StringComparison.OrdinalIgnoreCase))
            {
                string target = value[3..].Trim();
                normalized = normalized with
                {
                    Name = normalized.NormalizedName == "SAVE" ? "SAVE_TO" : normalized.Name,
                    Values = [new SurfaceValueSyntax($"{implicitInput} TO {target}", normalized.Values[0].Span)]
                };
            }
        }

        return normalized;
    }

    private static SurfaceValueSyntax MarkRecursive(SurfaceValueSyntax value)
    {
        string text = value.Text;
        string marked = text.Length >= 2 && text[0] is '"' or '\'' && text[^1] == text[0]
            ? $"{text[0]}__flunet_recursive__:{text[1..^1]}{text[^1]}"
            : $"__flunet_recursive__:{text}";
        return value with { Text = marked };
    }

    private static string RemoveNaturalLeadIn(string value)
    {
        if (value.Length == 0 || (value[0] is '"' or '\''))
            return value;

        return Regex.Replace(
            value,
            @"^\s*(?:PLEASE\s+)?(?:THE|A|AN)\s+",
            string.Empty,
            RegexOptions.IgnoreCase);
    }

    private static string ReplaceContextPronouns(string value, string input)
    {
        if (value.Length == 0 || (value[0] is '"' or '\''))
            return value;

        string result = value.Trim();
        if (result.Equals("IT", StringComparison.OrdinalIgnoreCase) ||
            result.Equals("THEM", StringComparison.OrdinalIgnoreCase) ||
            result.Equals("THIS", StringComparison.OrdinalIgnoreCase) ||
            result.Equals("THE RESULT", StringComparison.OrdinalIgnoreCase) ||
            result.Equals("THE RESPONSE", StringComparison.OrdinalIgnoreCase))
            return $"[{input}]";

        result = Regex.Replace(result, @"\bTHEIR\s*\.\s*", $"[{input}].", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bITS\s*\.\s*", $"[{input}].", RegexOptions.IgnoreCase);
        return result;
    }

    private static string NextPipe(int commandCount) =>
        $"__pipe_{commandCount:D4}";

    private static CommandSyntax? LowerText(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        string phrase = string.Join(" ", command.Values.Select(value => value.Text)).Trim();
        string output = command.Alias ?? command.NormalizedName.ToLowerInvariant();
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN370", "Text operation output must be a valid identifier.", command.Span));
            return null;
        }

        string source;
        string verb;
        List<PromptToken> tokens = [];
        switch (command.NormalizedName)
        {
            case "TRIM":
                verb = "TRIMTEXT";
                source = phrase;
                break;
            case "UPPER":
            case "UPPERCASE":
                verb = "UPPERTEXT";
                source = phrase;
                break;
            case "LOWER":
            case "LOWERCASE":
                verb = "LOWERTEXT";
                source = phrase;
                break;
            case "LINES":
                verb = "LINES";
                source = phrase;
                break;
            case "EXPECT":
            {
                Match match = Regex.Match(phrase, @"^(?<source>.+?)\s+TO\s+(?<operator>BE|EQUALS?|CONTAINS?|STARTS\s+WITH|ENDS\s+WITH|MATCHES?)\s+(?<expected>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!match.Success)
                {
                    diagnostics.Add(new("FLN375", "EXPECT requires `EXPECT value TO EQUAL expected` or another supported comparison.", command.Span));
                    return null;
                }
                verb = "EXPECTTEXT";
                source = match.Groups["source"].Value.Trim();
                tokens.Add(Token("USING", PromptTokenKind.Word, command.Span.Start));
                tokens.Add(TextOperationToken(match.Groups["expected"].Value.Trim(), command.Span.Start));
                tokens.Add(Token("WITH", PromptTokenKind.Word, command.Span.Start));
                tokens.Add(Token(match.Groups["operator"].Value.Trim(), PromptTokenKind.Word, command.Span.Start));
                break;
            }
            case "SPLIT":
            {
                Match match = Regex.Match(phrase, @"^(?<source>.+?)\s+BY\s+(?<separator>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!match.Success)
                {
                    diagnostics.Add(new("FLN371", "SPLIT requires `SPLIT text BY separator AS parts`.", command.Span));
                    return null;
                }
                verb = "SPLITTEXT";
                source = match.Groups["source"].Value.Trim();
                tokens.Add(Token("USING", PromptTokenKind.Word, command.Span.Start));
                tokens.Add(TextOperationToken(match.Groups["separator"].Value.Trim(), command.Span.Start));
                break;
            }
            case "COMBINE":
            case "CONCATENATE":
            {
                Match match = Regex.Match(phrase, @"^(?<source>.+?)\s+WITH\s+(?<separator>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!match.Success)
                {
                    diagnostics.Add(new("FLN372", "JOIN requires `JOIN parts WITH separator AS text`.", command.Span));
                    return null;
                }
                verb = "JOINTEXT";
                source = match.Groups["source"].Value.Trim();
                tokens.Add(Token("USING", PromptTokenKind.Word, command.Span.Start));
                tokens.Add(TextOperationToken(match.Groups["separator"].Value.Trim(), command.Span.Start));
                break;
            }
            case "REPLACE":
            {
                Match match = Regex.Match(phrase, @"^(?<old>.+?)\s+WITH\s+(?<new>.+?)\s+IN\s+(?<source>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!match.Success)
                {
                    diagnostics.Add(new("FLN373", "REPLACE requires `REPLACE old WITH new IN text AS result`.", command.Span));
                    return null;
                }
                verb = "REPLACETEXT";
                source = match.Groups["source"].Value.Trim();
                tokens.Add(Token("USING", PromptTokenKind.Word, command.Span.Start));
                tokens.Add(TextOperationToken(match.Groups["old"].Value.Trim(), command.Span.Start));
                tokens.Add(Token("WITH", PromptTokenKind.Word, command.Span.Start));
                tokens.Add(TextOperationToken(match.Groups["new"].Value.Trim(), command.Span.Start));
                break;
            }
            default:
                return null;
        }

        if (source.Length == 0)
        {
            diagnostics.Add(new("FLN374", "Text operation source cannot be empty.", command.Span));
            return null;
        }

        List<PromptToken> result = [
            Token(verb, PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            command.NormalizedName == "EXPECT"
                ? ExpectationSourceToken(source, command.Span.Start)
                : TextOperationToken(source, command.Span.Start)
        ];
        result.AddRange(tokens);
        return new CommandSyntax(result, grammar);
    }

    private static PromptToken TextOperationToken(string value, int start)
    {
        value = value.Trim();
        if (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith(']'))
            return Token(value, PromptTokenKind.Variable, start);
        if (value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith('}'))
            return Token(value, PromptTokenKind.Reference, start);
        return Token(value, PromptTokenKind.Word, start);
    }

    private static PromptToken ExpectationSourceToken(string value, int start)
    {
        value = value.Trim();
        if (value.StartsWith("[", StringComparison.Ordinal) ||
            value.StartsWith("{", StringComparison.Ordinal) ||
            value.StartsWith("\"", StringComparison.Ordinal) ||
            value.StartsWith("'", StringComparison.Ordinal))
            return TextOperationToken(value, start);
        return Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*|\[[0-9]+\])+$", RegexOptions.CultureInvariant)
            ? Token($"\"{{{value}}}\"", PromptTokenKind.Word, start)
            : TextOperationToken(value, start);
    }

    private static CommandSyntax LowerSay(
        SurfaceCommandSyntax command,
        PromptGrammar grammar)
    {
        List<PromptToken> tokens =
        [
            Token("SAY", PromptTokenKind.Word, command.Span.Start)
        ];

        for (int index = 0; index < command.Values.Count; index++)
        {
            SurfaceValueSyntax value = command.Values[index];
            if (index == command.Values.Count - 1 &&
                TrySplitTrailingIf(value.Text, out string? message, out string? condition))
            {
                string messageText = SurfacePath(message!) ? $"\"{{{message}}}\"" : message!;
                tokens.Add(Token(messageText, Classify(messageText), value.Span.Start));
                tokens.Add(Token("IF", PromptTokenKind.Word, value.Span.Start));
                tokens.Add(Token(condition!, Classify(condition!), value.Span.Start));
                continue;
            }
            string text = SurfacePath(value.Text)
                ? $"\"{{{value.Text}}}\""
                : value.Text;
            tokens.Add(Token(text, Classify(text), value.Span.Start));
        }

        return new(tokens, grammar);
    }

    private static CommandSyntax? LowerNotify(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count == 0)
        {
            diagnostics.Add(new("FLN307", "NOTIFY requires a message.", command.Span));
            return null;
        }

        List<PromptToken> tokens =
        [
            Token("NOTIFYTEXT", PromptTokenKind.Word, command.Span.Start)
        ];
        foreach (SurfaceValueSyntax value in command.Values)
            tokens.Add(Token(value.Text, Classify(value.Text), value.Span.Start));
        return new CommandSyntax(tokens, grammar);
    }

    private static CommandSyntax? LowerReceive(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new("FLN308", "RECEIVE requires a topic, for example `RECEIVE \"jobs\" AS message`.", command.Span));
            return null;
        }

        string output = command.Alias ?? "message";
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN309", $"RECEIVE output '{output}' is not a valid identifier.", command.Span));
            return null;
        }

        SurfaceValueSyntax value = command.Values[0];
        string topic = value.UnquotedText.Trim();
        PromptToken topicToken = topic.Length >= 2 && topic[0] == '[' && topic[^1] == ']'
            ? Token(topic, PromptTokenKind.Variable, value.Span.Start)
            : Token(topic.Trim('"', '\''), PromptTokenKind.Word, value.Span.Start);
        return new CommandSyntax([
            Token("RECEIVEMESSAGE", PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            topicToken
        ], grammar);
    }

    private static bool TrySplitTrailingIf(string text, out string? message, out string? condition)
    {
        message = condition = null;
        char? quote = null;
        for (int index = text.Length - 4; index >= 0; index--)
        {
            char current = text[index];
            if (current is '"' or '\'') quote = quote is null ? current : quote == current ? null : quote;
            if (quote is not null || !text.AsSpan(index, 4).Equals(" IF ", StringComparison.OrdinalIgnoreCase)) continue;
            message = text[..index].TrimEnd();
            condition = text[(index + 4)..].Trim();
            return message.Length > 0 && condition.Length > 0;
        }
        return false;
    }

    private static IReadOnlyList<CommandSyntax> LowerScan(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new("FLN287", "SCAN requires a file pattern, for example `SCAN \"./data/*.json\" AS files`.", command.Span));
            return [];
        }

        string output = command.Alias ?? "files";
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN288", $"SCAN output '{output}' is not a valid identifier.", command.Span));
            return [];
        }

        SurfaceValueSyntax value = command.Values[0];
        string phrase = value.UnquotedText.Trim();
        Match limitMatch = Regex.Match(phrase, @"\s+LIMIT\s+(?<count>\d+)(?=\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        int? limit = null;
        if (limitMatch.Success)
        {
            limit = int.Parse(limitMatch.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (limit is < 1 or > 1_000_000)
            {
                diagnostics.Add(new("FLN362", "SCAN/FIND LIMIT must be between 1 and 1000000.", command.Span));
                return [];
            }
            phrase = Regex.Replace(phrase, @"\s+LIMIT\s+\d+(?=\s|$)", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        }
        MatchCollection clauses = Regex.Matches(
            phrase,
            @"\s+(WHERE|ORDER\s+BY|TAKE|SKIP)\s+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        string sourcePhrase = clauses.Count == 0 ? phrase : phrase[..clauses[0].Index].Trim();
        if (sourcePhrase.Length == 0)
        {
            diagnostics.Add(new("FLN289", "FIND requires a file pattern or directory.", command.Span));
            return [];
        }

        string source = sourcePhrase.Trim();
        bool recursive = source.StartsWith("__flunet_recursive__:", StringComparison.Ordinal);
        if (recursive)
            source = source["__flunet_recursive__:".Length..].Trim().Trim('"', '\'');
        else if (command.NormalizedName == "FIND")
            recursive = true;
        source = recursive ? $"__flunet_recursive__:{source}" : source;
        PromptToken token = source.StartsWith("[", StringComparison.Ordinal) && source.EndsWith(']')
            ? Token(source, PromptTokenKind.Variable, value.Span.Start)
            : Token($"{{{source}}}", PromptTokenKind.Reference, value.Span.Start);

        bool hasPipeline = clauses.Count > 0;
        string current = hasPipeline ? $"__find_{output}_0" : output;
        List<PromptToken> scanTokens = [
            Token("SCANFILES", PromptTokenKind.Word, command.Span.Start),
            Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            token
        ];
        if (limit is int count)
        {
            scanTokens.Add(Token("LIMIT", PromptTokenKind.Word, command.Span.Start));
            scanTokens.Add(Token(count.ToString(System.Globalization.CultureInfo.InvariantCulture), PromptTokenKind.Word, command.Span.Start));
        }
        CommandSyntax scan = new(scanTokens, grammar);
        List<CommandSyntax> result = [scan];
        for (int index = 0; index < clauses.Count; index++)
        {
            Match clause = clauses[index];
            int contentStart = clause.Index + clause.Length;
            int contentEnd = index + 1 < clauses.Count ? clauses[index + 1].Index : phrase.Length;
            string content = phrase[contentStart..contentEnd].Trim();
            if (content.Length == 0)
            {
                diagnostics.Add(new("FLN289", $"FIND {clause.Groups[1].Value} requires a value.", command.Span));
                return [];
            }

            string next = index == clauses.Count - 1 ? output : $"__find_{output}_{index + 1}";
            switch (clause.Groups[1].Value.ToUpperInvariant())
            {
                case "WHERE":
                    content = NormalizeFilePredicate(content.Trim('{', '}'));
                    result.Add(new CommandSyntax([
                        Token("FILTERJSON", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{next}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("FROM", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("USING", PromptTokenKind.Word, command.Span.Start),
                        Token($"{{{content}}}", PromptTokenKind.Reference, command.Span.Start)
                    ], grammar));
                    break;
                case "ORDER BY":
                    result.Add(new CommandSyntax([
                        Token("SORTJSON", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{next}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("FROM", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("USING", PromptTokenKind.Word, command.Span.Start),
                        Token($"{{{content}}}", PromptTokenKind.Reference, command.Span.Start)
                    ], grammar));
                    break;
                case "TAKE":
                case "SKIP":
                    if (!int.TryParse(content, out int pageCount) || pageCount < 0)
                    {
                        diagnostics.Add(new("FLN313", $"FIND {clause.Groups[1].Value} requires a non-negative integer.", command.Span));
                        return [];
                    }
                    result.Add(new CommandSyntax([
                        Token(clause.Groups[1].Value.Equals("TAKE", StringComparison.OrdinalIgnoreCase) ? "TAKEJSON" : "SKIPJSON", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{next}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("FROM", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("USING", PromptTokenKind.Word, command.Span.Start),
                        Token(pageCount.ToString(System.Globalization.CultureInfo.InvariantCulture), PromptTokenKind.Word, command.Span.Start)
                    ], grammar));
                    break;
            }
            current = next;
        }
        return result;
    }

    private static IReadOnlyList<CommandSyntax> LowerList(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count == 0)
        {
            diagnostics.Add(new("FLN305", "LIST requires a directory path, for example `LIST \"./reports\" AS entries`.", command.Span));
            return [];
        }

        string output = command.Alias ?? "entries";
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN306", $"LIST output '{output}' is not a valid identifier.", command.Span));
            return [];
        }

        string phrase = string.Join(" ", command.Values.Select(item => item.UnquotedText)).Trim();
        bool recursive = Regex.IsMatch(phrase, @"\s+RECURSIVE(?=\s|$)", RegexOptions.IgnoreCase);
        if (recursive) phrase = Regex.Replace(phrase, @"\s+RECURSIVE(?=\s|$)", string.Empty, RegexOptions.IgnoreCase).Trim();
        MatchCollection clauses = Regex.Matches(
            phrase,
            @"\s+(WHERE|ORDER\s+BY|TAKE|SKIP)\s+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        string sourcePhrase = clauses.Count == 0 ? phrase : phrase[..clauses[0].Index].Trim();
        if (sourcePhrase.Length == 0)
        {
            diagnostics.Add(new("FLN305", "LIST requires a directory path.", command.Span));
            return [];
        }

        SurfaceValueSyntax value = new(sourcePhrase, command.Values[0].Span);
        string source = value.UnquotedText.Trim().Trim('"', '\'');
        PromptToken token = source.StartsWith("[", StringComparison.Ordinal) && source.EndsWith(']')
            ? Token(source, PromptTokenKind.Variable, value.Span.Start)
            : Token($"{{{source}}}", PromptTokenKind.Reference, value.Span.Start);

        bool hasPipeline = clauses.Count > 0;
        string current = hasPipeline ? $"__list_{output}_0" : output;
        List<PromptToken> tokens = [
            Token("LISTFILES", PromptTokenKind.Word, command.Span.Start),
            Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            token
        ];
        if (recursive)
        {
            tokens.Add(Token("USING", PromptTokenKind.Word, command.Span.Start));
            tokens.Add(Token("true", PromptTokenKind.Word, command.Span.Start));
        }
        List<CommandSyntax> result = [new CommandSyntax(tokens, grammar)];
        for (int index = 0; index < clauses.Count; index++)
        {
            Match clause = clauses[index];
            int contentStart = clause.Index + clause.Length;
            int contentEnd = index + 1 < clauses.Count ? clauses[index + 1].Index : phrase.Length;
            string content = phrase[contentStart..contentEnd].Trim();
            string keyword = clause.Groups[1].Value.ToUpperInvariant();
            string next = index + 1 == clauses.Count ? output : $"__list_{output}_{index + 1}";
            if (content.Length == 0)
            {
                diagnostics.Add(new("FLN312", $"LIST {keyword} requires a value.", command.Span));
                return [];
            }

            switch (keyword)
            {
                case "WHERE":
                    content = NormalizeFilePredicate(content);
                    result.Add(new CommandSyntax([
                        Token("FILTERJSON", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{next}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("FROM", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("USING", PromptTokenKind.Word, command.Span.Start),
                        Token($"{{{content}}}", PromptTokenKind.Reference, command.Span.Start)
                    ], grammar));
                    break;
                case "ORDER BY":
                    result.Add(new CommandSyntax([
                        Token("SORTJSON", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{next}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("FROM", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("USING", PromptTokenKind.Word, command.Span.Start),
                        Token($"{{{content}}}", PromptTokenKind.Reference, command.Span.Start)
                    ], grammar));
                    break;
                case "TAKE":
                case "SKIP":
                    if (!int.TryParse(content, out int count) || count < 0)
                    {
                        diagnostics.Add(new("FLN313", $"LIST {keyword} requires a non-negative integer.", command.Span));
                        return [];
                    }
                    result.Add(new CommandSyntax([
                        Token(keyword == "TAKE" ? "TAKEJSON" : "SKIPJSON", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{next}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("FROM", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("USING", PromptTokenKind.Word, command.Span.Start),
                        Token(count.ToString(), PromptTokenKind.Word, command.Span.Start)
                    ], grammar));
                    break;
            }
            current = next;
        }
        return result;
    }

    private static string NormalizeFilePredicate(string predicate)
    {
        string normalized = predicate.Trim();
        normalized = Regex.Replace(normalized, @"\b(?:size|bytes|fileSize)\b", "length", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\bmodified\b", "modifiedUtc", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\bcreated\b", "createdUtc", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\baccessed\b", "accessedUtc", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\bhidden\b", "isHidden", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\breadonly\b", "isReadOnly", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\b(?<field>modifiedUtc|createdUtc|accessedUtc)\s+AFTER\s+", "${field} > ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\b(?<field>modifiedUtc|createdUtc|accessedUtc)\s+BEFORE\s+", "${field} < ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return normalized;
    }

    private static CommandSyntax? LowerSearch(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new("FLN345", "SEARCH requires `text IN directory AS matches`.", command.Span));
            return null;
        }

        string phrase = command.Values[0].UnquotedText.Trim();
        bool recursive = Regex.IsMatch(phrase, @"\s+RECURSIVE(?=\s|$)", RegexOptions.IgnoreCase);
        bool regex = Regex.IsMatch(phrase, @"^REGEX\s+", RegexOptions.IgnoreCase);
        Match limitMatch = Regex.Match(phrase, @"\s+LIMIT\s+(?<count>\d+)(?=\s|$)", RegexOptions.IgnoreCase);
        int limit = limitMatch.Success ? int.Parse(limitMatch.Groups["count"].Value) : 0;
        phrase = Regex.Replace(phrase, @"\s+RECURSIVE(?=\s|$)", string.Empty, RegexOptions.IgnoreCase).Trim();
        phrase = Regex.Replace(phrase, @"\s+LIMIT\s+\d+(?=\s|$)", string.Empty, RegexOptions.IgnoreCase).Trim();
        if (regex) phrase = Regex.Replace(phrase, "^REGEX\\s+", string.Empty, RegexOptions.IgnoreCase).Trim();
        int separator = phrase.IndexOf(" IN ", StringComparison.OrdinalIgnoreCase);
        if (separator <= 0 || separator + 4 >= phrase.Length)
        {
            diagnostics.Add(new("FLN345", "SEARCH requires `text IN directory AS matches`.", command.Span));
            return null;
        }

        string query = phrase[..separator].Trim().Trim('"', '\'');
        string root = phrase[(separator + 4)..].Trim().Trim('"', '\'');
        string output = command.Alias ?? "matches";
        if (query.Length == 0 || root.Length == 0 || !IsIdentifier(output))
        {
            diagnostics.Add(new("FLN346", "SEARCH requires non-empty text, directory and a valid output identifier.", command.Span));
            return null;
        }

        return new CommandSyntax([
            Token("SEARCHFILES", PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{root}}}", PromptTokenKind.Reference, command.Span.Start),
            Token("USING", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{query}}}", PromptTokenKind.Reference, command.Span.Start),
            Token("RECURSIVE", PromptTokenKind.Word, command.Span.Start),
            Token(recursive.ToString().ToLowerInvariant(), PromptTokenKind.Word, command.Span.Start),
            Token("REGEX", PromptTokenKind.Word, command.Span.Start),
            Token(regex.ToString().ToLowerInvariant(), PromptTokenKind.Word, command.Span.Start),
            Token("LIMIT", PromptTokenKind.Word, command.Span.Start),
            Token(limit.ToString(), PromptTokenKind.Word, command.Span.Start)
        ], grammar);
    }

    private static CommandSyntax? LowerPagination(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics,
        string? authenticationSecret)
    {
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new("FLN357", "PAGINATE requires `url ITEMS path NEXT path LIMIT pages AS output`.", command.Span));
            return null;
        }

        string phrase = command.Values[0].UnquotedText.Trim();
        Match match = Regex.Match(
            phrase,
            @"^(?<url>.+?)\s+ITEMS\s+(?<items>\S+)\s+NEXT\s+(?<next>\S+)\s+LIMIT\s+(?<limit>\d+)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success || !Uri.TryCreate(Unquote(match.Groups["url"].Value), UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            diagnostics.Add(new("FLN357", "PAGINATE requires an absolute HTTP(S) URL, ITEMS path, NEXT path and LIMIT.", command.Span));
            return null;
        }

        if (!int.TryParse(match.Groups["limit"].Value, out int limit) || limit is < 1 or > 1000)
        {
            diagnostics.Add(new("FLN358", "PAGINATE LIMIT must be between 1 and 1000.", command.Span));
            return null;
        }

        string output = command.Alias ?? "items";
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN359", $"PAGINATE output '{output}' is not a valid identifier.", command.Span));
            return null;
        }

        List<PromptToken> tokens = [
            Token("PAGINATEJSON", PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{uri}}}", PromptTokenKind.Reference, command.Span.Start),
            Token("ITEMS", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{Unquote(match.Groups["items"].Value)}}}", PromptTokenKind.Reference, command.Span.Start),
            Token("NEXT", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{Unquote(match.Groups["next"].Value)}}}", PromptTokenKind.Reference, command.Span.Start),
            Token("LIMIT", PromptTokenKind.Word, command.Span.Start),
            Token(limit.ToString(System.Globalization.CultureInfo.InvariantCulture), PromptTokenKind.Word, command.Span.Start)
        ];
        if (!string.IsNullOrWhiteSpace(authenticationSecret))
        {
            tokens.Add(Token("USING", PromptTokenKind.Word, command.Span.Start));
            tokens.Add(Token($"{{{authenticationSecret}}}", PromptTokenKind.Reference, command.Span.Start));
        }
        return new CommandSyntax(tokens, grammar);
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;

    private static IReadOnlyList<CommandSyntax> LowerArchiveListing(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        string phrase = string.Join(" ", command.Values.Select(value => value.UnquotedText)).Trim();
        if (phrase.StartsWith("ARCHIVE", StringComparison.OrdinalIgnoreCase))
            phrase = phrase["ARCHIVE".Length..].Trim();
        MatchCollection clauses = Regex.Matches(
            phrase,
            @"\s+(WHERE|ORDER\s+BY|TAKE|SKIP)\s+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        string sourcePhrase = clauses.Count == 0 ? phrase : phrase[..clauses[0].Index].Trim();
        string output = command.Alias ?? "entries";
        if (sourcePhrase.Length == 0)
        {
            diagnostics.Add(new("FLN305", "LIST ARCHIVE requires an archive path, for example `LIST ARCHIVE \"./bundle.zip\" AS entries`.", command.Span));
            return [];
        }
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN306", $"LIST ARCHIVE output '{output}' is not a valid identifier.", command.Span));
            return [];
        }

        string source = sourcePhrase.Trim().Trim('"', '\'');
        PromptToken sourceToken = source.StartsWith("[", StringComparison.Ordinal) && source.EndsWith(']')
            ? Token(source, PromptTokenKind.Variable, command.Span.Start)
            : Token($"{{{source}}}", PromptTokenKind.Reference, command.Span.Start);
        bool hasPipeline = clauses.Count > 0;
        string current = hasPipeline ? $"__archive_{output}_0" : output;
        List<CommandSyntax> result = [new CommandSyntax([
            Token("LISTARCHIVE", PromptTokenKind.Word, command.Span.Start),
            Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            sourceToken
        ], grammar)];
        for (int index = 0; index < clauses.Count; index++)
        {
            Match clause = clauses[index];
            int contentStart = clause.Index + clause.Length;
            int contentEnd = index + 1 < clauses.Count ? clauses[index + 1].Index : phrase.Length;
            string content = phrase[contentStart..contentEnd].Trim();
            string keyword = clause.Groups[1].Value.ToUpperInvariant();
            string next = index + 1 == clauses.Count ? output : $"__archive_{output}_{index + 1}";
            if (content.Length == 0)
            {
                diagnostics.Add(new("FLN312", $"LIST ARCHIVE {keyword} requires a value.", command.Span));
                return [];
            }
            switch (keyword)
            {
                case "WHERE":
                case "ORDER BY":
                    result.Add(new CommandSyntax([
                        Token(keyword == "WHERE" ? "FILTERJSON" : "SORTJSON", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{next}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("FROM", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("USING", PromptTokenKind.Word, command.Span.Start),
                        Token($"{{{(keyword == "ORDER BY" && content.StartsWith("BY ", StringComparison.OrdinalIgnoreCase) ? content[3..].Trim() : content)}}}", PromptTokenKind.Reference, command.Span.Start)
                    ], grammar));
                    break;
                case "TAKE":
                case "SKIP":
                    if (!int.TryParse(content, out int count) || count < 0)
                    {
                        diagnostics.Add(new("FLN313", $"LIST ARCHIVE {keyword} requires a non-negative integer.", command.Span));
                        return [];
                    }
                    result.Add(new CommandSyntax([
                        Token(keyword == "TAKE" ? "TAKEJSON" : "SKIPJSON", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{next}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("FROM", PromptTokenKind.Word, command.Span.Start),
                        Token($"[{current}]", PromptTokenKind.Variable, command.Span.Start),
                        Token("USING", PromptTokenKind.Word, command.Span.Start),
                        Token(count.ToString(), PromptTokenKind.Word, command.Span.Start)
                    ], grammar));
                    break;
            }
            current = next;
        }
        return result;
    }

    private static IReadOnlyList<CommandSyntax> LowerBlobListing(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        string phrase = string.Join(" ", command.Values.Select(value => value.UnquotedText)).Trim();
        phrase = Regex.Replace(phrase, "^BLOBS?\\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
        if (phrase.Length == 0)
        {
            diagnostics.Add(new("FLN343", "LIST BLOB requires an optional prefix and output, for example `LIST BLOB \"reports/\" AS keys`.", command.Span));
            return [];
        }

        string prefix = phrase.Trim().Trim('"', '\'');
        string output = command.Alias ?? "keys";
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN344", $"LIST BLOB output '{output}' is not a valid identifier.", command.Span));
            return [];
        }

        PromptToken source = prefix.StartsWith("[", StringComparison.Ordinal) && prefix.EndsWith(']')
            ? Token(prefix, PromptTokenKind.Variable, command.Span.Start)
            : Token($"{{{prefix}}}", PromptTokenKind.Reference, command.Span.Start);
        return [new CommandSyntax([
            Token("LISTBLOB", PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            source
        ], grammar)];
    }

    private static IReadOnlyList<CommandSyntax> LowerKeyValueListing(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        string phrase = Regex.Replace(
            string.Join(" ", command.Values.Select(value => value.UnquotedText)).Trim(),
            "^STORES?\\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
        if (phrase.Length == 0)
        {
            diagnostics.Add(new("FLN347", "LIST STORE requires a prefix and output, for example `LIST STORE \"user:\" AS values`.", command.Span));
            return [];
        }
        string prefix = phrase.Trim().Trim('"', '\'');
        string output = command.Alias ?? "values";
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN348", $"LIST STORE output '{output}' is not a valid identifier.", command.Span));
            return [];
        }
        PromptToken source = prefix.StartsWith("[", StringComparison.Ordinal) && prefix.EndsWith(']')
            ? Token(prefix, PromptTokenKind.Variable, command.Span.Start)
            : Token($"{{{prefix}}}", PromptTokenKind.Reference, command.Span.Start);
        return [new CommandSyntax([
            Token("LISTVALUES", PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            source
        ], grammar)];
    }

    private static CommandSyntax? LowerStat(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new("FLN310", "STAT requires a path, for example `STAT \"./report.json\" AS info`.", command.Span));
            return null;
        }

        string output = command.Alias ?? "info";
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN311", $"STAT output '{output}' is not a valid identifier.", command.Span));
            return null;
        }

        SurfaceValueSyntax value = command.Values[0];
        string source = value.UnquotedText.Trim();
        PromptToken token = source.StartsWith("[", StringComparison.Ordinal) && source.EndsWith(']')
            ? Token(source, PromptTokenKind.Variable, value.Span.Start)
            : Token($"{{{source.Trim('"', '\'')}}}", PromptTokenKind.Reference, value.Span.Start);
        return new CommandSyntax([
            Token("STATPATH", PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            token
        ], grammar);
    }

    private static CommandSyntax? LowerHash(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new("FLN289", "HASH requires a file path, for example `HASH \"./data.json\" AS digest`.", command.Span));
            return null;
        }

        string output = command.Alias ?? "hash";
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN290", $"HASH output '{output}' is not a valid identifier.", command.Span));
            return null;
        }

        SurfaceValueSyntax value = command.Values[0];
        string source = value.UnquotedText.Trim();
        PromptToken token = source.StartsWith("[", StringComparison.Ordinal) && source.EndsWith(']')
            ? Token(source, PromptTokenKind.Variable, value.Span.Start)
            : Token($"{{{source}}}", PromptTokenKind.Reference, value.Span.Start);

        return new CommandSyntax([
            Token("HASHFILE", PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            token
        ], grammar);
    }

    private static CommandSyntax? LowerFileTransfer(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new("FLN291", $"{command.Name} requires `source TO destination AS result`.", command.Span));
            return null;
        }

        string value = command.Values[0].UnquotedText.Trim();
        bool directoryTransfer = Regex.IsMatch(value, @"^DIRECTORY\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (directoryTransfer)
            value = value["DIRECTORY".Length..].Trim();
        int separator = value.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
        if (separator <= 0 || separator + 4 >= value.Length)
        {
            diagnostics.Add(new("FLN291", $"{command.Name} requires `source TO destination AS result`.", command.Span));
            return null;
        }

        string source = value[..separator].Trim().Trim('"', '\'');
        string goal = value[(separator + 4)..].Trim().Trim('"', '\'');
        string output = command.Alias ?? (command.NormalizedName == "COPY" ? "copied" : "moved");
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN292", $"{command.Name} output '{output}' is not a valid identifier.", command.Span));
            return null;
        }

        string verb = directoryTransfer
            ? command.NormalizedName == "COPY" ? "COPYDIRECTORY" : "MOVEDIRECTORY"
            : command.NormalizedName == "COPY" ? "COPYFILE" : "MOVEFILE";
        return new CommandSyntax([
            Token(verb, PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{source}}}", PromptTokenKind.Reference, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{goal}}}", PromptTokenKind.Reference, command.Span.Start)
        ], grammar);
    }

    private static CommandSyntax? LowerArchive(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new("FLN301", $"{command.Name} requires `source TO destination AS result`.", command.Span));
            return null;
        }

        string value = command.Values[0].UnquotedText.Trim();
        int separator = value.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
        if (separator <= 0 || separator + 4 >= value.Length)
        {
            diagnostics.Add(new("FLN301", $"{command.Name} requires `source TO destination AS result`.", command.Span));
            return null;
        }

        string source = value[..separator].Trim().Trim('"', '\'');
        string goal = value[(separator + 4)..].Trim().Trim('"', '\'');
        string output = command.Alias ?? (command.NormalizedName == "PACK" ? "archive" : "files");
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN302", $"{command.Name} output '{output}' is not a valid identifier.", command.Span));
            return null;
        }

        return new CommandSyntax([
            Token(command.NormalizedName == "PACK" ? "CREATEARCHIVE" : "EXTRACTARCHIVE", PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{source}}}", PromptTokenKind.Reference, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{goal}}}", PromptTokenKind.Reference, command.Span.Start)
        ], grammar);
    }

    private static CommandSyntax? LowerSqlMutation(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new("FLN330", "APPLY SQL requires a statement, for example `APPLY SQL \"UPDATE items SET done = 1\" AS changed`.", command.Span));
            return null;
        }

        string query = command.Values[0].UnquotedText.Trim();
        if (query.StartsWith("SQL ", StringComparison.OrdinalIgnoreCase)) query = query[4..].Trim();
        query = query.Trim('"', '\'');
        if (query.Length == 0)
        {
            diagnostics.Add(new("FLN331", "APPLY SQL requires a non-empty statement.", command.Span));
            return null;
        }

        string output = command.Alias ?? "affected";
        if (!IsIdentifier(output))
        {
            diagnostics.Add(new("FLN332", $"APPLY SQL output '{output}' is not a valid identifier.", command.Span));
            return null;
        }

        return new CommandSyntax([
            Token("APPLYSQL", PromptTokenKind.Word, command.Span.Start),
            Token($"[{output}]", PromptTokenKind.Variable, command.Span.Start),
            Token("FROM", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{query}}}", PromptTokenKind.Reference, command.Span.Start)
        ], grammar);
    }

    private static CommandSyntax? LowerLet(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new("FLN285", "LET requires `name = value`.", command.Span));
            return null;
        }

        string source = command.Values[0].UnquotedText;
        int equals = source.IndexOf('=');
        if (equals <= 0 || equals == source.Length - 1)
        {
            diagnostics.Add(new("FLN285", "LET requires `name = value`.", command.Span));
            return null;
        }

        string name = source[..equals].Trim();
        string value = source[(equals + 1)..].Trim();
        if (!IsIdentifier(name))
        {
            diagnostics.Add(new("FLN286", $"LET name '{name}' is not a valid identifier.", command.Span));
            return null;
        }

        string qualifier;
        if (value.StartsWith('{') || (value.StartsWith('[') && value.EndsWith(']')))
            qualifier = "JSON";
        else if (bool.TryParse(value, out _))
            qualifier = "BOOLEAN";
        else if (decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out _))
            qualifier = "NUMBER";
        else
            qualifier = "TEXT";

        if (qualifier == "JSON")
            value = NormalizeObjectLiteral(value);

        return new CommandSyntax([
            Token("SET", PromptTokenKind.Word, command.Span.Start),
            Token(qualifier, PromptTokenKind.Word, command.Span.Start),
            Token($"[{name}]", PromptTokenKind.Variable, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start),
            Token(value, Classify(value), command.Values[0].Span.Start + equals + 1)
        ], grammar);
    }

    private static bool IsIdentifier(string value) =>
        value.Length > 0 && (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');

    private static string[] SplitProcessCommandLine(string text)
    {
        List<string> parts = [];
        System.Text.StringBuilder current = new();
        char quote = '\0';
        bool escaping = false;
        foreach (char character in text)
        {
            if (escaping) { current.Append(character); escaping = false; continue; }
            if (character == '\\' && quote != '\'') { escaping = true; continue; }
            if ((character == '"' || character == '\'') && (quote == '\0' || quote == character))
            {
                quote = quote == '\0' ? character : '\0';
                continue;
            }
            if (char.IsWhiteSpace(character) && quote == '\0')
            {
                if (current.Length > 0) { parts.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(character);
        }
        if (escaping) current.Append('\\');
        if (quote != '\0') return [];
        if (current.Length > 0) parts.Add(current.ToString());
        return [.. parts];
    }

    private static bool TrySplitTrailingProcessDirectory(
        string text,
        out string commandLine,
        out string workingDirectory)
    {
        int separator = text.LastIndexOf(" IN ", StringComparison.OrdinalIgnoreCase);
        if (separator <= 0 || separator + 4 >= text.Length)
        {
            commandLine = text;
            workingDirectory = string.Empty;
            return false;
        }

        commandLine = text[..separator].Trim().Trim('"', '\'');
        workingDirectory = text[(separator + 4)..].Trim().Trim('"', '\'');
        return commandLine.Length > 0 && workingDirectory.Length > 0;
    }

    private static bool TrySplitTrailingProcessEnvironment(
        string text,
        out string commandLine,
        out string environment)
    {
        int separator = text.LastIndexOf(" ENV ", StringComparison.OrdinalIgnoreCase);
        if (separator <= 0 || separator + 5 >= text.Length)
        {
            commandLine = text;
            environment = string.Empty;
            return false;
        }

        commandLine = text[..separator].Trim().Trim('"', '\'');
        environment = text[(separator + 5)..].Trim().Trim('"', '\'');
        return commandLine.Length > 0 && environment.Length > 0;
    }

    private static string NormalizeObjectLiteral(string value) =>
        System.Text.RegularExpressions.Regex.Replace(
            value,
            @"([,{\[]\s*)([A-Za-z_][A-Za-z0-9_]*)(\s*:)",
            match => $"{match.Groups[1].Value}\"{match.Groups[2].Value}\"{match.Groups[3].Value}");

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

    private static PromptToken SurfaceReferenceToken(string value, int start) =>
        value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith(']')
            ? Token(value, PromptTokenKind.Variable, start)
            : value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith('}')
                ? Token(value, PromptTokenKind.Reference, start)
            : Token($"{{{value}}}", PromptTokenKind.Reference, start);

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

    private static IEnumerable<PromptToken> CredentialTokens(string? secret, int start) =>
        string.IsNullOrWhiteSpace(secret)
            ? []
            : [
                Token("USING", PromptTokenKind.Word, start),
                Token($"{{{secret}}}", PromptTokenKind.Reference, start)
            ];

    private sealed record LoweringContext(
        Uri? BaseUri,
        int? Retry,
        string? Timeout,
        string? AuthenticationSecret,
        string? ErrorPolicy,
        string? Condition);

    private static string CombineConditions(string? inherited, string condition) =>
        string.IsNullOrWhiteSpace(inherited)
            ? condition
            : $"({inherited}) AND ({condition})";

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
