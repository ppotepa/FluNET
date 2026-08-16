using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record NowCommand : ICommand<string>;

public sealed class NowCommandBinder : ICommandBinder<NowCommand, string>
{
    public NowCommand? TryBind(BoundCommand command) =>
        command.Frame.Id == new FrameId("surface.system.now") ? new NowCommand() : null;
}

public sealed class NowCommandHandler(IFluNetClock clock) : ICommandHandler<NowCommand, string>
{
    public ValueTask<string> HandleAsync(NowCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(clock.UtcNow.ToString("O"));
    }
}

public sealed record WaitCommand(IExpression<string> Duration) : ICommand<string>;

public sealed class WaitCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<WaitCommand, string>
{
    public WaitCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.system.wait")) return null;
        return new(new CommandBindingContext(command, new ExpressionBinder(language, values))
            .RequireText(SemanticRole.Source));
    }
}

public sealed class WaitCommandHandler(IFluNetDelay delay, IVariableResolver variables)
    : ICommandHandler<WaitCommand, string>
{
    public async ValueTask<string> HandleAsync(WaitCommand command, CancellationToken cancellationToken = default)
    {
        string text = command.Duration.Evaluate(variables);
        if (!TryParseDuration(text, out TimeSpan duration))
            throw new FormatException($"Invalid WAIT duration '{text}'. Use ms, s, m, h or d.");
        await delay.DelayAsync(duration, cancellationToken).ConfigureAwait(false);
        return duration.ToString();
    }

    private static bool TryParseDuration(string text, out TimeSpan duration)
    {
        string value = text.Trim().ToLowerInvariant();
        (string Number, double Seconds) parts = value switch
        {
            _ when value.EndsWith("ms", StringComparison.Ordinal) => (value[..^2], .001),
            _ when value.EndsWith('s') => (value[..^1], 1),
            _ when value.EndsWith('m') => (value[..^1], 60),
            _ when value.EndsWith('h') => (value[..^1], 3600),
            _ when value.EndsWith('d') => (value[..^1], 86400),
            _ => (string.Empty, 0)
        };
        if (parts.Number.Length == 0 ||
            !double.TryParse(parts.Number, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double number) ||
            !double.IsFinite(number) || number <= 0)
        {
            duration = default;
            return false;
        }
        double seconds = number * parts.Seconds;
        duration = TimeSpan.FromSeconds(seconds);
        return seconds > 0 && seconds <= TimeSpan.FromDays(1).TotalSeconds;
    }
}
