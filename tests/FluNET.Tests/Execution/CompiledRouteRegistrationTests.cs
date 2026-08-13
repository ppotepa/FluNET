using FluNET.Context;
using FluNET.Execution.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class CompiledRouteRegistrationTests
{
    [Test]
    public void StandardRuntimeRegistersCompiledCommandRoutes()
    {
        using FluNETContext context = FluNETContext.Create();
        ICommandRoute[] routes = context.ServiceProvider
            .GetServices<ICommandRoute>()
            .ToArray();

        Assert.That(routes, Is.Not.Empty);
        Assert.That(routes.All(route =>
            route.GetType().IsGenericType &&
            route.GetType().GetGenericTypeDefinition() == typeof(CompiledCommandRoute<,>)),
            Is.True);
    }
}
