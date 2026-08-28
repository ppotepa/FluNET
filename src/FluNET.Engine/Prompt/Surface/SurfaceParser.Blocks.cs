namespace FluNET.Prompt.Surface;

public sealed partial class SurfaceParser
{
    private static bool TryParseBlockStatement(
        SurfaceStatementSyntax parsed,
        LineInfo line,
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<SurfaceStatementSyntax> statements)
    {
        if (parsed is not SurfaceCommandSyntax command)
            return false;

        switch (command.NormalizedName)
        {
            case "FROM":
                ParseFromBlock(command, line, lines, ref cursor, indent, diagnostics, statements);
                return true;
            case "FOR":
                ParseForBlock(command, line, lines, ref cursor, indent, diagnostics, statements);
                return true;
            case "POLICY":
                ParsePolicyBlock(command, line, lines, ref cursor, indent, diagnostics, statements);
                return true;
            case "WITH":
                ParseWithBlock(command, line, lines, ref cursor, indent, diagnostics, statements);
                return true;
            case "TASK":
                ParseTaskBlock(command, line, lines, ref cursor, indent, diagnostics, statements);
                return true;
            case "REPEAT":
                ParseRepeatBlock(command, line, lines, ref cursor, indent, diagnostics, statements);
                return true;
            case "WHILE":
                ParseWhileBlock(command, line, lines, ref cursor, indent, diagnostics, statements);
                return true;
            case "IF":
            case "UNLESS":
                ParseConditionalBlock(command, line, lines, ref cursor, indent, diagnostics, statements);
                return true;
            default:
                return false;
        }
    }

