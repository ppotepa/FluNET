using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record CleanupTemporaryArtifactCommand(IExpression<string> Source) : ICommand<string>;

public sealed class CleanupTemporaryArtifactCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<CleanupTemporaryArtifactCommand, string>
{
    public CleanupTemporaryArtifactCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.system.temp.cleanup")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.Require<string>(SemanticRole.Source));
    }
}

public sealed class CleanupTemporaryArtifactCommandHandler(
    IFluNetTemporaryArtifacts artifacts,
    IVariableResolver variables) : ICommandHandler<CleanupTemporaryArtifactCommand, string>
{
    public async ValueTask<string> HandleAsync(
        CleanupTemporaryArtifactCommand command,
        CancellationToken cancellationToken = default)
    {
        string path = command.Source.Evaluate(variables);
        await artifacts.CleanupAsync(path, cancellationToken).ConfigureAwait(false);
        return path;
    }
}
