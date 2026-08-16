using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests;

[TestFixture]
public sealed class CapabilityTests
{
    [Test]
    public async Task SendUsesTheInjectedEmailBoundary()
    {
        RecordingEmailTransport email = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<IEmailTransport>(email));

        ExecutionResult execution = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("SEND hello TO user@example.test."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(execution.Result, Is.EqualTo("accepted:user@example.test"));
            Assert.That(email.Messages, Is.EqualTo(new[] { ("user@example.test", "hello") }));
        });
    }

    [Test]
    public async Task ChainedCommandCapabilityFailureKeepsItsErrorKind()
    {
        string allowedRoot = Path.Combine(Path.GetTempPath(), $"FluNET_Allowed_{Guid.NewGuid():N}");
        string outsidePath = Path.Combine(Path.GetTempPath(), $"FluNET_Outside_{Guid.NewGuid():N}.txt");
        Directory.CreateDirectory(allowedRoot);
        RecordingTextOutput output = new();

        try
        {
            using FluNETContext context = FluNETContext.Create(services =>
            {
                services.AddSingleton<IExecutionPolicy>(new RestrictedExecutionPolicy(
                    [allowedRoot],
                    []));
                services.AddSingleton<ITextOutput>(output);
            });

            ExecutionResult execution = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(
                $"SAY ready THEN SAVE \"blocked\" TO {{{outsidePath}}}."));

            Assert.Multiple(() =>
            {
                Assert.That(output.Lines, Is.EqualTo(new[] { "ready" }));
                Assert.That(execution.Error?.Kind, Is.EqualTo(ExecutionFailureKind.Capability));
                Assert.That(execution.Error?.Code, Is.EqualTo("FLN230"));
                Assert.That(File.Exists(outsidePath), Is.False);
            });
        }
        finally
        {
            Directory.Delete(allowedRoot, recursive: true);
            if (File.Exists(outsidePath))
            {
                File.Delete(outsidePath);
            }
        }
    }

    [Test]
    public void AnalyzeDoesNotPerformFileIo()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.txt");
        using FluNETContext context = FluNETContext.Create();

        PromptAnalysis analysis = context.GetEngine().Analyze(
            new ProcessedPrompt($"GET [text] FROM {{{missingPath}}}."));

        Assert.Multiple(() =>
        {
            Assert.That(analysis.IsValid, Is.True, analysis.ValidationResult.FailureReason);
            Assert.That(File.Exists(missingPath), Is.False);
        });
    }
}
