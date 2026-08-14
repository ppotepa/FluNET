using FluNET.Prompt.Expressions;

namespace FluNET.Execution.Commands;

/// <summary>
/// Process-local cache of side-effect-free compiled condition trees. The source
/// text is the stable cache key; evaluation always uses the current variable resolver.
/// </summary>
public static class ConditionExpressionCache
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, CompiledCondition> Cache =
        new(StringComparer.Ordinal);

    public static CompiledCondition GetOrCompile(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new FormatException("A condition expression cannot be empty.");
        }

        string key = source.Trim();
        lock (Gate)
        {
            if (Cache.TryGetValue(key, out CompiledCondition? compiled))
            {
                return compiled;
            }

            ExpressionSyntax syntax = ExpressionSyntaxParser.Parse(key);
            compiled = new ConditionExpressionCompiler().Compile(syntax);
            Cache.Add(key, compiled);
            return compiled;
        }
    }
}
