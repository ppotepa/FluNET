using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Syntax.Verbs;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record SetTextCommand(TextExpression Value) : ICommand<string>;
public sealed record SetJsonCommand(JsonExpression Value) : ICommand<JsonElement>;
public sealed record SetNumberCommand(IExpression<decimal> Value) : ICommand<decimal>;
public sealed record SetBooleanCommand(IExpression<bool> Value) : ICommand<bool>;
public sealed record ParseJsonCommand(JsonExpression Source) : ICommand<JsonElement>;
public sealed record FormatJsonCommand(JsonExpression Source) : ICommand<string>;

public sealed class SetTextCommandBinder(LanguageSnapshot language) :
    FrameCommandBinder<SetTextCommand, string, SetText>
{
    protected override SetTextCommand Bind(BoundCommand command) =>
        new(TextExpression.Bind(command[SemanticRole.Theme], language));
}

public sealed class SetJsonCommandBinder :
    FrameCommandBinder<SetJsonCommand, JsonElement, SetJson>
{
    protected override SetJsonCommand Bind(BoundCommand command) =>
        new(new JsonExpression(command[SemanticRole.Theme]));
}

public sealed class SetNumberCommandBinder :
    FrameCommandBinder<SetNumberCommand, decimal, SetNumber>
{
    protected override SetNumberCommand Bind(BoundCommand command) =>
        new(new ScalarExpression<decimal>(
            command[SemanticRole.Theme],
            new DecimalValueConverter()));
}

public sealed class SetBooleanCommandBinder :
    FrameCommandBinder<SetBooleanCommand, bool, SetBoolean>
{
    protected override SetBooleanCommand Bind(BoundCommand command) =>
        new(new ScalarExpression<bool>(
            command[SemanticRole.Theme],
            new BooleanValueConverter()));
}

public sealed class ParseJsonCommandBinder :
    FrameCommandBinder<ParseJsonCommand, JsonElement, ParseJson>
{
    protected override ParseJsonCommand Bind(BoundCommand command) =>
        new(new JsonExpression(command[SemanticRole.Source]));
}

public sealed class FormatJsonCommandBinder :
    FrameCommandBinder<FormatJsonCommand, string, FormatJson>
{
    protected override FormatJsonCommand Bind(BoundCommand command) =>
        new(new JsonExpression(command[SemanticRole.Source]));
}

public sealed class SetTextCommandHandler(IVariableResolver variables)
    : ICommandHandler<SetTextCommand, string>
{
    public ValueTask<string> HandleAsync(
        SetTextCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(command.Value.Evaluate(variables));
    }
}

public sealed class SetJsonCommandHandler(IVariableResolver variables)
    : ICommandHandler<SetJsonCommand, JsonElement>
{
    public ValueTask<JsonElement> HandleAsync(
        SetJsonCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(command.Value.Evaluate(variables));
    }
}

public sealed class SetNumberCommandHandler(IVariableResolver variables)
    : ICommandHandler<SetNumberCommand, decimal>
{
    public ValueTask<decimal> HandleAsync(
        SetNumberCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(command.Value.Evaluate(variables));
    }
}

public sealed class SetBooleanCommandHandler(IVariableResolver variables)
    : ICommandHandler<SetBooleanCommand, bool>
{
    public ValueTask<bool> HandleAsync(
        SetBooleanCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(command.Value.Evaluate(variables));
    }
}

public sealed class ParseJsonCommandHandler(IVariableResolver variables)
    : ICommandHandler<ParseJsonCommand, JsonElement>
{
    public ValueTask<JsonElement> HandleAsync(
        ParseJsonCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(command.Source.Evaluate(variables));
    }
}

public sealed class FormatJsonCommandHandler(IVariableResolver variables)
    : ICommandHandler<FormatJsonCommand, string>
{
    public ValueTask<string> HandleAsync(
        FormatJsonCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string json = JsonSerializer.Serialize(
            command.Source.Evaluate(variables),
            new JsonSerializerOptions { WriteIndented = true });
        return ValueTask.FromResult(json);
    }
}
