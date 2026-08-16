using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record TrimTextCommand(IExpression<string> Source) : ICommand<string>;
public sealed record UpperTextCommand(IExpression<string> Source) : ICommand<string>;
public sealed record LowerTextCommand(IExpression<string> Source) : ICommand<string>;
public sealed record ReplaceTextCommand(IExpression<string> Source, IExpression<string> Old, IExpression<string> New) : ICommand<string>;
public sealed record SplitTextCommand(IExpression<string> Source, IExpression<string> Separator) : ICommand<string[]>;
public sealed record JoinTextCommand(IExpression<string[]> Source, IExpression<string> Separator) : ICommand<string>;
public sealed record LinesTextCommand(IExpression<string> Source) : ICommand<string[]>;
public sealed record ExpectTextCommand(IExpression<string> Source, IExpression<string> Expected, IExpression<string> Operator) : ICommand<bool>;

public abstract class TextCommandBinder<TCommand, TResult>(LanguageSnapshot language, IValueCodecRegistry values)
    where TCommand : ICommand<TResult>
{
    protected CommandBindingContext Context(BoundCommand command) =>
        new(command, new ExpressionBinder(language, values));

    protected static bool Is(BoundCommand command, string frame) =>
        command.Frame.Id == new FrameId(frame);
}

public sealed class TrimTextCommandBinder(LanguageSnapshot l, IValueCodecRegistry v) : TextCommandBinder<TrimTextCommand, string>(l, v), ICommandBinder<TrimTextCommand, string>
{
    public TrimTextCommand? TryBind(BoundCommand c) => Is(c, "surface.text.trim") ? new(Context(c).RequireText(SemanticRole.Source)) : null;
}
public sealed class UpperTextCommandBinder(LanguageSnapshot l, IValueCodecRegistry v) : TextCommandBinder<UpperTextCommand, string>(l, v), ICommandBinder<UpperTextCommand, string>
{
    public UpperTextCommand? TryBind(BoundCommand c) => Is(c, "surface.text.upper") ? new(Context(c).RequireText(SemanticRole.Source)) : null;
}
public sealed class LowerTextCommandBinder(LanguageSnapshot l, IValueCodecRegistry v) : TextCommandBinder<LowerTextCommand, string>(l, v), ICommandBinder<LowerTextCommand, string>
{
    public LowerTextCommand? TryBind(BoundCommand c) => Is(c, "surface.text.lower") ? new(Context(c).RequireText(SemanticRole.Source)) : null;
}
public sealed class ReplaceTextCommandBinder(LanguageSnapshot l, IValueCodecRegistry v) : TextCommandBinder<ReplaceTextCommand, string>(l, v), ICommandBinder<ReplaceTextCommand, string>
{
    public ReplaceTextCommand? TryBind(BoundCommand c) => Is(c, "surface.text.replace") ? new(Context(c).RequireText(SemanticRole.Source), Context(c).RequireText(new FrameRoleId("Old")), Context(c).RequireText(new FrameRoleId("New"))) : null;
}
public sealed class SplitTextCommandBinder(LanguageSnapshot l, IValueCodecRegistry v) : TextCommandBinder<SplitTextCommand, string[]>(l, v), ICommandBinder<SplitTextCommand, string[]>
{
    public SplitTextCommand? TryBind(BoundCommand c) => Is(c, "surface.text.split") ? new(Context(c).RequireText(SemanticRole.Source), Context(c).RequireText(new FrameRoleId("Separator"))) : null;
}
public sealed class JoinTextCommandBinder(LanguageSnapshot l, IValueCodecRegistry v) : TextCommandBinder<JoinTextCommand, string>(l, v), ICommandBinder<JoinTextCommand, string>
{
    public JoinTextCommand? TryBind(BoundCommand c) => Is(c, "surface.text.join") ? new(Context(c).Require<string[]>(SemanticRole.Source), Context(c).RequireText(new FrameRoleId("Separator"))) : null;
}
public sealed class LinesTextCommandBinder(LanguageSnapshot l, IValueCodecRegistry v) : TextCommandBinder<LinesTextCommand, string[]>(l, v), ICommandBinder<LinesTextCommand, string[]>
{
    public LinesTextCommand? TryBind(BoundCommand c) => Is(c, "surface.text.lines") ? new(Context(c).RequireText(SemanticRole.Source)) : null;
}
public sealed class ExpectTextCommandBinder(LanguageSnapshot l, IValueCodecRegistry v) : TextCommandBinder<ExpectTextCommand, bool>(l, v), ICommandBinder<ExpectTextCommand, bool>
{
    public ExpectTextCommand? TryBind(BoundCommand c) => Is(c, "surface.text.expect")
        ? new(Context(c).RequireText(SemanticRole.Source), Context(c).RequireText(new FrameRoleId("Expected")), Context(c).RequireText(new FrameRoleId("Operator")))
        : null;
}

