using FluNET.Compilation;
using FluNET.Compilation.Dependencies;
using FluNET.Execution.Commands;
using FluNET.Execution.Planning;
using FluNET.Prompt;
using FluNET.Prompt.Surface;
using System.Text;

namespace FluNET.Tooling;

public sealed class SurfaceCheckService(SurfaceCompiler compiler)
{ public SurfaceCompilationResult Check(string source) => compiler.Compile(new SourceDocument(source)); }

public sealed class SurfaceFormatter
{
    public string Format(string source)
    {
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument(source));
        if (!parsed.IsValid) throw new FormatException(string.Join(" | ", parsed.Diagnostics.Select(item => item.Message)));
        StringBuilder result = new(); WriteStatements(parsed.Program.Statements, 0, result); return result.ToString().TrimEnd();
    }
    private static void WriteStatements(IEnumerable<SurfaceStatementSyntax> statements,int indent,StringBuilder output)
    {
        string prefix=new(' ',indent);foreach(SurfaceStatementSyntax statement in statements)
        {
            switch(statement)
            {
                case SurfaceContextSyntax context:
                    output.Append(prefix).Append("FROM ").Append(context.BaseResource.Text).AppendLine();WriteStatements(context.Statements,indent+4,output);break;
                case SurfacePolicyDefinitionSyntax policy:
                    output.Append(prefix).Append("POLICY ").Append(policy.Name).AppendLine();WriteStatements(policy.Statements,indent+4,output);break;
                case SurfacePolicyContextSyntax policy:
                    output.Append(prefix).Append("WITH ").Append(policy.Name).AppendLine();WriteStatements(policy.Statements,indent+4,output);break;
                case SurfaceTaskDefinitionSyntax task:
                    output.Append(prefix).Append("TASK ").Append(task.Name);if(task.Parameters.Count>0)output.Append(' ').Append(string.Join(' ',task.Parameters));if(task.ResultTypeName is not null)output.Append(" RETURNS ").Append(task.ResultTypeName);output.AppendLine();WriteStatements(task.Statements,indent+4,output);break;
                case SurfacePipelineSyntax pipeline:
                    output.Append(prefix).Append(string.Join(" | ",pipeline.Stages.Select(FormatCommand))).AppendLine();break;
                case SurfaceCommandSyntax command:
                    output.Append(prefix).Append(FormatCommand(command)).AppendLine();break;
            }
        }
    }
    private static string FormatCommand(SurfaceCommandSyntax command){StringBuilder output=new(command.NormalizedName);if(command.Values.Count>0)output.Append(' ').Append(string.Join(", ",command.Values.Select(value=>value.Text)));if(command.Alias is not null)output.Append(" AS ").Append(command.Alias);return output.ToString();}
}

public sealed record SurfaceExplanation(SurfaceCompilationResult Compilation,string Text);

public sealed class SurfaceExplainService(SurfaceCompiler compiler)
{
    public SurfaceExplanation Explain(string source) => Explain(new SourceDocument(source));

