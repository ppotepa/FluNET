using System.Text.Json;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using NUnit.Framework;

namespace FluNET.Tests.Compilation;

public sealed class HostDoctorTests
{
    [Test]
    public async Task DoctorReportsRuntimeAndProviderBackendsWithoutSecrets()
    {
        using FluNETContext context = FluNETContext.Create();
        ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt("DOCTOR [report]"));

        Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
        JsonElement report = (JsonElement)result.Result!;
        Assert.That(report.GetProperty("status").GetString(), Is.EqualTo("ok"));
        Assert.That(report.GetProperty("framework").GetString(), Does.Contain(".NET"));
        Assert.That(report.GetProperty("capabilities").GetProperty("total").GetInt32(), Is.GreaterThan(0));
        Assert.That(report.GetProperty("providers").GetProperty("blob").GetString(), Does.Contain("InMemory"));
        Assert.That(report.ToString(), Does.Not.Contain("secret"));
    }
}
