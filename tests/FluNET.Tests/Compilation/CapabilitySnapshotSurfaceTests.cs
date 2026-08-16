using System.Text.Json;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using NUnit.Framework;

namespace FluNET.Tests.Compilation;

public sealed class CapabilitySnapshotSurfaceTests
{
    [Test]
    public async Task CapabilitiesCommandReturnsAvailabilityAndPermissions()
    {
        using FluNETContext context = FluNETContext.Create();
        ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt("CAPABILITIES [caps]"));

        Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
        JsonElement[] capabilities = (JsonElement[])result.Result!;
        JsonElement filesystem = capabilities.Single(item => item.GetProperty("id").GetString() == "filesystem.scan");
        Assert.That(filesystem.GetProperty("available").GetBoolean(), Is.True);
        Assert.That(filesystem.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()), Does.Contain("filesystem.read"));
        Assert.That(filesystem.GetProperty("platform").GetString(), Is.Not.Null.And.Not.Empty);
    }
}