    public SurfaceExplanation Explain(SourceDocument document)
    {
        SurfaceCompilationResult result=compiler.Compile(document);StringBuilder text=new();
        text.AppendLine("SOURCE").AppendLine(document.Text.TrimEnd());
        text.AppendLine().AppendLine("INFERENCE");foreach(var decision in result.Lowering.InferenceTrace.Items)text.Append(decision.Kind).Append(' ').Append(decision.Input).Append(" -> ").Append(decision.Result).Append(" [").Append(decision.Rule).AppendLine("]");
        text.AppendLine().AppendLine("LOWERING");foreach(var pair in result.Lowering.CanonicalSyntax.Commands.Select((command,index)=>(Command:command,Index:index)))text.Append(pair.Index).Append(": ").AppendLine(string.Join(' ',pair.Command.AllTokens.Select(token=>token.Text)));
        text.AppendLine().AppendLine("PLAN");if(result.DependencyGraph is null)text.AppendLine("<not available>");else foreach(DependencyNode node in result.DependencyGraph.Nodes){DependencyEdge[]incoming=result.DependencyGraph.Incoming(node.Index).ToArray();text.Append(node.Index).Append(": ").Append(node.Command.Frame.Id.Value).Append(" effect=").Append(node.Metadata.Effect).Append(" concurrency=").Append(node.Metadata.Concurrency).Append(" depends=").AppendLine(incoming.Length==0?"-":string.Join(',',incoming.Select(edge=>$"{edge.From}:{edge.Kind}{(edge.Variable is null?string.Empty:$"[{edge.Variable}]")}")));}
        if(result.Plan is not null)
        {
            text.AppendLine().AppendLine("EXECUTION POLICIES");
            foreach(ExecutionPlanStep step in result.Plan.Steps)
            {
                CommandExecutionPolicy p=step.Policy;text.Append(step.Index).Append(": retry=").Append(p.RetryCount).Append(" timeout=").Append(p.Timeout?.ToString()??"-").Append(" error=").Append(p.ErrorBehavior);
                if(p.Backoff is not null)text.Append(" backoff=").Append(p.Backoff.Kind).Append(':').Append(p.Backoff.BaseDelay).Append(" jitter=").Append(p.Backoff.JitterFraction.ToString("P0",System.Globalization.CultureInfo.InvariantCulture));
                AppendCodes(text," retryOn",p.RetryOnStatusCodes);AppendCodes(text," continueOn",p.ContinueOnStatusCodes);AppendCodes(text," failOn",p.FailOnStatusCodes);text.AppendLine();
            }
        }
        if(result.BoundProgram is not null)
        {
            text.AppendLine().AppendLine("EXECUTION ARTIFACTS");bool any=false;
            for(int index=0;index<result.BoundProgram.Commands.Count;index++)
            {
                var command=result.BoundProgram.Commands[index];List<string>artifacts=[];
                if(CommandExecutionArtifactStore.TryGetCache(command,out ExecutionCachePolicy?cache)&&cache is not null)artifacts.Add($"CACHE ttl={cache.Ttl}");
                if(CommandExecutionArtifactStore.TryGetIdempotency(command,out ExecutionIdempotencyPolicy?once)&&once is not null)artifacts.Add($"ONCE BY {once.KeyExpression}");
                if(artifacts.Count>0){any=true;text.Append(index).Append(": ").AppendLine(string.Join("; ",artifacts));}
            }
            if(!any)text.AppendLine("-");
        }
        if(!result.IsValid){text.AppendLine().AppendLine("DIAGNOSTICS");foreach(SurfaceDiagnostic diagnostic in result.Lowering.Diagnostics)text.Append(diagnostic.Code).Append(": ").AppendLine(diagnostic.Message);foreach(CompilationDiagnostic diagnostic in result.Diagnostics)text.Append(diagnostic.Code).Append(" [").Append(diagnostic.Phase).Append("]: ").AppendLine(diagnostic.Message);}
        return new(result,text.ToString().TrimEnd());
    }
    private static void AppendCodes(StringBuilder text,string label,IReadOnlyList<int>?codes){if(codes is{Count:>0})text.Append(label).Append('=').Append(string.Join(',',codes));}
}

