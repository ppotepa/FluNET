using FluNET.Context;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class ResourceProviderTests
{
    [Test]
    public void RuntimeRegistersBuiltInResourceProviders()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        IResourceProviderRegistry providers = context.GetService<IResourceProviderRegistry>();
        Assert.That(providers.Providers.Select(item => item.Id), Is.SupersetOf(new[] { "core.file", "core.http-json", "core.environment" }));
    }

    [Test]
    public void ModuleCanRegisterProviderWithoutChangingSurfaceLowerer()
    {
        FluNetModuleBuilder builder = new();
        builder.AddModule(new StandardLanguageModule()).AddModule(new SurfaceLanguageModule()).ResourceProvider<DemoProvider>();
        FluNetRuntimeDefinition runtime = builder.Build();
        using FluNETContext context = FluNETContext.CreateWithRuntime(runtime);
        IResourceProviderRegistry providers = context.GetService<IResourceProviderRegistry>();
        ResourceDescriptor descriptor = new(
            new ModuleResourceReference("demo", "value"), ResourceFormat.Unknown, runtime.Language.Types.Object, "value");
        Assert.That(providers.Resolve(descriptor), Is.TypeOf<DemoProvider>());
    }

    public sealed class DemoProvider : IResourceProvider
    {
        public string Id => "tests.demo";
        public bool CanHandle(ResourceDescriptor descriptor) => descriptor.Reference is ModuleResourceReference module && module.Scheme == "demo";
        public ResourceProviderResult LowerRead(ResourceProviderContext context) => ResourceProviderResult.Error("TEST", "not executed");
    }
}
