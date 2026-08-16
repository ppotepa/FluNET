using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record CreateTemporaryArtifactCommand(bool Directory, IExpression<string>? Suffix) : ICommand<string>;

public sealed class CreateTemporaryArtifactCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<CreateTemporaryArtifactCommand, string>
{
    public CreateTemporaryArtifactCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.system.temp.file") &&
            command.Frame.Id != new FrameId("surface.system.temp.directory"))
            return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        IExpression<string>? suffix = command.Find(SemanticRole.Source) is { IsPresent: true }
            ? context.Require<string>(SemanticRole.Source)
            : null;
        return new(
            command.Frame.Id == new FrameId("surface.system.temp.directory"),
            suffix);
    }
}

public sealed class CreateTemporaryArtifactCommandHandler(
    IFluNetTemporaryArtifacts artifacts,
    IVariableResolver variables) : ICommandHandler<CreateTemporaryArtifactCommand, string>
{
    public async ValueTask<string> HandleAsync(
        CreateTemporaryArtifactCommand command,
        CancellationToken cancellationToken = default)
    {
        FluNetTempArtifact artifact = command.Directory
            ? await artifacts.CreateDirectoryAsync(cancellationToken).ConfigureAwait(false)
            : await artifacts.CreateFileAsync(command.Suffix?.Evaluate(variables), cancellationToken).ConfigureAwait(false);
        return artifact.Path;
    }
}
