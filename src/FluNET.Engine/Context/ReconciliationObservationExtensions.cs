using FluNET.Capabilities;
using FluNET.Declarative.Reconciliation;
using FluNET.Language;
using FluNET.Language.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Context;

public static class ReconciliationObservationExtensions
{
    public static IResourceObserverRegistry GetResourceObserverRegistry(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IEnumerable<IResourceObserver> custom = context.ServiceProvider.GetServices<IResourceObserver>();
        IResourceObserver[] builtIns =
        [
            new FileResourceObserver(
                context.GetService<IFluNetFileSystem>(),
                context.GetService<IResourceDecoderRegistry>()),
            new HttpResourceObserver(
                context.GetService<IHttpTransport>(),
                context.GetService<IResourceDecoderRegistry>()),
            new SqlResourceObserver(context.GetService<ISqlQueryExecutor>()),
            new EnvironmentResourceObserver(
                context.ServiceProvider.GetService<IEnvironmentReader>() ?? new ProcessEnvironmentReader()),
            new SecretResourceObserver(
                context.GetService<ISecretStore>(),
                context.GetService<ISecretAccessPolicy>())
        ];
        return new ResourceObserverRegistry(
            context.GetService<LanguageSnapshot>(),
            custom.Concat(builtIns));
    }

    public static ValueTask<ObservedStateSnapshot> ObserveResourceAsync(
        this FluNETContext context,
        string source,
        string keyField,
        CancellationToken cancellationToken = default) =>
        context.GetResourceObserverRegistry().ObserveAsync(
            new ResourceObservationRequest(source, keyField),
            cancellationToken);
}
