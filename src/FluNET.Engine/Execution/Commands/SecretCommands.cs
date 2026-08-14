using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;

namespace FluNET.Execution.Commands;

public sealed record GetSecretCommand(string Name) : ICommand<SecretValue>;

public sealed class GetSecretCommandBinder : ICommandBinder<GetSecretCommand, SecretValue>
{
    public GetSecretCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.get.secret")) return null;
        BoundArgument source = command[SemanticRole.Source];
        if (source.Tokens.Count != 1) return null;
        string text = source.Tokens[0].Text;
        string name = text.Length >= 2 && text[0] == '{' && text[^1] == '}' ? text[1..^1] : text;
        return new GetSecretCommand(name);
    }
}

public sealed class GetSecretCommandHandler(ISecretStore secrets, ISecretAccessPolicy policy)
    : ICommandHandler<GetSecretCommand, SecretValue>
{
    public ValueTask<SecretValue> HandleAsync(GetSecretCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        policy.EnsureSecretAccess(command.Name);
        return secrets.TryGet(command.Name, out SecretValue? value) && value is not null
            ? ValueTask.FromResult(value)
            : throw new KeyNotFoundException($"Secret '{command.Name}' was not found.");
    }
}
