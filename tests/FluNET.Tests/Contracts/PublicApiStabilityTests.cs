using FluNET.Context;
using System.Reflection;

namespace FluNET.Tests.Contracts;

[TestFixture]
public sealed class PublicApiStabilityTests
{
    [Test]
    public void PreviewCompatibilityEntryPointsAreAbsentFromPublicSurface()
    {
        MethodInfo? execute = typeof(Engine).GetMethod(
            "Execute",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? run = typeof(Engine).GetMethod(
            "Run",
            BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo? defaultContext = typeof(FluNETContext).GetProperty(
            "Default",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo? resetDefault = typeof(FluNETContext).GetMethod(
            "ResetDefault",
            BindingFlags.Public | BindingFlags.Static);

        Assert.Multiple(() =>
        {
            Assert.That(execute, Is.Null);
            Assert.That(run, Is.Null);
            Assert.That(defaultContext, Is.Null);
            Assert.That(resetDefault, Is.Null);
        });
    }

    [Test]
    public void PreferredAsyncAndExplicitLifetimeApisDefineCandidateBoundary()
    {
        MethodInfo[] asyncMethods = typeof(Engine)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == nameof(Engine.ExecuteAsync))
            .ToArray();
        MethodInfo[] contextFactories = typeof(FluNETContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is nameof(FluNETContext.Create) or nameof(FluNETContext.CreateWithRuntime))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(asyncMethods, Is.Not.Empty);
            Assert.That(asyncMethods.All(method => method.GetCustomAttribute<ObsoleteAttribute>() is null), Is.True);
            Assert.That(contextFactories, Is.Not.Empty);
            Assert.That(contextFactories.All(method => method.GetCustomAttribute<ObsoleteAttribute>() is null), Is.True);
            Assert.That(typeof(IAsyncDisposable).IsAssignableFrom(typeof(FluNETContext)), Is.True);
        });
    }
}
