using FluNET.Compilation.Lowering;
using FluNET.Execution.Commands;
using FluNET.Execution.Planning;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Policies;

public sealed record SurfacePolicyProfile(
    int? Retry,
    string? Timeout,
    bool ContinueOnError,
    RetryBackoffPolicy? Backoff = null,
    IReadOnlyList<int>? RetryOn = null,
    IReadOnlyList<int>? ContinueOn = null,
    IReadOnlyList<int>? FailOn = null)
{
    public static SurfacePolicyProfile Empty { get; } = new(null, null, false);
    public SurfacePolicyProfile Merge(SurfacePolicyProfile other) => new(
        other.Retry ?? Retry,
        other.Timeout ?? Timeout,
        ContinueOnError || other.ContinueOnError,
        other.Backoff ?? Backoff,
        MergeCodes(RetryOn, other.RetryOn),
        MergeCodes(ContinueOn, other.ContinueOn),
        MergeCodes(FailOn, other.FailOn));
    private static IReadOnlyList<int>? MergeCodes(IReadOnlyList<int>? left, IReadOnlyList<int>? right) =>
        right is { Count: > 0 } ? (left ?? []).Concat(right).Distinct().Order().ToArray() : left;
}

public sealed record SurfacePolicyCompilationResult(
    SurfaceParseResult Parse,
    IReadOnlyDictionary<SourceSpan, SurfacePolicyProfile> Assignments,
    IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{ public bool IsValid => Diagnostics.Count == 0; }

public sealed class SurfacePolicyCompiler
{
    public SurfacePolicyCompilationResult Compile(SurfaceParseResult parse)
    {
        Dictionary<string, SurfacePolicyProfile> profiles = new(StringComparer.OrdinalIgnoreCase);
        List<SurfaceDiagnostic> diagnostics = [.. parse.Diagnostics];
        CollectDefinitions(parse.Program.Statements, profiles, diagnostics);
        Dictionary<SourceSpan, SurfacePolicyProfile> assignments = [];
        List<SurfaceStatementSyntax> executable = Rewrite(parse.Program.Statements, profiles, SurfacePolicyProfile.Empty, assignments, diagnostics);
        SourceSpan span = executable.Count == 0 ? default : SourceSpan.FromBounds(executable[0].Span.Start, executable[^1].Span.End);
        return new(new SurfaceParseResult(parse.Document, new SurfaceProgramSyntax(executable, span), diagnostics), assignments, diagnostics);
    }

    private static void CollectDefinitions(IEnumerable<SurfaceStatementSyntax> statements, IDictionary<string, SurfacePolicyProfile> profiles, ICollection<SurfaceDiagnostic> diagnostics)
    {
        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfacePolicyDefinitionSyntax definition)
            {
                if (!profiles.TryAdd(definition.Name, ParseProfile(definition, diagnostics)))
                    diagnostics.Add(new("FLN285", $"Policy '{definition.Name}' is declared more than once.", definition.Span));
            }
            else if (statement is SurfaceContextSyntax context) CollectDefinitions(context.Statements, profiles, diagnostics);
            else if (statement is SurfacePolicyContextSyntax policy) CollectDefinitions(policy.Statements, profiles, diagnostics);
        }
    }

    private static SurfacePolicyProfile ParseProfile(SurfacePolicyDefinitionSyntax definition, ICollection<SurfaceDiagnostic> diagnostics)
    {
        int? retry = null; string? timeout = null; bool continueOnError = false;
        RetryBackoffPolicy? backoff = null; double? jitter = null;
        IReadOnlyList<int>? retryOn = null, continueOn = null, failOn = null;
        foreach (SurfaceStatementSyntax statement in definition.Statements)
        {
            if (statement is not SurfaceCommandSyntax command)
            { diagnostics.Add(new("FLN286", "POLICY bodies may contain RETRY, TIMEOUT, CONTINUE, FAIL, BACKOFF and JITTER.", statement.Span)); continue; }
            string value = string.Join(", ", command.Values.Select(item => item.UnquotedText.Trim())).Trim();
            switch (command.NormalizedName)
            {
                case "RETRY":
                    if (!ParseRetry(value, ref retry, ref retryOn)) diagnostics.Add(new("FLN286", $"Invalid RETRY directive '{value}'.", command.Span));
                    break;
                case "TIMEOUT" when value.Length > 0: timeout = value; break;
                case "CONTINUE" when value.Length == 0 || value.Equals("ON ERROR", StringComparison.OrdinalIgnoreCase): continueOnError = true; break;
                case "CONTINUE" when TryOnCodes(value, out int[]? codes): continueOn = codes; break;
                case "FAIL" when TryOnCodes(value, out int[]? codes): failOn = codes; break;
                case "BACKOFF" when TryBackoff(value, out RetryBackoffPolicy? parsed): backoff = parsed; break;
                case "JITTER" when TryJitter(value, out double fraction): jitter = fraction; break;
                default: diagnostics.Add(new("FLN286", $"Unsupported policy directive '{command.Name} {value}'.", command.Span)); break;
            }
        }
        if (jitter is double jitterValue)
        {
            if (backoff is null) diagnostics.Add(new("FLN287", "JITTER requires BACKOFF in the same effective policy.", definition.Span));
            else backoff = backoff with { JitterFraction = jitterValue };
        }
        return new(retry, timeout, continueOnError, backoff, retryOn, continueOn, failOn);
    }

    private static bool ParseRetry(string value, ref int? retry, ref IReadOnlyList<int>? retryOn)
    {
        if (int.TryParse(value, out int count) && count is >= 0 and <= 100) { retry = count; return true; }
        int marker = value.IndexOf(" ON ", StringComparison.OrdinalIgnoreCase);
        string statusText;
        if (value.StartsWith("ON ", StringComparison.OrdinalIgnoreCase)) { statusText = value[3..]; retry ??= 3; }
        else if (marker > 0 && int.TryParse(value[..marker].Trim(), out count) && count is >= 0 and <= 100) { retry = count; statusText = value[(marker + 4)..]; }
        else return false;
        if (!TryCodes(statusText, out int[]? codes)) return false;
        retryOn = codes; return true;
    }

    private static bool TryOnCodes(string value, out int[]? codes)
    { if (!value.StartsWith("ON ", StringComparison.OrdinalIgnoreCase)) { codes = null; return false; } return TryCodes(value[3..], out codes); }
    private static bool TryCodes(string source, out int[]? codes)
    {
        string[] items = source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (items.Length == 0) { codes = null; return false; }
        List<int> result = [];
        foreach (string item in items) if (!int.TryParse(item, out int status) || status is < 100 or > 599) { codes = null; return false; } else result.Add(status);
        codes = result.Distinct().Order().ToArray(); return true;
    }
    private static bool TryBackoff(string value, out RetryBackoffPolicy? policy)
    {
        string[] parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        RetryBackoffKind kind = RetryBackoffKind.Fixed; string duration;
        if (parts.Length == 1) duration = parts[0];
        else if (parts.Length == 2 && Enum.TryParse(parts[0], true, out kind)) duration = parts[1];
        else { policy = null; return false; }
        if (!TryDuration(duration, out TimeSpan delay)) { policy = null; return false; }
        policy = new(delay, kind, 0); return true;
    }
    private static bool TryJitter(string value, out double fraction)
    {
        string text = value.Trim(); if (text.EndsWith('%')) text = text[..^1];
        if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double percent) || percent is < 0 or > 100)
        { fraction = 0; return false; }
        fraction = percent / 100d; return true;
    }
    private static bool TryDuration(string value, out TimeSpan duration)
    {
        string t=value.Trim().ToLowerInvariant();(string Number,double Seconds)p=t switch{_ when t.EndsWith("ms")=>(t[..^2],.001),_ when t.EndsWith('s')=>(t[..^1],1),_ when t.EndsWith('m')=>(t[..^1],60),_ when t.EndsWith('h')=>(t[..^1],3600),_ when t.EndsWith('d')=>(t[..^1],86400),_=>(string.Empty,0)};
        if(p.Number.Length==0||!double.TryParse(p.Number,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double n)||n<=0||!double.IsFinite(n*p.Seconds)){duration=default;return false;}duration=TimeSpan.FromSeconds(n*p.Seconds);return duration<=TimeSpan.FromDays(365);
    }

    private static List<SurfaceStatementSyntax> Rewrite(IEnumerable<SurfaceStatementSyntax> statements,IReadOnlyDictionary<string,SurfacePolicyProfile> profiles,SurfacePolicyProfile inherited,IDictionary<SourceSpan,SurfacePolicyProfile> assignments,ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceStatementSyntax> output=[];foreach(SurfaceStatementSyntax statement in statements){if(statement is SurfacePolicyDefinitionSyntax)continue;if(statement is SurfacePolicyContextSyntax context){if(!profiles.TryGetValue(context.Name,out SurfacePolicyProfile? profile)){diagnostics.Add(new("FLN289",$"Unknown policy profile '{context.Name}'.",context.Span));continue;}output.AddRange(Rewrite(context.Statements,profiles,inherited.Merge(profile),assignments,diagnostics));continue;}if(statement is SurfaceContextSyntax resource){output.Add(resource with{Statements=Rewrite(resource.Statements,profiles,inherited,assignments,diagnostics)});continue;}if(statement is SurfacePipelineSyntax pipeline){output.Add(pipeline with{Stages=pipeline.Stages.Select(stage=>RewriteCommand(stage,profiles,inherited,assignments,diagnostics)).ToArray()});continue;}if(statement is SurfaceCommandSyntax command){output.Add(RewriteCommand(command,profiles,inherited,assignments,diagnostics));continue;}output.Add(statement);}return output;
    }
    private static SurfaceCommandSyntax RewriteCommand(SurfaceCommandSyntax command,IReadOnlyDictionary<string,SurfacePolicyProfile> profiles,SurfacePolicyProfile inherited,IDictionary<SourceSpan,SurfacePolicyProfile> assignments,ICollection<SurfaceDiagnostic> diagnostics)
    {
        SurfacePolicyProfile effective=inherited;SurfaceCommandSyntax rewritten=command;if(command.Values.Count>0){SurfaceValueSyntax last=command.Values[^1];int marker=last.Text.LastIndexOf(" USING ",StringComparison.OrdinalIgnoreCase);if(marker>0){string name=last.Text[(marker+7)..].Trim();if(!profiles.TryGetValue(name,out SurfacePolicyProfile? profile))diagnostics.Add(new("FLN289",$"Unknown policy profile '{name}'.",last.Span));else effective=effective.Merge(profile);SurfaceValueSyntax[] values=[..command.Values.Take(command.Values.Count-1),new SurfaceValueSyntax(last.Text[..marker].TrimEnd(),last.Span)];rewritten=command with{Values=values};}}if(effective!=SurfacePolicyProfile.Empty)assignments[rewritten.Span]=effective;return rewritten;
    }
}

