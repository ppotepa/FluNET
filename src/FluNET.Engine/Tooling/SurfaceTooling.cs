using FluNET.Compilation;
using FluNET.Compilation.Dependencies;
using FluNET.Prompt.Surface;
using System.Text;

namespace FluNET.Tooling;

public sealed class SurfaceCheckService(SurfaceCompiler compiler)
{
    public SurfaceCompilationResult Check(string source) =>
        compiler.Compile(new SourceDocument(source));
}

public sealed class SurfaceFormatter
{
    public string Format(string source)
    {
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument(source));
        if (!parsed.IsValid)
        {
            throw new FormatException(string.Join(" | ", parsed.Diagnostics.Select(item => item.Message)));
        }
        StringBuilder result = new();
        WriteStatements(parsed.Program.Statements, 0, result);
        return result.ToString().TrimEnd();
    }

    private static void WriteStatements(
        IEnumerable<SurfaceStatementSyntax> statements,
        int indent,
        StringBuilder output)
    {
        string prefix = new(' ', indent);
        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfaceContextSyntax context)
            {
                output.Append(prefix).Append("FROM ").Append(context.BaseResource.Text).AppendLine();
                WriteStatements(context.Statements, indent + 4, output);
                continue;
            }
            if (statement is SurfacePipelineSyntax pipeline)
            {
                output.Append(prefix)
                    .Append(string.Join(" | ", pipeline.Stages.Select(FormatCommand)))
                    .AppendLine();
                continue;
            }
            output.Append(prefix).Append(FormatCommand((SurfaceCommandSyntax)statement)).AppendLine();
        }
    }

    private static string FormatCommand(SurfaceCommandSyntax command)
    {
        StringBuilder output = new(command.NormalizedName);
        if (command.Values.Count > 0)
        {
            output.Append(' ').Append(string.Join(", ", command.Values.Select(value => value.Text)));
        }
        if (command.Alias is not null)
        {
            output.Append(" AS ").Append(command.Alias);
        }
        return output.ToString();
    }
}

public sealed record SurfaceExplanation(
    SurfaceCompilationResult Compilation,
    string Text);

public sealed class SurfaceExplainService(SurfaceCompiler compiler)
{
    public SurfaceExplanation Explain(string source)
    {
        SurfaceCompilationResult result = compiler.Compile(new SourceDocument(source));
        StringBuilder text = new();
        text.AppendLine("SOURCE");
        text.AppendLine(source.TrimEnd());
        text.AppendLine().AppendLine("INFERENCE");
        foreach (var decision in result.Lowering.InferenceTrace.Items)
        {
            text.Append(decision.Kind).Append(' ')
                .Append(decision.Input).Append(" -> ")
                .Append(decision.Result).Append(" [")
                .Append(decision.Rule).AppendLine("]");
        }
        text.AppendLine().AppendLine("LOWERING");
        foreach (var pair in result.Lowering.CanonicalSyntax.Commands.Select((command, index) => (Command: command, Index: index)))
        {
            text.Append(pair.Index).Append(": ")
                .AppendLine(string.Join(' ', pair.Command.AllTokens.Select(token => token.Text)));
        }
        text.AppendLine().AppendLine("PLAN");
        if (result.DependencyGraph is null)
        {
            text.AppendLine("<not available>");
        }
        else
        {
            foreach (DependencyNode node in result.DependencyGraph.Nodes)
            {
                DependencyEdge[] incoming = result.DependencyGraph.Incoming(node.Index).ToArray();
                text.Append(node.Index).Append(": ").Append(node.Command.Frame.Id.Value)
                    .Append(" effect=").Append(node.Metadata.Effect)
                    .Append(" depends=")
                    .AppendLine(incoming.Length == 0
                        ? "-"
                        : string.Join(',', incoming.Select(edge =>
                            $"{edge.From}:{edge.Kind}{(edge.Variable is null ? string.Empty : $"[{edge.Variable}]")}")));
            }
        }
        if (!result.IsValid)
        {
            text.AppendLine().AppendLine("DIAGNOSTICS");
            foreach (SurfaceDiagnostic diagnostic in result.Lowering.Diagnostics)
            {
                text.Append(diagnostic.Code).Append(": ").AppendLine(diagnostic.Message);
            }
            foreach (CompilationDiagnostic diagnostic in result.Diagnostics)
            {
                text.Append(diagnostic.Code).Append(": ").AppendLine(diagnostic.Message);
            }
        }
        return new SurfaceExplanation(result, text.ToString().TrimEnd());
    }
}

public sealed class SurfaceGraphExporter
{
    public string ToDot(SurfaceCompilationResult compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        StringBuilder dot = new("digraph FluNET {\n");
        if (compilation.DependencyGraph is DependencyGraph graph)
        {
            foreach (DependencyNode node in graph.Nodes)
            {
                dot.Append("  n").Append(node.Index).Append(" [label=\"")
                    .Append(Escape(node.Command.Frame.Id.Value)).Append("\"];\n");
            }
            foreach (DependencyEdge edge in graph.Edges)
            {
                dot.Append("  n").Append(edge.From).Append(" -> n").Append(edge.To)
                    .Append(" [label=\"").Append(edge.Kind);
                if (edge.Variable is not null) dot.Append(':').Append(Escape(edge.Variable));
                dot.Append("\"];\n");
            }
        }
        dot.Append('}');
        return dot.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
