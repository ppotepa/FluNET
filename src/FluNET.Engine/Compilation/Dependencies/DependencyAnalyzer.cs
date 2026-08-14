using FluNET.Compilation.Inference;
using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;

namespace FluNET.Compilation.Dependencies;

/// <summary>
/// Derives orchestration dependencies from typed semantic inputs, explicit
/// connectors, conditions, and conservative effect metadata.
/// </summary>
public sealed class DependencyAnalyzer(IExecutionMetadataProvider metadata)
{
    public DependencyAnalyzer() : this(new DefaultExecutionMetadataProvider()) { }

    public DependencyGraph Analyze(
        BoundProgram program,
        PromptSyntax syntax,
        InferenceTrace? trace = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(syntax);
        if (program.Commands.Count != syntax.Commands.Count)
        {
            throw new ArgumentException("Bound commands and canonical syntax must have matching counts.", nameof(syntax));
        }

        DependencyNode[] nodes = program.Commands
            .Select((command, index) => new DependencyNode(index, command, metadata.Get(command.Frame)))
            .ToArray();
        List<DependencyEdge> edges = [];
        Dictionary<string, int> producers = new(StringComparer.OrdinalIgnoreCase);
        HashSet<(int From, int To)> explicitParallel = syntax.Links
            .Where(link => link.Kind == CommandLinkKind.Parallel)
            .Select(link => (link.PredecessorIndex, link.SuccessorIndex))
            .ToHashSet();

        foreach (CommandLinkSyntax link in syntax.Links.Where(link => link.Kind == CommandLinkKind.Sequence))
        {
            Add(edges, new DependencyEdge(link.PredecessorIndex, link.SuccessorIndex, DependencyKind.Control));
        }

        int? lastOrderedEffect = null;
        for (int index = 0; index < program.Commands.Count; index++)
        {
            BoundCommand command = program.Commands[index];
            foreach (string variable in InputVariables(command))
            {
                if (producers.TryGetValue(variable, out int producer))
                {
                    Add(edges, new DependencyEdge(producer, index, DependencyKind.Data, variable));
                    trace?.Add(new InferenceDecision(InferenceKind.Dependency, variable,
                        $"{producer}->{index}", "producer-consumer", command.Syntax.Span));
                }
            }

            foreach (string variable in ConditionVariables(command))
            {
                if (producers.TryGetValue(variable, out int producer))
                {
                    Add(edges, new DependencyEdge(producer, index, DependencyKind.Condition, variable));
                }
            }

            DependencyNode node = nodes[index];
            if (node.Metadata.Concurrency != ConcurrencyPolicy.ParallelSafe)
            {
                if (lastOrderedEffect is int previous &&
                    !explicitParallel.Contains((previous, index)))
                {
                    Add(edges, new DependencyEdge(previous, index, DependencyKind.Effect));
                    trace?.Add(new InferenceDecision(InferenceKind.Scheduling,
                        command.Frame.Id.Value, $"{previous}->{index}",
                        "ordered-effect", command.Syntax.Span));
                }
                lastOrderedEffect = index;
            }

            foreach (string output in OutputVariables(command))
            {
                producers[output] = index;
            }
        }

        return new DependencyGraph(program, syntax, nodes, edges);
    }

    private static IEnumerable<string> InputVariables(BoundCommand command)
    {
        HashSet<string> variables = new(StringComparer.OrdinalIgnoreCase);
        foreach (BoundArgument argument in command.Arguments.Values.Where(argument =>
            argument.Slot.Direction == SlotDirection.Input && argument.IsPresent))
        {
            foreach (PromptToken token in argument.Tokens)
            {
                if (token.Kind == PromptTokenKind.Variable)
                {
                    variables.Add(NormalizeVariable(token.Text));
                    continue;
                }
                foreach (string interpolation in InterpolationVariables(token.Text))
                {
                    variables.Add(interpolation);
                }
            }
        }
        return variables;
    }

    private static IEnumerable<string> ConditionVariables(BoundCommand command)
    {
        HashSet<string> variables = new(StringComparer.OrdinalIgnoreCase);
        foreach (CommandModifierSyntax modifier in command.Syntax.Modifiers.Where(modifier =>
            modifier.Kind == CommandModifierKind.Condition && modifier.Values.Count > 0))
        {
            string source = string.Join(" ", modifier.Values.Select(token => token.Text));
            foreach (string variable in ConditionExpressionCache.GetOrCompile(source).VariableReferences)
            {
                variables.Add(variable);
            }
        }
        return variables;
    }

    private static IEnumerable<string> OutputVariables(BoundCommand command)
    {
        BoundArgument? output = command.Arguments.Values.FirstOrDefault(argument =>
            argument.Slot.Direction == SlotDirection.Output && argument.IsPresent);
        if (output is null) yield break;
        foreach (PromptToken token in output.Tokens.Where(token => token.Kind == PromptTokenKind.Variable))
        {
            string inner = NormalizeVariable(token.Text);
            if (inner.Length >= 2 && inner[0] == '{' && inner[^1] == '}')
            {
                foreach (string target in inner[1..^1]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    yield return target;
                }
            }
            else
            {
                yield return inner;
            }
        }
    }

    private static IEnumerable<string> InterpolationVariables(string text)
    {
        int cursor = 0;
        while (cursor < text.Length)
        {
            int open = text.IndexOf('{', cursor);
            if (open < 0) yield break;
            if (open + 1 < text.Length && text[open + 1] == '{')
            {
                cursor = open + 2;
                continue;
            }
            int close = text.IndexOf('}', open + 1);
            if (close < 0) yield break;
            string expression = text[(open + 1)..close].Trim();
            int end = expression.IndexOfAny(['.', '[']);
            string root = (end < 0 ? expression : expression[..end]).Trim();
            if (root.Length > 0 && (char.IsLetter(root[0]) || root[0] == '_') &&
                root.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_'))
            {
                yield return root;
            }
            cursor = close + 1;
        }
    }

    private static string NormalizeVariable(string token)
    {
        string value = token.Trim().TrimEnd('.');
        return value.Length >= 2 && value[0] == '[' && value[^1] == ']'
            ? value[1..^1]
            : value;
    }

    private static void Add(ICollection<DependencyEdge> edges, DependencyEdge edge)
    {
        if (edge.From == edge.To || edges.Contains(edge)) return;
        edges.Add(edge);
    }
}
