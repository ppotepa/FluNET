using FluNET.Context;
using FluNET.Execution.Commands;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class TypedCommandTests
{
    [Test]
    public async Task Engine_UsesTheRegisteredTypedSayHandler()
    {
        CapturingSayHandler handler = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ICommandHandler<SayCommand, string>>(handler));

        var execution = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("SAY typed path."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(execution.Result, Is.EqualTo("typed:typed path"));
            Assert.That(handler.Messages, Is.EqualTo(new[] { "typed path" }));
        });
    }

    [Test]
    public async Task ChainedCommands_StayAlignedWithTheirTypedSyntax()
    {
        CapturingSayHandler handler = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ICommandHandler<SayCommand, string>>(handler));

        var execution = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("SAY first THEN SAY second."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(execution.Result, Is.EqualTo("typed:second"));
            Assert.That(handler.Messages, Is.EqualTo(new[] { "first", "second" }));
        });
    }

    [Test]
    public async Task CommandRoute_PreservesGenericCommandAndResultTypes()
    {
        CommandDispatcher dispatcher = new(new ICommandRoute[]
        {
            new CommandRoute<CountCommand, int>(new CountBinder(), new CountHandler())
        });
        CommandSyntax syntax = new ProcessedPrompt("COUNT one two three.")
            .Syntax.Commands.Single();

        CommandDispatchResult result = await dispatcher.TryExecuteAsync(syntax);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHandled, Is.True);
            Assert.That(result.Result, Is.EqualTo(3));
        });
    }

    private sealed class CapturingSayHandler : ICommandHandler<SayCommand, string>
    {
        public List<string> Messages { get; } = [];

        public ValueTask<string> HandleAsync(
            SayCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string message = string.Join(" ", command.Message.Parts
                .OfType<LiteralTextPart>()
                .Select(part => part.Value));
            Messages.Add(message);
            return ValueTask.FromResult($"typed:{message}");
        }
    }

    private sealed record CountCommand(int Value) : ICommand<int>;

    private sealed class CountBinder : ICommandBinder<CountCommand, int>
    {
        public CountCommand? TryBind(CommandSyntax syntax) =>
            syntax.Verb.Text.Equals("COUNT", StringComparison.OrdinalIgnoreCase)
                ? new CountCommand(syntax.Arguments.Count)
                : null;
    }

    private sealed class CountHandler : ICommandHandler<CountCommand, int>
    {
        public ValueTask<int> HandleAsync(
            CountCommand command,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(command.Value);
    }
}
