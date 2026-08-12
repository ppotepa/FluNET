using FluNET.Context;
using FluNET.Execution;
using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Syntax.Verbs;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class LanguageIdentityTests
{
    [Test]
    public void StandardLanguage_ExposesStableCommandFrameModuleAndVersionIds()
    {
        LanguageSnapshot snapshot = StandardLanguage.CreateSnapshot();
        CommandDescriptor get = snapshot.FindCommand("GET")!;
        CommandFrameDescriptor frame = get.Frames.Single();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Version, Is.EqualTo(new LanguageVersion("0.3")));
            Assert.That(get.Id, Is.EqualTo(new CommandId("flunet.core.get")));
            Assert.That(get.ModuleId, Is.EqualTo(new ModuleId("flunet.core")));
            Assert.That(frame.Id, Is.EqualTo(new FrameId("core.get.text")));
            Assert.That(frame.CommandId, Is.EqualTo(get.Id));
            Assert.That(snapshot.FindCommand(get.Id), Is.SameAs(get));
            Assert.That(snapshot.FindFrame(frame.Id), Is.SameAs(frame));
        });
    }

    [Test]
    public void Build_RejectsDuplicateFrameIds()
    {
        LanguageBuilder language = new();
        language.Command<SayText, string>("FIRST", "Text")
            .FrameId("tests.shared")
            .Positional<string>(SemanticRole.Theme);
        language.Command<GetText, string>("SECOND", "Text")
            .FrameId("tests.shared")
            .Positional<string>(SemanticRole.Theme);

        LanguageDefinitionException error = Assert.Throws<LanguageDefinitionException>(() => language.Build())!;

        Assert.That(error.Message, Does.Contain("Frame id 'tests.shared'"));
    }

    [Test]
    public async Task Runtime_DispatchesDirectRouteByFrameIdWithoutVerbRouteIdentity()
    {
        FluNetModuleBuilder runtime = new();
        runtime.Language
            .Module("tests.runtime")
            .Command<SayText, int>("COUNT", "Words")
            .FrameId("tests.count.words")
            .Positional<string>(SemanticRole.Theme);
        runtime.Route<CountCommand, int, CountBinder, CountHandler>("tests.count.words");

        FluNetRuntimeDefinition definition = runtime.Build();
        using FluNETContext context = FluNETContext.CreateWithRuntime(definition);
        ExecutionResult result = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("COUNT one two three."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.Result, Is.EqualTo(3));
            Assert.That(definition.Routes.Single().FrameId,
                Is.EqualTo(new FrameId("tests.count.words")));
            Assert.That(definition.Routes.Single().ImplementationType, Is.Null);
        });
    }

    public sealed record CountCommand(int Count) : ICommand<int>;

    public sealed class CountBinder : ICommandBinder<CountCommand, int>
    {
        public CountCommand? TryBind(BoundCommand command) =>
            new(command.Syntax.Arguments.Count);
    }

    public sealed class CountHandler : ICommandHandler<CountCommand, int>
    {
        public ValueTask<int> HandleAsync(
            CountCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(command.Count);
        }
    }
}