public sealed class SurfaceGraphExporter
{
    public string ToDot(SurfaceCompilationResult compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        StringBuilder dot = new("digraph FluNET {\n");
        dot.AppendLine("  rankdir=LR;");
        dot.AppendLine("  node [shape=box, fontname=\"Segoe UI\"];");

        AddNode(dot, "source", "SourceDocument", "lightblue");
        AddNode(dot, "lexer", "Lexer / TokenStream", "lightblue");
        AddNode(dot, "segmenter", "SentenceSegmenter", "lightblue");
        AddNode(dot, "parser", "SentenceParser / SurfaceParser", "lightyellow");
        AddNode(dot, "lowering", "SurfaceLowerer", "lightyellow");
        AddNode(dot, "binder", "SemanticBinder", "lightyellow");
        AddNode(dot, "planner", "ExecutionPlanner", "lightgreen");
        AddNode(dot, "executor", "SentenceExecutor", "orange");

        dot.AppendLine("  source -> lexer;");
        dot.AppendLine("  lexer -> segmenter;");
        dot.AppendLine("  segmenter -> parser;");
        dot.AppendLine("  parser -> lowering;");
        dot.AppendLine("  lowering -> binder;");
        dot.AppendLine("  binder -> planner;");
        dot.AppendLine("  planner -> executor;");

        foreach (Sentence sentence in compilation.Document.Sentences)
        {
            string id = $"sentence_{sentence.Index}";
            AddNode(dot, id, $"Sentence {sentence.Index}\\n{sentence.Text}", "aliceblue");
            dot.Append("  segmenter -> ").Append(id).AppendLine(";");
            dot.Append("  ").Append(id).AppendLine(" -> parser;");
        }

        for (int index = 0; index < compilation.Lowering.SurfaceProgram.Statements.Count; index++)
        {
            SurfaceStatementSyntax statement = compilation.Lowering.SurfaceProgram.Statements[index];
            string id = $"surface_{index}";
            AddNode(dot, id, $"Surface {index}\\n{statement.GetType().Name}", "lemonchiffon");
            dot.Append("  parser -> ").Append(id).AppendLine(";");
            Sentence? sentence = statement.SentenceIndex >= 0
                ? compilation.Document.Sentences.ElementAtOrDefault(statement.SentenceIndex)
                : compilation.Document.FindSentence(statement.Span);
            if (sentence is not null)
                dot.Append("  sentence_").Append(sentence.Index).Append(" -> ").Append(id).AppendLine(" [style=dashed];");
            dot.Append("  ").Append(id).AppendLine(" -> lowering;");
        }

        for (int index = 0; index < compilation.Lowering.CanonicalSyntax.Commands.Count; index++)
        {
            string id = $"lowered_{index}";
            CommandSyntax command = compilation.Lowering.CanonicalSyntax.Commands[index];
            AddNode(dot, id, $"Lowered {index}\\n{string.Join(' ', command.AllTokens.Select(token => token.Text))}", "palegreen");
            dot.Append("  lowering -> ").Append(id).AppendLine(";");
            SourceSpan? span = compilation.Lowering.SourceMap.FindCommand(index);
            if (span is SourceSpan sourceSpan)
            {
                Sentence? sentence = compilation.Document.FindSentence(sourceSpan);
                if (sentence is not null)
                    dot.Append("  sentence_").Append(sentence.Index).Append(" -> ").Append(id).AppendLine(" [style=dotted];");
            }
        }

        if (compilation.DependencyGraph is DependencyGraph graph)
        {
            foreach (DependencyNode node in graph.Nodes)
            {
                string id = $"plan_{node.Index}";
                AddNode(dot, id, $"Plan {node.Index}\\n{node.Command.Frame.Id.Value}\\n{node.Metadata.Effect}/{node.Metadata.Concurrency}", "moccasin");
                dot.Append("  planner -> ").Append(id).AppendLine(";");
                dot.Append("  lowered_").Append(node.Index).Append(" -> ").Append(id).AppendLine(" [style=dashed];");
                dot.Append("  ").Append(id).AppendLine(" -> executor;");
                string? capability = node.Command.Frame.Id.Value switch
                {
                    "surface.files.scan.json" => "filesystem.scan",
                    "surface.files.list.json" => "filesystem.directory",
                    "surface.files.stat" => "filesystem.directory",
                    "surface.files.hash" => "filesystem.hash",
                    "surface.system.info" => "system.info",
                    "surface.system.path" => "system.path",
                    "surface.system.temp.file" or "surface.system.temp.directory" or "surface.system.temp.cleanup" => "system.temp",
                    "surface.system.now" or "surface.system.wait" => "system.time",
                    "storage.put.value" or "storage.read.value" => "storage.keyvalue",
                    "storage.blob.get" or "storage.blob.put" or "storage.blob.delete" => "storage.blob",
                    "surface.get.sql" => "database.sql",
                    "system.process.run" => "system.process",
                    "system.process.session.start" or "system.process.session.send" or "system.process.session.stop" => "system.process",
                    "filesystem.archive.create" or "filesystem.archive.extract" => "filesystem.archive",
                    "filesystem.directory.create" => "filesystem.directory",
                    "filesystem.directory.copy" or "filesystem.directory.move" => "filesystem.directory",
                    "filesystem.directory.trash" => "filesystem.trash",
                    "filesystem.trash.restore.file" or "filesystem.trash.restore.directory" => "filesystem.trash",
                    "surface.files.copy" or "surface.files.move" or "surface.files.trash" => "filesystem.write",
                    "surface.get.environment" => "system.environment",
                    "surface.system.environment.write" => "system.environment.write",
                    "surface.get.secret" => "system.secrets",
                    "surface.system.notify" => "system.notify",
                    "messaging.publish" => "messaging.queue",
                    "messaging.receive" => "messaging.queue",
                    "surface.get.http.json" or "surface.get.http.text" or "surface.get.http.csv" or
                    "surface.get.http.xml" or "surface.get.http.binary" or "surface.get.http.image" or
                    "core.post.json" or "core.download.file" => "network.http",
                    "core.put.json" or "core.patch.json" or "core.delete.http" => "network.http",
                    "surface.system.clipboard.read" or "surface.system.clipboard.write" => "system.clipboard",
                    _ => null
                };
                if (capability is not null)
                {
                    string capabilityId = "cap_" + capability.Replace('.', '_');
                    AddNode(dot, capabilityId, $"Capability\\n{capability}", "orange");
                    dot.Append("  ").Append(id).Append(" -> ").Append(capabilityId).AppendLine(";");
                    dot.Append("  ").Append(capabilityId).AppendLine(" -> provider [label=\"resolve\"];");
                }
            }

            foreach (DependencyEdge edge in graph.Edges)
            {
                dot.Append("  plan_").Append(edge.From).Append(" -> plan_").Append(edge.To).Append(" [label=\"").Append(edge.Kind);
                if (edge.Variable is not null)
                    dot.Append(':').Append(Escape(edge.Variable));
                dot.AppendLine("\"];");
            }
        }

        dot.AppendLine("  executor -> capability [label=\"dispatch\"];");
        AddNode(dot, "capability", "Capability / ProviderResolver", "orange");
        dot.AppendLine("  capability -> provider [label=\"platform\"];");
        AddNode(dot, "provider", "Portable / Windows / Linux / macOS Provider", "lightsalmon");
        dot.Append('}');
        return dot.ToString();
    }

    private static void AddNode(StringBuilder dot, string id, string label, string color) =>
        dot.Append("  ").Append(id).Append(" [label=\"").Append(Escape(label)).Append("\", style=filled, fillcolor=\"").Append(color).AppendLine("\"];");

    private static string Escape(string value)=>value.Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal);
}
