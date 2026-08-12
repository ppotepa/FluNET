using FluNET.Compatibility;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;

namespace FluNET.Tests.Compatibility;

[TestFixture]
public sealed class LegacySentenceAdapterTests
{
    [Test]
    public void Adapter_ProjectsStandardCommandWithoutExecutingIt()
    {
        using FluNETContext context = FluNETContext.Create();
        LegacySentenceAdapter adapter = context.GetService<LegacySentenceAdapter>();

        LegacySentenceAdaptation result = adapter.Adapt(new ProcessedPrompt("SAY compatibility."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, result.ValidationResult.FailureReason);
            Assert.That(result.Sentence, Is.Not.Null);
            Assert.That(result.Sentence!.Root, Is.Not.Null);
        });
    }

    [Test]
    public void NativeCommand_CompilesWithoutLegacySentenceProjection()
    {
        FluNetModuleBuilder builder = new();
        builder.AddModule(new NativeOnlyModule());
        FluNetRuntimeDefinition runtime = builder.Build();
        using FluNETContext context = FluNETContext.CreateWithRuntime(runtime);

        CompilationResult compilation = context.GetEngine().Analyze(
            new ProcessedPrompt("COUNTNATIVE one two three."));
        LegacySentenceAdaptation compatibility = context
            .GetService<LegacySentenceAdapter>()
            .Adapt(new ProcessedPrompt("COUNTNATIVE one two three."));

        Assert.Multiple(() =>
        {
            Assert.That(compilation.IsCompilationSuccessful, Is.True);
            Assert.That(compilation.IsValid, Is.True);
            Assert.That(compilation.Plan, Is.Not.Null);
            Assert.That(compilation.Sentence, Is.Null);
            Assert.That(compatibility.IsValid, Is.False);
            Assert.That(compatibility.ValidationResult.FailureReason,
                Does.Contain("known verb").IgnoreCase);
        });
    }

    [Test]
    public void LegacyRegistry_ExcludesNativeCommandFrames()
    {
        FluNetModuleBuilder builder = new();
        builder.AddModule(new NativeOnlyModule());
        using FluNETContext context = FluNETContext.CreateWithRuntime(builder.Build());

        Syntax.Registry.LanguageRegistry registry = context.GetService<Syntax.Registry.LanguageRegistry>();

        Assert.Multiple(() =>
        {
            Assert.That(registry.VerbNames, Does.Not.Contain("COUNTNATIVE"));
            Assert.That(registry.GetVerbType("COUNTNATIVE"), Is.Null);
        });
    }

    public sealed class NativeOnlyModule : IFluNetModule
    {
        public void Register(FluNetModuleBuilder module)
        {
            module.Language.Module("tests.compatibility");
            module.Command<NativeCountCommand, int>("COUNTNATIVE", "Words")
                .FrameId("tests.compatibility.count")
                .Positional<string>(
                    SemanticRole.Theme,
                    SlotDirection.Input,
                    SlotCardinality.Repeated)
                .BindWith<NativeCountBinder>()
                .HandleWith<NativeCountHandler>();
        }
    }

    public sealed record NativeCountCommand(int Count) : ICommand<int>;

    public sealed class NativeCountBinder : ICommandBinder<NativeCountCommand, int>
    {
        public NativeCountCommand? TryBind(BoundCommand command) =>
            new(command[SemanticRole.Theme].Tokens.Count);
    }

    public sealed class NativeCountHandler : ICommandHandler<NativeCountCommand, int>
    {
        public ValueTask<int> HandleAsync(
            NativeCountCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(command.Count);
        }
    }
}
