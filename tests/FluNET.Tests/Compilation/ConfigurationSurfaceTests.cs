using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class ConfigurationSurfaceTests
{
    [Test]
    public async Task CanonicalGetConfigUsesTheSameHostProvider()
    {
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<IFluNetConfiguration>(new DictionaryFluNetConfiguration(
                new Dictionary<string, string> { ["api.base"] = "https://api.example.test" })));

        ExecutionResult execution = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("GETCONFIG [api] FROM {api.base}"));

        Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
        Assert.That(execution.Result, Is.EqualTo("https://api.example.test"));
    }

    [Test]
    public async Task GetConfigReadsFromTheHostOwnedConfigurationProvider()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IFluNetConfiguration>(new DictionaryFluNetConfiguration(
                new Dictionary<string, string> { ["api.base"] = "https://api.example.test" })));

        var execution = await context.ExecuteSurfaceAsync("GET config:api.base AS api");

        Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
        Assert.That(execution.Result, Is.EqualTo("https://api.example.test"));
        Assert.That(execution.Compilation.Lowering.CanonicalSyntax.Commands.Single().AllTokens.First().Text, Is.EqualTo("GETCONFIG"));
    }

    [Test]
    public async Task MissingConfigKeyIsAnExecutionErrorWithoutLeakingAValue()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IFluNetConfiguration>(new DictionaryFluNetConfiguration(
                new Dictionary<string, string>())));

        var execution = await context.ExecuteSurfaceAsync("GET config:missing AS value");

        Assert.That(execution.IsSuccess, Is.False);
        Assert.That(execution.Error, Has.Message.EqualTo("Configuration key 'missing' is not defined."));
    }

    [Test]
    public async Task JsonFileConfigurationResolvesNestedKeysCaseInsensitively()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "appsettings.json");
        await File.WriteAllTextAsync(path, "{\"Api\":{\"Base\":\"https://api.example.test\"},\"retries\":3}");
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
                services.AddSingleton<IFluNetConfiguration>(provider =>
                    new JsonFileFluNetConfiguration(path, provider.GetRequiredService<IExecutionPolicy>())));

            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync("GET config:api.base AS api");

            Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
            Assert.That(execution.Result, Is.EqualTo("https://api.example.test"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
