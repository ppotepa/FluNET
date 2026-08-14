using FluNET.Capabilities;
using FluNET.Context;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SecretCapabilityTests
{
    [Test]
    public void SecretIsASeparateLanguageTypeAndCannotFlowToSay()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var get = context.CompileSurface("GET secret:github-token AS token");
        var leak = context.CompileSurface("GET secret:github-token AS token\nSAY [token]");
        Assert.Multiple(() =>
        {
            Assert.That(get.IsValid, Is.True);
            Assert.That(get.BoundProgram!.Commands[0].Frame.ResultTypeSymbol.Name, Is.EqualTo("Secret"));
            Assert.That(leak.IsValid, Is.False);
            Assert.That(leak.Diagnostics.Select(item => item.Code), Does.Contain("FLN151"));
        });
    }

    [Test]
    public async Task SecretAccessIsDeniedByDefaultAndCanBeAllowListed()
    {
        using FluNETContext denied = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceExecutionResult deniedResult = await denied.ExecuteSurfaceAsync("GET secret:key AS token");
        Assert.That(deniedResult.Error, Is.TypeOf<CapabilityDeniedException>());

        using FluNETContext allowed = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
        {
            services.AddSingleton<ISecretStore>(new DictionarySecretStore(new Dictionary<string,string>{{"key","value"}}));
            services.AddSingleton<ISecretAccessPolicy>(new AllowListedSecretAccessPolicy(["key"]));
        });
        SurfaceExecutionResult allowedResult = await allowed.ExecuteSurfaceAsync("GET secret:key AS token");
        Assert.That(allowedResult.IsSuccess, Is.True, allowedResult.Error?.Message);
        Assert.That(allowedResult.Result, Is.TypeOf<SecretValue>());
        Assert.That(allowedResult.Result!.ToString(), Is.EqualTo("<secret>"));
    }
}
