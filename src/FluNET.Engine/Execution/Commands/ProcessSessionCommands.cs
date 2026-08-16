using System.Text.Json;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record StartProcessSessionCommand(
    IExpression<string> FileName,
    IExpression<string> Arguments,
    IExpression<string>? WorkingDirectory,
    IExpression<string>? Environment) : ICommand<string>;

public sealed class StartProcessSessionCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<StartProcessSessionCommand, string>
{
    public StartProcessSessionCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("system.process.session.start")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(
            context.RequireText(SemanticRole.Source),
            context.Optional<string>(SemanticRole.Theme) ?? new LiteralExpression<string>(string.Empty),
            context.Optional<string>(new FrameRoleId("WorkingDirectory")),
            context.Optional<string>(new FrameRoleId("Environment")));
    }
}

public sealed class StartProcessSessionCommandHandler(
    IFluNetProcessSessionRegistry sessions,
    IVariableResolver variables) : ICommandHandler<StartProcessSessionCommand, string>
{
    public async ValueTask<string> HandleAsync(
        StartProcessSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        FluNetProcessSessionOutput result = await sessions.StartAsync(
            new FluNetProcessRequest(
                command.FileName.Evaluate(variables),
                ProcessArgumentParser.Parse(command.Arguments.Evaluate(variables)),
                WorkingDirectory: command.WorkingDirectory?.Evaluate(variables),
                Environment: command.Environment is null
                    ? null
                    : ProcessEnvironmentParser.Parse(command.Environment.Evaluate(variables))),
            cancellationToken).ConfigureAwait(false);
        return result.SessionId;
    }
}

public sealed record SendProcessSessionCommand(
    IExpression<string> SessionId,
    IExpression<string> Input) : ICommand<JsonElement>;

public sealed class SendProcessSessionCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<SendProcessSessionCommand, JsonElement>
{
    public SendProcessSessionCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("system.process.session.send")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(
            context.RequireText(SemanticRole.Source),
            context.RequireText(SemanticRole.Theme));
    }
}

public sealed class SendProcessSessionCommandHandler(
    IFluNetProcessSessionRegistry sessions,
    IVariableResolver variables) : ICommandHandler<SendProcessSessionCommand, JsonElement>
{
    public async ValueTask<JsonElement> HandleAsync(
        SendProcessSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        FluNetProcessSessionOutput result = await sessions.SendAsync(
            command.SessionId.Evaluate(variables),
            command.Input.Evaluate(variables),
            cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(result);
    }
}

public sealed record StopProcessSessionCommand(IExpression<string> SessionId) : ICommand<JsonElement>;

public sealed class StopProcessSessionCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<StopProcessSessionCommand, JsonElement>
{
    public StopProcessSessionCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("system.process.session.stop")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source));
    }
}

public sealed class StopProcessSessionCommandHandler(
    IFluNetProcessSessionRegistry sessions,
    IVariableResolver variables) : ICommandHandler<StopProcessSessionCommand, JsonElement>
{
    public async ValueTask<JsonElement> HandleAsync(
        StopProcessSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        FluNetProcessResult result = await sessions.StopAsync(
            command.SessionId.Evaluate(variables), cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(result);
    }
}
