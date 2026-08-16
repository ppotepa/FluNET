using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record GetConfigurationCommand(IExpression<string> Key) : ICommand<string>;

public sealed class GetConfigurationCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<GetConfigurationCommand, string>
{
    public GetConfigurationCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.get.configuration")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source));
    }
}

public sealed class GetConfigurationCommandHandler(IFluNetConfiguration configuration, IVariableResolver variables) : ICommandHandler<GetConfigurationCommand, string>
{
    public ValueTask<string> HandleAsync(GetConfigurationCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = command.Key.Evaluate(variables);
        return configuration.TryGet(key, out string? value) && value is not null
            ? ValueTask.FromResult(value)
            : throw new KeyNotFoundException($"Configuration key '{key}' is not defined.");
    }
}