    private static void ParseFromBlock(
        SurfaceCommandSyntax command,
        LineInfo line,
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<SurfaceStatementSyntax> statements)
    {
        if (command.Values.Count != 1 || command.Alias is not null)
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN205",
                "A FROM context requires exactly one base resource and no AS alias.",
                command.Span));
            return;
        }

        if (!TryReadChildBlock(
                lines,
                ref cursor,
                indent,
                diagnostics,
                command.Span,
                "FROM context",
                out IReadOnlyList<SurfaceStatementSyntax>? children))
        {
            return;
        }

        statements.Add(new SurfaceContextSyntax(
            command.Values[0],
            children!,
            SourceSpan.FromBounds(command.Span.Start, children![^1].Span.End))
        {
            SentenceIndex = line.SentenceIndex
        });
    }

    private static void ParseForBlock(
        SurfaceCommandSyntax command,
        LineInfo line,
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<SurfaceStatementSyntax> statements)
    {
        if (!TryReadChildBlock(
                lines,
                ref cursor,
                indent,
                diagnostics,
                command.Span,
                "FOR EACH",
                out IReadOnlyList<SurfaceStatementSyntax>? children))
        {
            return;
        }

        SourceSpan loopSpan = SourceSpan.FromBounds(command.Span.Start, children![^1].Span.End);
        if (!SurfaceForEachDescriptor.TryCreate(
                command,
                children,
                diagnostics,
                out SurfaceForEachDescriptor? descriptor))
        {
            return;
        }

        statements.Add(new SurfaceCommandSyntax(
            "FOREACH",
            [new SurfaceValueSyntax(descriptor!.Encode(), loopSpan)],
            null,
            loopSpan)
        {
            SentenceIndex = line.SentenceIndex
        });
    }

    private static void ParsePolicyBlock(
        SurfaceCommandSyntax command,
        LineInfo line,
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<SurfaceStatementSyntax> statements)
    {
        if (!TrySingleName(command, "POLICY", diagnostics, out string? name) ||
            !TryReadChildBlock(
                lines,
                ref cursor,
                indent,
                diagnostics,
                command.Span,
                "POLICY definition",
                out IReadOnlyList<SurfaceStatementSyntax>? children))
        {
            return;
        }

        statements.Add(new SurfacePolicyDefinitionSyntax(
            name!,
            children!,
            SourceSpan.FromBounds(command.Span.Start, children![^1].Span.End))
        {
            SentenceIndex = line.SentenceIndex
        });
    }

    private static void ParseWithBlock(
        SurfaceCommandSyntax command,
        LineInfo line,
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<SurfaceStatementSyntax> statements)
    {
        string inlineValue = command.Values.Count == 1
            ? command.Values[0].UnquotedText.Trim()
            : string.Empty;
        bool inlinePolicy = inlineValue.Contains(' ') ||
            inlineValue.StartsWith("RETRY", StringComparison.OrdinalIgnoreCase) ||
            inlineValue.StartsWith("TIMEOUT", StringComparison.OrdinalIgnoreCase);
        if (inlinePolicy)
        {
            statements.Add(command);
            return;
        }

        if (!TrySingleName(command, "WITH", diagnostics, out string? name) ||
            !TryReadChildBlock(
                lines,
                ref cursor,
                indent,
                diagnostics,
                command.Span,
                "WITH policy",
                out IReadOnlyList<SurfaceStatementSyntax>? children))
        {
            return;
        }

        statements.Add(new SurfacePolicyContextSyntax(
            name!,
            children!,
            SourceSpan.FromBounds(command.Span.Start, children![^1].Span.End))
        {
            SentenceIndex = line.SentenceIndex
        });
    }

    private static void ParseTaskBlock(
        SurfaceCommandSyntax command,
        LineInfo line,
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<SurfaceStatementSyntax> statements)
    {
        if (!TryReadChildBlock(
                lines,
                ref cursor,
                indent,
                diagnostics,
                command.Span,
                "TASK definition",
                out IReadOnlyList<SurfaceStatementSyntax>? children))
        {
            return;
        }

        if (!Compilation.Tasks.SurfaceTaskHeader.TryParse(
                command,
                diagnostics,
                out string? name,
                out IReadOnlyList<string>? parameters,
                out string? resultType))
        {
            return;
        }

        statements.Add(new SurfaceTaskDefinitionSyntax(
            name!,
            parameters!,
            resultType,
            children!,
            SourceSpan.FromBounds(command.Span.Start, children![^1].Span.End))
        {
            SentenceIndex = line.SentenceIndex
        });
    }

    private static void ParseRepeatBlock(
        SurfaceCommandSyntax command,
        LineInfo line,
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<SurfaceStatementSyntax> statements)
    {
        if (!TryRepeatCount(command, diagnostics, out int count) ||
            !TryReadChildBlock(
                lines,
                ref cursor,
                indent,
                diagnostics,
                command.Span,
                "REPEAT block",
                out IReadOnlyList<SurfaceStatementSyntax>? children))
        {
            return;
        }

        statements.Add(new SurfaceRepeatSyntax(
            count,
            children!,
            SourceSpan.FromBounds(command.Span.Start, children![^1].Span.End))
        {
            SentenceIndex = line.SentenceIndex
        });
    }

    private static void ParseWhileBlock(
        SurfaceCommandSyntax command,
        LineInfo line,
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<SurfaceStatementSyntax> statements)
    {
        if (!TryReadChildBlock(
                lines,
                ref cursor,
                indent,
                diagnostics,
                command.Span,
                "WHILE block",
                out IReadOnlyList<SurfaceStatementSyntax>? children) ||
            !SurfaceWhileDescriptor.TryCreate(
                command,
                children!,
                diagnostics,
                out SurfaceWhileDescriptor? descriptor))
        {
            return;
        }

        statements.Add(new SurfaceWhileSyntax(
            descriptor!,
            SourceSpan.FromBounds(command.Span.Start, children![^1].Span.End))
        {
            SentenceIndex = line.SentenceIndex
        });
    }

    private static void ParseConditionalBlock(
        SurfaceCommandSyntax command,
        LineInfo line,
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<SurfaceStatementSyntax> statements)
    {
        string condition = string.Join(" ", command.Values.Select(value => value.UnquotedText))
            .Trim()
            .TrimEnd(':')
            .Trim();
        if (condition.Length == 0)
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN363",
                "IF requires a condition and an indented block.",
                command.Span));
            return;
        }

        if (command.NormalizedName == "UNLESS")
            condition = $"NOT ({condition})";

        if (!TryReadChildBlock(
                lines,
                ref cursor,
                indent,
                diagnostics,
                command.Span,
                "IF block",
                out IReadOnlyList<SurfaceStatementSyntax>? whenTrue))
        {
            return;
        }

        IReadOnlyList<SurfaceStatementSyntax> whenFalse = [];
        if (cursor < lines.Count && lines[cursor].Indent == indent)
        {
            LineInfo elseLine = lines[cursor];
            SurfaceCommandSyntax? elseCommand = ParseLineStatement(elseLine, diagnostics) as SurfaceCommandSyntax;
            if (elseCommand?.NormalizedName == "ELSE")
            {
                cursor++;
                string elsePhrase = string.Join(
                    " ",
                    elseCommand.Values.Select(value => value.UnquotedText)).Trim();
                if (elsePhrase.StartsWith("IF ", StringComparison.OrdinalIgnoreCase))
                {
                    whenFalse = ParseElseIf(
                        elseCommand,
                        elseLine,
                        elsePhrase,
                        lines,
                        ref cursor,
                        indent,
                        diagnostics);
                }
                else if (TryReadChildBlock(
                             lines,
                             ref cursor,
                             indent,
                             diagnostics,
                             elseCommand.Span,
                             "ELSE block",
                             out IReadOnlyList<SurfaceStatementSyntax>? elseChildren))
                {
                    whenFalse = elseChildren!;
                }
            }
        }

        int end = whenFalse.Count > 0
            ? whenFalse[^1].Span.End
            : whenTrue![^1].Span.End;
        statements.Add(new SurfaceIfSyntax(
            condition,
            whenTrue!,
            whenFalse,
            SourceSpan.FromBounds(command.Span.Start, end))
        {
            SentenceIndex = line.SentenceIndex
        });
    }

    private static IReadOnlyList<SurfaceStatementSyntax> ParseElseIf(
        SurfaceCommandSyntax elseCommand,
        LineInfo elseLine,
        string elsePhrase,
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        string elseIfCondition = elsePhrase[3..].Trim().TrimEnd(':').Trim();
        if (elseIfCondition.Length == 0)
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN363",
                "ELSE IF requires a condition and an indented block.",
                elseCommand.Span));
            return [];
        }

        if (!TryReadChildBlock(
                lines,
                ref cursor,
                indent,
                diagnostics,
                elseCommand.Span,
                "ELSE IF block",
                out IReadOnlyList<SurfaceStatementSyntax>? elseIfChildren))
        {
            return [];
        }

        List<SurfaceStatementSyntax> nestedFalse = [];
        if (cursor < lines.Count && lines[cursor].Indent == indent)
        {
            LineInfo finalElseLine = lines[cursor];
            SurfaceCommandSyntax? finalElse = ParseLineStatement(finalElseLine, diagnostics) as SurfaceCommandSyntax;
            if (finalElse?.NormalizedName == "ELSE" && finalElse.Values.Count == 0)
            {
                cursor++;
                if (TryReadChildBlock(
                        lines,
                        ref cursor,
                        indent,
                        diagnostics,
                        finalElse.Span,
                        "ELSE block",
                        out IReadOnlyList<SurfaceStatementSyntax>? finalElseChildren))
                {
                    nestedFalse.AddRange(finalElseChildren!);
                }
            }
        }

        SourceSpan nestedSpan = SourceSpan.FromBounds(
            elseCommand.Span.Start,
            nestedFalse.Count > 0
                ? nestedFalse[^1].Span.End
                : elseIfChildren![^1].Span.End);
        return
        [
            new SurfaceIfSyntax(
                elseIfCondition,
                elseIfChildren!,
                nestedFalse,
                nestedSpan)
            {
                SentenceIndex = elseLine.SentenceIndex
            }
        ];
    }

    private static bool TrySingleName(
        SurfaceCommandSyntax command,
        string owner,
        ICollection<SurfaceDiagnostic> diagnostics,
        out string? name)
    {
        name = null;
        if (command.Values.Count != 1 || command.Alias is not null)
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN284",
                $"{owner} requires exactly one profile name.",
                command.Span));
            return false;
        }

        string value = command.Values[0].UnquotedText.Trim();
        if (!Identifier(value))
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN284",
                $"Invalid policy profile name '{value}'.",
                command.Values[0].Span));
            return false;
        }

        name = value;
        return true;
    }

    private static bool TryRepeatCount(
        SurfaceCommandSyntax command,
        ICollection<SurfaceDiagnostic> diagnostics,
        out int count)
    {
        count = 0;
        if (command.Values.Count != 1)
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN360",
                "REPEAT requires a non-negative count, for example `REPEAT 3 TIMES:`.",
                command.Span));
            return false;
        }

        string value = command.Values[0].UnquotedText.Trim().TrimEnd(':').Trim();
        if (value.EndsWith(" TIMES", StringComparison.OrdinalIgnoreCase))
            value = value[..^" TIMES".Length].Trim();
        if (!int.TryParse(value, out count) || count < 0 || count > 10_000)
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN360",
                "REPEAT requires a count between 0 and 10000.",
                command.Span));
            return false;
        }

        return true;
    }

    private static string DisplayName(SurfaceStatementSyntax statement) => statement switch
    {
        SurfaceCommandSyntax command => command.Name,
        SurfacePipelineSyntax => "pipeline",
        SurfaceContextSyntax => "context",
        SurfacePolicyDefinitionSyntax => "policy",
        SurfacePolicyContextSyntax => "policy context",
        SurfaceTaskDefinitionSyntax => "task",
        SurfaceRepeatSyntax => "repeat",
        SurfaceWhileSyntax => "while",
        SurfaceIfSyntax => "if",
        _ => statement.GetType().Name
    };

    private static bool Identifier(string value) =>
        value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
}