public sealed class TrimTextCommandHandler(IVariableResolver v) : ICommandHandler<TrimTextCommand, string>
{ public ValueTask<string> HandleAsync(TrimTextCommand c, CancellationToken x = default) => ValueTask.FromResult(c.Source.Evaluate(v).Trim()); }
public sealed class UpperTextCommandHandler(IVariableResolver v) : ICommandHandler<UpperTextCommand, string>
{ public ValueTask<string> HandleAsync(UpperTextCommand c, CancellationToken x = default) => ValueTask.FromResult(c.Source.Evaluate(v).ToUpperInvariant()); }
public sealed class LowerTextCommandHandler(IVariableResolver v) : ICommandHandler<LowerTextCommand, string>
{ public ValueTask<string> HandleAsync(LowerTextCommand c, CancellationToken x = default) => ValueTask.FromResult(c.Source.Evaluate(v).ToLowerInvariant()); }
public sealed class ReplaceTextCommandHandler(IVariableResolver v) : ICommandHandler<ReplaceTextCommand, string>
{ public ValueTask<string> HandleAsync(ReplaceTextCommand c, CancellationToken x = default) => ValueTask.FromResult(c.Source.Evaluate(v).Replace(c.Old.Evaluate(v), c.New.Evaluate(v), StringComparison.Ordinal)); }
public sealed class SplitTextCommandHandler(IVariableResolver v) : ICommandHandler<SplitTextCommand, string[]>
{ public ValueTask<string[]> HandleAsync(SplitTextCommand c, CancellationToken x = default) => ValueTask.FromResult(c.Source.Evaluate(v).Split(c.Separator.Evaluate(v), StringSplitOptions.None)); }
public sealed class JoinTextCommandHandler(IVariableResolver v) : ICommandHandler<JoinTextCommand, string>
{ public ValueTask<string> HandleAsync(JoinTextCommand c, CancellationToken x = default) => ValueTask.FromResult(string.Join(c.Separator.Evaluate(v), c.Source.Evaluate(v))); }
public sealed class LinesTextCommandHandler(IVariableResolver v) : ICommandHandler<LinesTextCommand, string[]>
{ public ValueTask<string[]> HandleAsync(LinesTextCommand c, CancellationToken x = default) => ValueTask.FromResult(c.Source.Evaluate(v).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')); }
public sealed class ExpectTextCommandHandler(IVariableResolver v) : ICommandHandler<ExpectTextCommand, bool>
{
    public ValueTask<bool> HandleAsync(ExpectTextCommand c, CancellationToken x = default)
    {
        x.ThrowIfCancellationRequested();
        string actual = c.Source.Evaluate(v);
        string expected = c.Expected.Evaluate(v);
        string operation = c.Operator.Evaluate(v).Trim().ToUpperInvariant();
        bool result = operation switch
        {
            "EQUAL" or "EQUALS" or "BE" => string.Equals(actual, expected, StringComparison.Ordinal),
            "CONTAIN" or "CONTAINS" => actual.Contains(expected, StringComparison.Ordinal),
            "STARTS" or "STARTSWITH" => actual.StartsWith(expected, StringComparison.Ordinal),
            "ENDS" or "ENDSWITH" => actual.EndsWith(expected, StringComparison.Ordinal),
            "MATCH" or "MATCHES" => System.Text.RegularExpressions.Regex.IsMatch(actual, expected),
            _ => throw new InvalidOperationException($"Unknown EXPECT operation '{operation}'.")
        };
        if (!result)
            throw new InvalidOperationException($"Expectation failed: '{actual}' does not {operation.ToLowerInvariant()} '{expected}'.");
        return ValueTask.FromResult(true);
    }
}
