using FluNET.Capabilities;
using FluNET.Execution.Actions;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Resources;
using FluNET.Language.Values;
using FluNET.Prompt;
using FluNET.Prompt.Expressions;
using FluNET.Prompt.Surface;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record WhileCommand(CompiledCondition Condition, int MaxIterations, CompiledActionTemplate Template) : ICommand<bool>;

public sealed class WhileCommandBinder(
    LanguageSnapshot language, IValueCodecRegistry values, ITextOutput output, IExecutionPolicy policy,
    IFluNetFileSystem files, IHttpTransport http, IResourceDecoderRegistry decoders, ISecretStore secrets,
    ISecretAccessPolicy secretPolicy, ISqlQueryExecutor sql, IFluNetDirectoryOperations directories,
    IFluNetFileOperations fileOperations, IFluNetFileTrash trash, IFluNetArchive archive,
    IFluNetMessageBus bus, IFluNetNotifier notifier) : ICommandBinder<WhileCommand, bool>
{
    public WhileCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.flow.while")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        string conditionText = string.Join(" ", command[new FrameRoleId("Condition")].Tokens.Select(Unwrap));
        SurfaceWhileDescriptor descriptor = SurfaceWhileDescriptor.Decode(
            string.Join("", command[new FrameRoleId("Template")].Tokens.Select(Unwrap)));
        CompiledCondition condition = new ConditionExpressionCompiler().Compile(
            ExpressionSyntaxParser.Parse(ConditionExpressionCompiler.NormalizeNaturalCondition(conditionText)));
        CompiledActionTemplate template = new SurfaceNestedActionCompiler(
            language, values, output, policy, files, http, decoders, secrets, secretPolicy, sql,
            directories, fileOperations, trash, archive, bus, notifier).Compile(descriptor.Actions);
        return new(condition, descriptor.MaxIterations, template);
    }

    private static string Unwrap(PromptToken token) =>
        token.Text.Length >= 2 && token.Text[0] == '{' && token.Text[^1] == '}'
            ? token.Text[1..^1]
            : token.Text;

}

public sealed class WhileCommandHandler(IVariableResolver variables) : ICommandHandler<WhileCommand, bool>
{
    public async ValueTask<bool> HandleAsync(WhileCommand command, CancellationToken cancellationToken = default)
    {
        for (int iteration = 0; iteration < command.MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!command.Condition.Expression.Evaluate(variables)) return true;
            try
            {
                await command.Template.ExecuteAsync(variables, cancellationToken).ConfigureAwait(false);
            }
            catch (LoopControlSignal signal) when (signal.Kind == LoopControlKind.Continue)
            {
                continue;
            }
            catch (LoopControlSignal signal) when (signal.Kind == LoopControlKind.Break)
            {
                return true;
            }
        }
        throw new InvalidOperationException($"WHILE reached its MAX limit of {command.MaxIterations} iterations.");
    }
}

public enum LoopControlKind
{
    Break,
    Continue
}

public sealed class LoopControlSignal(LoopControlKind kind) : Exception
{
    public LoopControlKind Kind { get; } = kind;
}