public static class SurfacePolicyApplicationPass
{
    public static LoweringResult Apply(LoweringResult lowering,IReadOnlyDictionary<SourceSpan,SurfacePolicyProfile> assignments,PromptGrammar grammar)
    {
        if(assignments.Count==0)return lowering;CommandSyntax[]commands=lowering.CanonicalSyntax.Commands.ToArray();foreach(SourceMapEntry entry in lowering.SourceMap.Entries.Where(item=>item.NodeKind=="command")){if(!assignments.TryGetValue(entry.SourceSpan,out SurfacePolicyProfile? profile))continue;commands[entry.CommandIndex]=Apply(commands[entry.CommandIndex],profile,grammar,entry.SourceSpan.Start);}return lowering with{CanonicalSyntax=new PromptSyntax(commands,lowering.CanonicalSyntax.Links)};
    }
    private static CommandSyntax Apply(CommandSyntax command,SurfacePolicyProfile profile,PromptGrammar grammar,int position){List<PromptToken>tokens=[..command.AllTokens];if(profile.Retry is int retry){tokens.Add(T("WITH",position));tokens.Add(T("RETRY",position));tokens.Add(T(retry.ToString(System.Globalization.CultureInfo.InvariantCulture),position));}if(profile.Timeout is string timeout){tokens.Add(T("WITH",position));tokens.Add(T("TIMEOUT",position));tokens.Add(T(timeout,position));}if(profile.ContinueOnError){tokens.Add(T("ON",position));tokens.Add(T("ERROR",position));tokens.Add(T("CONTINUE",position));}return new(tokens,grammar);}private static PromptToken T(string text,int position)=>new(text,PromptTokenKind.Word,Math.Max(0,position),0);
}

public static class SurfaceAdvancedPolicyPass
{
    public static void Attach(BoundProgram program,SourceMap sourceMap,IReadOnlyDictionary<SourceSpan,SurfacePolicyProfile> assignments)
    {
        foreach(SourceMapEntry entry in sourceMap.Entries.Where(item=>item.NodeKind=="command")){if(!assignments.TryGetValue(entry.SourceSpan,out SurfacePolicyProfile? profile))continue;AdvancedExecutionPolicy advanced=new(profile.Backoff,profile.RetryOn,profile.ContinueOn,profile.FailOn);if(!advanced.IsEmpty)CommandExecutionArtifactStore.SetAdvancedPolicy(program.Commands[entry.CommandIndex],advanced);}
    }
}
