using FluNET.Compilation;
using FluNET.Compilation.Dependencies;
using FluNET.Execution.Commands;
using FluNET.Execution.Planning;
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
                    output.Append(prefix).Append("TASK ").Append(task.Name);if(task.Parameters.Count>0)output.Append(' ').Append(string.Join(' ',task.Parameters));if(task.ResultTypeName is not null)output.Append(" -> ").Append(task.ResultTypeName);output.AppendLine();WriteStatements(task.Statements,indent+4,output);break;
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
    public SurfaceExplanation Explain(string source)
    {
        SurfaceCompilationResult result=compiler.Compile(new SourceDocument(source));StringBuilder text=new();
        text.AppendLine("SOURCE").AppendLine(source.TrimEnd());
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
        ArgumentNullException.ThrowIfNull(compilation);StringBuilder dot=new("digraph FluNET {\n");if(compilation.DependencyGraph is DependencyGraph graph){foreach(DependencyNode node in graph.Nodes)dot.Append("  n").Append(node.Index).Append(" [label=\"").Append(Escape(node.Command.Frame.Id.Value)).Append("\\n").Append(node.Metadata.Effect).Append('/').Append(node.Metadata.Concurrency).Append("\"];\n");foreach(DependencyEdge edge in graph.Edges){dot.Append("  n").Append(edge.From).Append(" -> n").Append(edge.To).Append(" [label=\"").Append(edge.Kind);if(edge.Variable is not null)dot.Append(':').Append(Escape(edge.Variable));dot.Append("\"];\n");}}dot.Append('}');return dot.ToString();
    }
    private static string Escape(string value)=>value.Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal);
}
