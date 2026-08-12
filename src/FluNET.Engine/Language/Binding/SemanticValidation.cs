using FluNET.Compilation;
using FluNET.Prompt;

namespace FluNET.Language.Binding;

/// <summary>
/// Validates a bound program exclusively from the immutable language snapshot,
/// selected command frames, and their semantic slots. It has no dependency on
/// WordFactory, IWord, legacy verb validation, or sentence creation.
/// </summary>
public sealed class SemanticProgramValidator(LanguageSnapshot language)
{
    public DiagnosticBag Validate(BoundProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        DiagnosticBag diagnostics = new();

        foreach (BoundCommand command in program.Commands)
        {
            ValidateRegistration(command, diagnostics);
            ValidateMarkers(command, diagnostics);
            ValidateQualifierUsage(command, diagnostics);
            ValidateSlots(command, diagnostics);
        }

        return diagnostics;
    }

    private void ValidateRegistration(BoundCommand command, DiagnosticBag diagnostics)
    {
        CommandDescriptor? registered = language.FindCommand(command.Syntax.Verb.Text);
        if (registered is null || !registered.Frames.Contains(command.Frame))
        {
            diagnostics.Add(
                CompilationDiagnosticCodes.FrameMismatch,
                CompilationPhase.Validate,
                $"Selected frame '{command.Frame.UsageName}' is not registered for command " +
                $"'{command.Syntax.Verb.Text.ToUpperInvariant()}'.",
                command.Syntax.Verb.Span);
        }
    }

    private static void ValidateMarkers(BoundCommand command, DiagnosticBag diagnostics)
    {
        HashSet<string> acceptedMarkers = command.Frame.Slots
            .Select(slot => slot.Marker)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ClauseSyntax[] markedClauses = command.Syntax.Clauses
            .Where(clause => clause.Keyword is not null)
            .ToArray();

        foreach (ClauseSyntax clause in markedClauses)
        {
            PromptToken keyword = clause.Keyword!;
            if (!acceptedMarkers.Contains(keyword.Text))
            {
                diagnostics.Add(
                    CompilationDiagnosticCodes.UnknownMarker,
                    CompilationPhase.Validate,
                    $"{command.Command.Name} frame '{command.Frame.UsageName}' does not accept marker " +
                    $"'{keyword.Text.ToUpperInvariant()}'.",
                    keyword.Span);
            }
        }

        foreach (IGrouping<string, ClauseSyntax> duplicate in markedClauses
            .GroupBy(clause => clause.Keyword!.Text, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            ClauseSyntax first = duplicate.First();
            ClauseSyntax last = duplicate.Last();
            diagnostics.Add(
                CompilationDiagnosticCodes.DuplicateMarker,
                CompilationPhase.Validate,
                $"Marker '{duplicate.Key.ToUpperInvariant()}' is declared more than once for " +
                $"{command.Command.Name}.",
                SourceSpan.FromBounds(first.Keyword!.Start, last.Span.End));
        }
    }

    private static void ValidateQualifierUsage(BoundCommand command, DiagnosticBag diagnostics)
    {
        ClauseSyntax subject = command.Syntax.Clauses.First(clause =>
            clause.Kind == PromptClauseKind.Subject);
        if (subject.Values.Count != 1)
        {
            return;
        }

        PromptToken token = subject.Values[0];
        bool isQualifier = token.Kind == PromptTokenKind.Word &&
            command.Frame.Qualifiers.Contains(token.Text, StringComparer.OrdinalIgnoreCase);
        if (!isQualifier)
        {
            return;
        }

        // LOAD historically accepts a lone TEXT/CONFIG-like token as the output
        // target while also using it to select the frame. Preserve only that
        // compatibility construction; other qualifiers introduce a realization
        // and therefore require a following subject value.
        if (command.Command.Name.Equals("LOAD", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        diagnostics.Add(
            CompilationDiagnosticCodes.SurplusArgument,
            CompilationPhase.Validate,
            $"Qualifier '{token.Text.ToUpperInvariant()}' for {command.Command.Name} must be followed by a subject value.",
            token.Span);
    }

    private static void ValidateSlots(BoundCommand command, DiagnosticBag diagnostics)
    {
        foreach (CommandSlotDescriptor slot in command.Frame.Slots)
        {
            if (!command.Arguments.TryGetValue(slot.RoleId, out BoundArgument? argument))
            {
                diagnostics.Add(
                    CompilationDiagnosticCodes.FrameMismatch,
                    CompilationPhase.Validate,
                    $"Frame '{command.Frame.UsageName}' did not bind semantic role '{slot.RoleId}'.",
                    command.Syntax.Span);
                continue;
            }

            if (slot.Cardinality != SlotCardinality.Optional && !argument.IsPresent)
            {
                string position = slot.Marker is null
                    ? "subject"
                    : $"{slot.Marker} clause";
                diagnostics.Add(
                    CompilationDiagnosticCodes.MissingRequiredRole,
                    CompilationPhase.Validate,
                    $"{command.Command.Name} requires a value for its {position} " +
                    $"({slot.RoleId}).",
                    MissingValueSpan(command.Syntax, slot));
                continue;
            }

            bool requiresSingleToken = slot.Cardinality != SlotCardinality.Repeated &&
                (slot.Marker is not null || slot.Direction == SlotDirection.Output);
            if (requiresSingleToken && argument.Tokens.Count > 1)
            {
                diagnostics.Add(
                    CompilationDiagnosticCodes.SurplusArgument,
                    CompilationPhase.Validate,
                    $"Semantic role '{slot.RoleId}' accepts one value, but " +
                    $"{argument.Tokens.Count} values were supplied.",
                    SourceSpan.FromBounds(argument.Tokens[1].Start, argument.Tokens[^1].Span.End));
            }
        }
    }

    private static SourceSpan MissingValueSpan(CommandSyntax syntax, CommandSlotDescriptor slot)
    {
        if (slot.Marker is null)
        {
            return syntax.Verb.Span;
        }

        ClauseSyntax? emptyClause = syntax.Clauses.FirstOrDefault(clause =>
            clause.Keyword?.Text.Equals(slot.Marker, StringComparison.OrdinalIgnoreCase) == true &&
            clause.Values.Count == 0);
        return emptyClause?.Keyword?.Span ?? syntax.Span;
    }
}
