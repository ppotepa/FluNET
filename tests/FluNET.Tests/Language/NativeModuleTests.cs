using FluNET.Context;
using FluNET.Execution;
using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class NativeModuleTests
{
    [Test]
    public async Task NativeModule_ParsesValidatesPlansAndExecutesWithoutLegacyVerb()
    {
        FluNetModuleBuilder builder = new();
        builder.AddModule(new CountWordsModule());
        FluNetRuntimeDefinition runtime = builder.Build();

        using FluNETContext context = FluNETContext.CreateWithRuntime(runtime);
        ExecutionResult result = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("COUNTWORDS one two three."));

        CommandFrameDescriptor frame = runtime.Language.FindFrame(
            new FrameId("tests.native.countwords"))!;
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.Result, Is.EqualTo(3));
            Assert.That(result.Plan, Is.Not.Null);
            Assert.That(result.Plan!.Steps, Has.Count.EqualTo(1));
            Assert.That(result.Plan.Steps[0].Command.Frame.Id, Is.EqualTo(frame.Id));
            Assert.That(frame.ImplementationType, Is.EqualTo(typeof(CountWordsCommand)));
        });
    }

    public sealed class CountWordsModule : IFluNetModule
    {
        public void Register(FluNetModuleBuilder module)
        {
            module.Language.Module("tests.native");
            module.Command<CountWordsCommand, int>("COUNTWORDS", "Words")
                .FrameId("tests.native.countwords")
                .Positional<string>(
                    SemanticRole.Theme,
                    SlotDirection.Input,
                    SlotCardinality.Repeated)
                .BindWith<CountWordsBinder>()
                .HandleWith<CountWordsHandler>();
        }
    }

    public sealed record CountWordsCommand(int Count) : ICommand<int>;

    public sealed class CountWordsBinder : ICommandBinder<CountWordsCommand, int>
    {
        public CountWordsCommand? TryBind(BoundCommand command) =>
            new(command[SemanticRole.Theme].Tokens.Count);
    }

    public sealed class CountWordsHandler : ICommandHandler<CountWordsCommand, int>
    {
        public ValueTask<int> HandleAsync(
            CountWordsCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(command.Count);
        }
    }
}
