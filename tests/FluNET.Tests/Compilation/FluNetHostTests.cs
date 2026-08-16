using FluNET.Context;
using FluNET.Capabilities;
using FluNET.Execution;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Tests.Compilation;

public sealed class FluNetHostTests
{
    [Test]
    public async Task BatteriesIncludedHostUsesRootedPortableProviders()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using FluNETContext context = FluNetHost.Create(new FluNetHostOptions { Root = root, DataDirectory = ".data" });
            ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt("CAPABILITIES [caps]"));
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(context.GetService<CapabilityRegistry>().TryResolve("storage.blob", out _), Is.True);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public void HostPolicyRestrictsFilesAndAllowsConfiguredNetworkOnly()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using FluNETContext context = FluNetHost.Create(new FluNetHostOptions
            {
                Root = root,
                NetworkHosts = ["api.example.test"]
            });
            IExecutionPolicy policy = context.GetService<IExecutionPolicy>();
            Assert.DoesNotThrow(() => policy.EnsureFileAccess(Path.Combine(root, "file.txt")));
            Assert.DoesNotThrow(() => policy.EnsureNetworkAccess(new Uri("https://api.example.test/data")));
            Assert.Throws<CapabilityDeniedException>(() => policy.EnsureNetworkAccess(new Uri("https://other.example.test/data")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
