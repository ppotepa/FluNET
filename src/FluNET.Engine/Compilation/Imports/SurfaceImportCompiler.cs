using FluNET.Prompt.Surface;
using FluNET.Prompt;

namespace FluNET.Compilation.Imports;

/// <summary>Expands local source modules before task, policy and lowering passes.</summary>
internal static class SurfaceImportCompiler
{
    public static SurfaceParseResult Expand(SurfaceParseResult root)
    {
        List<SurfaceDiagnostic> diagnostics = [.. root.Diagnostics];
        HashSet<string> active = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<SurfaceStatementSyntax> statements = ExpandStatements(root.Document, root.Program.Statements, active, diagnostics);
        SourceSpan span = statements.Count == 0 ? default : SourceSpan.FromBounds(statements[0].Span.Start, statements[^1].Span.End);
        return new SurfaceParseResult(root.Document, new SurfaceProgramSyntax(statements, span), diagnostics);
    }

    private static IReadOnlyList<SurfaceStatementSyntax> ExpandStatements(
        SourceDocument owner,
        IReadOnlyList<SurfaceStatementSyntax> statements,
        HashSet<string> active,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceStatementSyntax> expanded = [];
        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfaceContextSyntax context)
            {
                expanded.Add(context with
                {
                    Statements = ExpandStatements(owner, context.Statements, active, diagnostics)
                });
                continue;
            }
            if (statement is SurfacePolicyDefinitionSyntax policy)
            {
                expanded.Add(policy with
                {
                    Statements = ExpandStatements(owner, policy.Statements, active, diagnostics)
                });
                continue;
            }
            if (statement is SurfacePolicyContextSyntax policyContext)
            {
                expanded.Add(policyContext with
                {
                    Statements = ExpandStatements(owner, policyContext.Statements, active, diagnostics)
                });
                continue;
            }
            if (statement is SurfaceTaskDefinitionSyntax task)
            {
                expanded.Add(task with
                {
                    Statements = ExpandStatements(owner, task.Statements, active, diagnostics)
                });
                continue;
            }
            if (statement is SurfaceRepeatSyntax repeat)
            {
                expanded.Add(repeat with
                {
                    Statements = ExpandStatements(owner, repeat.Statements, active, diagnostics)
                });
                continue;
            }
            if (statement is not SurfaceCommandSyntax command || command.NormalizedName != "IMPORT")
            {
                expanded.Add(statement);
                continue;
            }

            if (command.Values.Count != 1)
            {
                diagnostics.Add(new("FLN380", "IMPORT requires exactly one local .flu file path.", command.Span));
                continue;
            }

            string requested = command.Values[0].UnquotedText.Trim();
            if (requested.Length == 0 || requested.Contains('\0') ||
                Uri.TryCreate(requested, UriKind.Absolute, out Uri? uri) && uri.Scheme is not "file")
            {
                diagnostics.Add(new("FLN381", "IMPORT accepts a non-empty local file path; network imports are not supported.", command.Span));
                continue;
            }

            string baseDirectory = owner.Path is null
                ? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(Path.GetFullPath(owner.Path)) ?? Directory.GetCurrentDirectory();
            string path = Path.GetFullPath(requested.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                ? new Uri(requested).LocalPath
                : Path.Combine(baseDirectory, requested));
            if (!path.EndsWith(".flu", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".flunet", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new("FLN382", "Imported modules must use the .flu or .flunet extension.", command.Span));
                continue;
            }
            if (!File.Exists(path))
            {
                diagnostics.Add(new("FLN383", $"Imported module '{requested}' was not found.", command.Span));
                continue;
            }

            string identity = Path.GetFullPath(path);
            if (!active.Add(identity))
            {
                diagnostics.Add(new("FLN384", $"Import cycle detected at '{requested}'.", command.Span));
                continue;
            }
            try
            {
                SourceDocument importedDocument = new(File.ReadAllText(identity), owner.SyntaxKind, identity);
                SurfaceParseResult imported = new SurfaceParser().Parse(importedDocument);
                foreach (SurfaceDiagnostic diagnostic in imported.Diagnostics)
                    diagnostics.Add(diagnostic);
                expanded.AddRange(ExpandStatements(importedDocument, imported.Program.Statements, active, diagnostics));
            }
            catch (IOException exception)
            {
                diagnostics.Add(new("FLN385", $"Could not import '{requested}': {exception.Message}", command.Span));
            }
            finally
            {
                active.Remove(identity);
            }
        }
        return expanded;
    }
}
