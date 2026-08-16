using FluNET.Capabilities;
using NUnit.Framework;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class CapabilityRegistryTests
{
    [Test]
    public void RegistryPrefersAnExactPlatformProvider()
    {
        CapabilityRegistry registry = new();
        registry.Register(new TestProvider(
            new CapabilityDescriptor("system.notify", platforms: [FluNetPlatform.Any])));
        registry.Register(new TestProvider(
            new CapabilityDescriptor("system.notify", platforms: [FluNetPlatform.Windows])));

        CapabilityResolution resolution = registry.Require("SYSTEM.NOTIFY", FluNetPlatform.Windows);

        Assert.That(resolution.Descriptor.Platforms, Does.Contain(FluNetPlatform.Windows));
    }

    [Test]
    public void RegistryDoesNotResolveUnavailableProviders()
    {
        CapabilityRegistry registry = new();
        registry.Register(new TestProvider(
            new CapabilityDescriptor("system.notify", platforms: [FluNetPlatform.Linux]),
            isAvailable: false));

        Assert.That(
            () => registry.Require("system.notify", FluNetPlatform.Linux),
            Throws.TypeOf<CapabilityUnavailableException>());
    }

    [Test]
    public void ContextExposesCapabilityDiscovery()
    {
        using FluNET.Context.FluNETContext context = FluNET.Context.FluNETContext.Create();

        Assert.That(context.GetService<CapabilityRegistry>(), Is.Not.Null);
    }

    [Test]
    public void RuntimeModulesCanContributeCapabilityProviders()
    {
        FluNET.Language.FluNetRuntimeDefinition runtime = new FluNET.Language.FluNetModuleBuilder()
            .AddModule(new TestCapabilityModule())
            .Build();

        using FluNET.Context.FluNETContext context = FluNET.Context.FluNETContext.CreateWithRuntime(runtime);

        CapabilityResolution resolution = context.GetService<CapabilityRegistry>().Require("test.module");

        Assert.That(resolution.Provider, Is.TypeOf<ModuleCapabilityProvider>());
        Assert.That(resolution.Descriptor.Permissions, Does.Contain("test.invoke"));
    }

    [Test]
    public void BuiltInEnvironmentIsDiscoverableWhileSecretsRemainDeniedByDefault()
    {
        using FluNET.Context.FluNETContext context = FluNET.Context.FluNETContext.Create();
        CapabilityRegistry registry = context.GetService<CapabilityRegistry>();

        Assert.That(registry.TryResolve("system.environment", out _), Is.True);
        Assert.That(registry.TryResolve("system.secrets", out _), Is.False);
    }

    private sealed class TestProvider(CapabilityDescriptor descriptor, bool isAvailable = true) : ICapabilityProvider
    {
        public CapabilityDescriptor Descriptor { get; } = descriptor;
        public bool IsAvailable { get; } = isAvailable;
    }

    private sealed class TestCapabilityModule : FluNET.Language.IFluNetModule
    {
        public void Register(FluNET.Language.FluNetModuleBuilder module) => module.Capability<ModuleCapabilityProvider>();
    }

    private sealed class ModuleCapabilityProvider : ICapabilityProvider
    {
        public CapabilityDescriptor Descriptor { get; } = new(
            "test.module",
            platforms: [FluNetPlatform.Any],
            permissions: ["test.invoke"]);

        public bool IsAvailable => true;
    }
}
