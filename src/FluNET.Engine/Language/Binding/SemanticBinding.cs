using FluNET.Prompt;
using System.Collections.ObjectModel;

namespace FluNET.Language.Binding;

/// <summary>Tokens assigned to one semantic slot of a selected command frame.</summary>
public sealed class BoundArgument
{
    private readonly ReadOnlyCollection<PromptToken> _tokens;

    internal BoundArgument(CommandSlotDescriptor slot, IEnumerable<PromptToken> tokens)
    {
        Slot = slot ?? throw new ArgumentNullException(nameof(slot));
        _tokens = Array.AsReadOnly(tokens?.ToArray() ?? throw new ArgumentNullException(nameof(tokens)));
    }

    public CommandSlotDescriptor Slot { get; }
    public FrameRoleId RoleId => Slot.RoleId;
    public SemanticRole Role => Slot.Role;
    public IReadOnlyList<PromptToken> Tokens => _tokens;
    public bool IsPresent => _tokens.Count > 0;
    public SourceSpan Span => _tokens.Count == 0
        ? default
        : SourceSpan.FromBounds(_tokens[0].Start, _tokens[^1].Span.End);
}

/// <summary>A command whose lexical form has been assigned one frame and semantic roles.</summary>
public sealed class BoundCommand
{
    private readonly ReadOnlyDictionary<FrameRoleId, BoundArgument> _arguments;

    internal BoundCommand(
        CommandSyntax syntax,
        CommandDescriptor command,
        CommandFrameDescriptor frame,
        IDictionary<FrameRoleId, BoundArgument> arguments)
    {
        Syntax = syntax;
        Command = command;
        Frame = frame;
        _arguments = new ReadOnlyDictionary<FrameRoleId, BoundArgument>(
            new Dictionary<FrameRoleId, BoundArgument>(arguments));
    }

    public CommandSyntax Syntax { get; }
    public CommandDescriptor Command { get; }
    public CommandFrameDescriptor Frame { get; }
    public IReadOnlyDictionary<FrameRoleId, BoundArgument> Arguments => _arguments;

    public BoundArgument this[FrameRoleId role] => _arguments[role];
    public BoundArgument this[SemanticRole role] => _arguments[(FrameRoleId)role];

    public BoundArgument? Find(FrameRoleId role) =>
        _arguments.TryGetValue(role, out BoundArgument? argument) ? argument : null;

    public BoundArgument? Find(SemanticRole role) => Find((FrameRoleId)role);
}

/// <summary>
/// Converts parsed clauses into a lexical frame: command identity, one frame,
/// and arguments labelled by their semantic participation.
/// </summary>
public sealed class SemanticCommandBinder(LanguageSnapshot language)
{
    public IReadOnlyList<BoundCommand> BindProgram(PromptSyntax syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        return syntax.Commands.Select(Bind).ToArray();
    }

    public BoundCommand Bind(CommandSyntax syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        CommandDescriptor command = language.FindCommand(syntax.Verb.Text)
            ?? throw Error(syntax,
                $"Sentence must start with a known verb; unknown command '{syntax.Verb.Text}'.");
        FrameSelection selection = SelectFrame(command, syntax);
        IReadOnlyDictionary<string, IReadOnlyList<PromptToken>> clauses =
            SegmentForFrame(syntax, selection.Frame);
        Dictionary<FrameRoleId, BoundArgument> arguments = [];

        foreach (CommandSlotDescriptor slot in selection.Frame.Slots)
        {
            string marker = slot.Marker ?? SubjectMarker;
            IReadOnlyList<PromptToken> tokens = clauses.TryGetValue(marker, out IReadOnlyList<PromptToken>? values)
                ? values
                : Array.Empty<PromptToken>();
            if (slot.Marker is null && selection.ConsumesQualifier && tokens.Count > 0)
            {
                tokens = tokens.Skip(1).ToArray();
            }

            ValidateCardinality(syntax, slot, tokens);
            arguments.Add(slot.RoleId, new BoundArgument(slot, tokens));
        }

        return new BoundCommand(syntax, command, selection.Frame, arguments);
    }

