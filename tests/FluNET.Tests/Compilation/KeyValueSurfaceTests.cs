using System.Text.Json;
using FluNET.Context;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class KeyValueSurfaceTests
{
    [Test]
    public async Task ListStoreProducesComposableKeyValueRows()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var execution = await context.ExecuteSurfaceAsync("""
            STORE user:one = "1"
            STORE user:two = "2"
            LIST STORE "user:" AS values
            WHERE key ENDS WITH 'two'
            """);

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
        JsonElement[] values = (JsonElement[])execution.Result!;
        Assert.That(values, Has.Length.EqualTo(1));
        Assert.That(values[0].GetProperty("value").GetString(), Is.EqualTo("2"));
    }

    [Test]
    public async Task DeleteStoreRemovesAKeyThroughTheStorageFrame()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var execution = await context.ExecuteSurfaceAsync("""
            STORE user:one = "1"
            DELETE STORE "user:one" AS removed
            """);

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
        Assert.That(execution.Result, Is.EqualTo("user:one"));
        Assert.That(execution.Compilation.Plan!.Steps.Last().Command.Frame.Id.Value,
            Is.EqualTo("storage.delete.value"));
    }
}
