using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Syntax.Verbs;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class ModuleRuntimeTests
{
    [Test]
    public void RuntimeRejectsAFrameWithoutAnExecutableRoute()
    {
        FluNetModuleBuilder runtime = new();
        runtime.Language.Command<SayText, string>("CHECK", "Text")
            .Positional<string>(SemanticRole.Theme);

        LanguageDefinitionException exception = Assert.Throws<LanguageDefinitionException>(
            () => runtime.Build())!;

        Assert.That(exception.Message, Does.Contain("exactly one typed route"));
    }

    [Test]
    public void RuntimeBuildsLanguageAndRouteAsOneDefinition()
    {
        FluNetModuleBuilder runtime = new();
        runtime.Language.Command<SayText, int>("COUNT", "Words")
            .Positional<string>(SemanticRole.Theme);
        runtime.Route<SayText, CountCommand, int, CountBinder, CountHandler>();

        FluNetRuntimeDefinition definition = runtime.Build();

        Assert.Multiple(() =>
        {
            Assert.That(definition.Language.FindCommand("COUNT"), Is.Not.Null);
            Assert.That(definition.Routes, Has.Count.EqualTo(1));
            Assert.That(definition.Routes[0].ResultType, Is.EqualTo(typeof(int)));
        });
    }

    private sealed record CountCommand(int Count) : ICommand<int>;

    private sealed class CountBinder : ICommandBinder<CountCommand, int>
    {
        public CountCommand? TryBind(BoundCommand command) => new(command.Syntax.Arguments.Count);
    }

    private sealed class CountHandler : ICommandHandler<CountCommand, int>
    {
        public ValueTask<int> HandleAsync(
            CountCommand command,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(command.Count);
    }
}