    private const string SubjectMarker = "<SUBJECT>";

    private static IReadOnlyDictionary<string, IReadOnlyList<PromptToken>> SegmentForFrame(
        CommandSyntax syntax,
        CommandFrameDescriptor frame)
    {
        HashSet<string> acceptedMarkers = frame.Slots
            .Select(slot => slot.Marker)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<PromptToken>> segments = new(StringComparer.OrdinalIgnoreCase)
        {
            [SubjectMarker] = []
        };
        string currentMarker = SubjectMarker;

        foreach (PromptToken token in syntax.Arguments)
        {
            if (token.Kind == PromptTokenKind.Word && acceptedMarkers.Contains(token.Text))
            {
                currentMarker = token.Text.ToUpperInvariant();
                if (!segments.TryAdd(currentMarker, []))
                {
                    throw new SemanticBindingException(
                        $"Clause {currentMarker} is declared more than once.",
                        token.Span);
                }
                continue;
            }

            segments[currentMarker].Add(token);
        }

        return segments.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<PromptToken>)pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static FrameSelection SelectFrame(CommandDescriptor command, CommandSyntax syntax)
    {
        if (command.Frames.Count == 1)
        {
            return new FrameSelection(command.Frames[0], false);
        }

        ClauseSyntax subject = syntax.Clauses.First(clause => clause.Kind == PromptClauseKind.Subject);
        PromptToken? selector = subject.Values.FirstOrDefault();
        if (selector is not null)
        {
            CommandFrameDescriptor[] explicitMatches = command.Frames
                .Where(frame => frame.Qualifiers.Contains(selector.Text, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            if (explicitMatches.Length == 1)
            {
                // A qualifier preceding another subject token is grammatical
                // syntax. A lone qualifier is the historic LOAD config output
                // target and is retained without consuming it.
                bool consume = subject.Values.Count > 1;
                return new FrameSelection(explicitMatches[0], consume);
            }

            // Compatibility rule for the original PoC: LOAD [configname]
            // selected the Config realization from the output variable name.
            if (selector.Kind == PromptTokenKind.Variable)
            {
                CommandFrameDescriptor? legacyMatch = command.Frames.FirstOrDefault(frame =>
                    frame.Qualifiers.Any(qualifier =>
                        selector.Text.Contains(qualifier, StringComparison.OrdinalIgnoreCase)));
                if (legacyMatch is not null)
                {
                    return new FrameSelection(legacyMatch, false);
                }
            }
        }

        return new FrameSelection(
            command.Frames.Single(frame => frame.IsDefault),
            false);
    }

    private static void ValidateCardinality(
        CommandSyntax syntax,
        CommandSlotDescriptor slot,
        IReadOnlyList<PromptToken> tokens)
    {
        if (slot.Cardinality == SlotCardinality.Required && tokens.Count == 0)
        {
            string position = slot.Marker is null ? "subject" : $"{slot.Marker} clause";
            throw Error(syntax, $"{syntax.Verb.Text.ToUpperInvariant()} requires a value for its {position}.");
        }

        if (slot.Cardinality != SlotCardinality.Repeated &&
            slot.Marker is not null &&
            tokens.Count > 1)
        {
            throw new SemanticBindingException(
                $"{slot.Marker} accepts one value for semantic role {slot.Role}.",
                SourceSpan.FromBounds(tokens[0].Start, tokens[^1].Span.End));
        }
    }

    private static SemanticBindingException Error(CommandSyntax syntax, string message) =>
        new(message, syntax.Span);

    private readonly record struct FrameSelection(
        CommandFrameDescriptor Frame,
        bool ConsumesQualifier);
}

public sealed class SemanticBindingException(string message, SourceSpan span) : Exception(message)
{
    public SourceSpan Span { get; } = span;
}
