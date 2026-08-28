using FluNET.Context;
using FluNET.Prompt;
using System.Reflection;

namespace FluNET.Tests.Contracts;

[TestFixture]
public sealed class PublicApiStabilityTests
{
    [Test]
    public void CompatibilityEntryPointsAreExplicitlyMarkedObsolete()
    {
        MethodInfo execute = typeof(Engine).GetMethod(nameof(Engine.Execute), [typeof(ProcessedPrompt)])!;
        MethodInfo run = typeof(Engine).GetMethod(nameof(Engine.Run), [typeof(ProcessedPrompt)])!;
        PropertyInfo defaultContext = typeof(FluNETContext).GetProperty(nameof(FluNETContext.Default))!;

        Assert.Multiple(() =>
        {
            Assert.That(execute.GetCustomAttribute<ObsoleteAttribute>(), Is.Not.Null);
            Assert.That(run.GetCustomAttribute<ObsoleteAttribute>(), Is.Not.Null);
            Assert.That(defaultContext.GetCustomAttribute<ObsoleteAttribute>(), Is.Not.Null);
        });
    }

    [Test]
    public void PreferredAsyncAndExplicitLifetimeApisRemainNonObsolete()
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
        });
    }
}
