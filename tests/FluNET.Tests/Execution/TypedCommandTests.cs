using FluNET.Context;
using FluNET.Execution.Commands;
using FluNET.Prompt;
using FluNET.Language.Binding;
using FluNET.Language;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class TypedCommandTests
{
    [TestCase("GET [value] FROM {input.txt}.")]
    [TestCase("LOAD TEXT [value] FROM {input.txt}.")]
    [TestCase("LOAD CONFIG [value] FROM {input.json}.")]
    [TestCase("SAVE value TO {output.txt}.")]
    [TestCase("DELETE {output.txt}.")]
    [TestCase("DOWNLOAD [file] FROM {https://example.test/file}.")]
    [TestCase("POST {\"ok\":true} TO {https://example.test/api}.")]
    [TestCase("SAY value.")]
    [TestCase("SEND value TO user@example.test.")]
    [TestCase("TRANSFORM value USING UTF8.")]
    public void EveryStandardFrameHasATypedRoute(string source)
    {
        using FluNETContext context = FluNETContext.Create();
        BoundCommand command = new SemanticCommandBinder(StandardLanguage.CreateSnapshot())
            .Bind(new ProcessedPrompt(source).Syntax.Commands.Single());

        bool hasRoute = context.GetService<CommandDispatcher>().CanDispatch(command);

        Assert.That(hasRoute, Is.True, $"Missing typed route for {command.Frame.ImplementationType.Name}.");
    }

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
        BoundCommand command = new SemanticCommandBinder(StandardLanguage.CreateSnapshot())
            .Bind(new ProcessedPrompt("SAY one two three.").Syntax.Commands.Single());

        CommandDispatchResult result = await dispatcher.TryExecuteAsync(command);

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
            string message = LiteralText(command.Message);
            Messages.Add(message);
            return ValueTask.FromResult($"typed:{message}");
        }

        private static string LiteralText(IExpression<string> expression) => expression switch
        {
            LiteralExpression<string> literal => literal.Value,
            JoinedTextExpression joined => string.Join(" ", joined.Parts.Select(LiteralText)),
            _ => throw new InvalidOperationException(
                $"Expected a literal-only SAY expression, got '{expression.GetType().Name}'.")
        };
    }

    private sealed record CountCommand(int Value) : ICommand<int>;

    private sealed class CountBinder : ICommandBinder<CountCommand, int>
    {
        public CountCommand? TryBind(BoundCommand command) =>
            command.Command.Name.Equals("SAY", StringComparison.OrdinalIgnoreCase)
                ? new CountCommand(command.Syntax.Arguments.Count)
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
