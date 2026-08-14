using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Syntax.Verbs;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record SetTextCommand(IExpression<string> Value) : ICommand<string>;
public sealed record SetJsonCommand(IExpression<JsonElement> Value) : ICommand<JsonElement>;
public sealed record SetNumberCommand(IExpression<decimal> Value) : ICommand<decimal>;
public sealed record SetBooleanCommand(IExpression<bool> Value) : ICommand<bool>;
public sealed record ParseJsonCommand(IExpression<JsonElement> Source) : ICommand<JsonElement>;
public sealed record FormatJsonCommand(IExpression<JsonElement> Source) : ICommand<string>;

public sealed class SetTextCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<SetTextCommand, string, SetText>(language, values)
{
    protected override SetTextCommand Bind(BoundCommand command) =>
        new(Context(command).RequireText(SemanticRole.Theme));
}

public sealed class SetJsonCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<SetJsonCommand, JsonElement, SetJson>(language, values)
{
    protected override SetJsonCommand Bind(BoundCommand command) =>
        new(Context(command).Require<JsonElement>(SemanticRole.Theme));
}

public sealed class SetNumberCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<SetNumberCommand, decimal, SetNumber>(language, values)
{
    protected override SetNumberCommand Bind(BoundCommand command) =>
        new(Context(command).Require<decimal>(SemanticRole.Theme));
}

public sealed class SetBooleanCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<SetBooleanCommand, bool, SetBoolean>(language, values)
{
    protected override SetBooleanCommand Bind(BoundCommand command) =>
        new(Context(command).Require<bool>(SemanticRole.Theme));
}

public sealed class ParseJsonCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<ParseJsonCommand, JsonElement, ParseJson>(language, values)
{
    protected override ParseJsonCommand Bind(BoundCommand command) =>
        new(Context(command).Require<JsonElement>(SemanticRole.Source));
}

public sealed class FormatJsonCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<FormatJsonCommand, string, FormatJson>(language, values)
{
    protected override FormatJsonCommand Bind(BoundCommand command) =>
        new(Context(command).Require<JsonElement>(SemanticRole.Source));
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
