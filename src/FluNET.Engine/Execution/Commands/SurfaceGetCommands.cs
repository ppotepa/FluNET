using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record GetHttpJsonCommand(IExpression<Uri> Source) : ICommand<JsonElement>;
public sealed record GetEnvironmentCommand(IExpression<string> Name) : ICommand<string>;

public sealed class GetHttpJsonCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<GetHttpJsonCommand, JsonElement>
{
    public GetHttpJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.get.http.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new GetHttpJsonCommand(context.Require<Uri>(SemanticRole.Source));
    }
}

public sealed class GetEnvironmentCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<GetEnvironmentCommand, string>
{
    public GetEnvironmentCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.get.environment")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new GetEnvironmentCommand(context.RequireText(SemanticRole.Source));
    }
}

public sealed class GetHttpJsonCommandHandler(
    IVariableResolver variables,
    IHttpTransport http) : ICommandHandler<GetHttpJsonCommand, JsonElement>
{
    public async ValueTask<JsonElement> HandleAsync(
        GetHttpJsonCommand command,
        CancellationToken cancellationToken = default)
    {
        Uri uri = command.Source.Evaluate(variables);
        byte[] content = await http.GetBytesAsync(uri, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }
}

public sealed class GetEnvironmentCommandHandler(
    IVariableResolver variables) : ICommandHandler<GetEnvironmentCommand, string>
{
    private readonly IEnvironmentReader _environment = new ProcessEnvironmentReader();

    public ValueTask<string> HandleAsync(
        GetEnvironmentCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string name = command.Name.Evaluate(variables);
        string value = _environment.Get(name)
            ?? throw new KeyNotFoundException($"Environment variable '{name}' is not defined.");
        return ValueTask.FromResult(value);
    }
}
