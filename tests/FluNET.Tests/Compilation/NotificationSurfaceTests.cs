using FluNET.Capabilities;
using FluNET.Context;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class NotificationSurfaceTests
{
    [Test]
    public async Task NotifyUsesTheHostNotifierCapability()
    {
        CaptureOutput output = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services => services.AddSingleton<ITextOutput>(output));

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync("NOTIFY \"backup completed\"");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(execution.Result, Is.EqualTo("backup completed"));
        Assert.That(output.Lines, Is.EqualTo(new[] { "[NOTIFY] backup completed" }));
        Assert.That(context.GetService<CapabilityRegistry>().TryResolve("system.notify", out _), Is.True);
    }

    private sealed class CaptureOutput : ITextOutput
    {
        public List<string> Lines { get; } = [];

        public ValueTask WriteLineAsync(string message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Lines.Add(message);
            return ValueTask.CompletedTask;
        }
    }
}
