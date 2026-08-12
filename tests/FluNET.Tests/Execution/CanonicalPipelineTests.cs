using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class CanonicalPipelineTests
{
    [Test]
    public async Task ExecuteAsync_DoesNotConstructLegacySentence()
    {
        CapturingOutput output = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ITextOutput>(output));

        ExecutionResult result = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("SAY canonical."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.Sentence, Is.Null);
            Assert.That(result.Plan, Is.Not.Null);
            Assert.That(result.Result, Is.EqualTo("canonical"));
            Assert.That(output.Messages, Is.EqualTo(new[] { "canonical" }));
        });
    }

    [Test]
    public async Task ExecuteAsync_RejectsSemanticErrorBeforeExternalEffect()
    {
        CapturingOutput output = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ITextOutput>(output));

        ExecutionResult result = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("SAY hello FROM {input.txt}."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error?.Kind, Is.EqualTo(ExecutionFailureKind.Validation));
            Assert.That(result.Error?.Code, Is.EqualTo(CompilationDiagnosticCodes.UnknownMarker));
            Assert.That(output.Messages, Is.Empty);
        });
    }

    [Test]
    public void Run_ProjectsCompatibilitySentenceWithoutRepeatingEffect()
    {
        CapturingOutput output = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ITextOutput>(output));

        var result = context.GetEngine().Run(new ProcessedPrompt("SAY compatible."));

        Assert.Multiple(() =>
        {
            Assert.That(result.ValidationResult.IsValid, Is.True);
            Assert.That(result.Sentence, Is.Not.Null);
            Assert.That(result.Result, Is.EqualTo("compatible"));
            Assert.That(output.Messages, Is.EqualTo(new[] { "compatible" }));
        });
    }

    private sealed class CapturingOutput : ITextOutput
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public ValueTask WriteLineAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _messages.Add(message);
            return ValueTask.CompletedTask;
        }
    }
}
