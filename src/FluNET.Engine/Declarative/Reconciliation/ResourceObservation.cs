using FluNET.Capabilities;
using FluNET.Compilation.Inference;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt.Surface;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FluNET.Declarative.Reconciliation;

public sealed record ResourceObservationRequest(
    string Source,
    string KeyField,
    ResourceIdentity? Identity = null);

public interface IResourceObserver
{
    string Id { get; }
    bool CanObserve(ResourceDescriptor descriptor);
    ValueTask<ObservedStateSnapshot> ObserveAsync(
        ResourceDescriptor descriptor,
        ResourceObservationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IResourceObserverRegistry
{
    IReadOnlyList<IResourceObserver> Observers { get; }
    ValueTask<ObservedStateSnapshot> ObserveAsync(
        ResourceObservationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ResourceObservationException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Resolves a side-effecting observation boundary after resource inference. Custom observers
/// are ordered before built-ins by the context adapter, allowing a host to override an
/// observation strategy without changing the SYNC compiler.
/// </summary>
public sealed class ResourceObserverRegistry(
    LanguageSnapshot language,
    IEnumerable<IResourceObserver> observers) : IResourceObserverRegistry
{
    private readonly IResourceObserver[] _observers = (observers ?? throw new ArgumentNullException(nameof(observers))).ToArray();
    private readonly InferenceEngine _inference = new();

    public IReadOnlyList<IResourceObserver> Observers => _observers;

    public async ValueTask<ObservedStateSnapshot> ObserveAsync(
        ResourceObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.KeyField);

        ResourceDescriptor descriptor;
        try
        {
            descriptor = _inference.InferResource(
                new SurfaceValueSyntax(request.Source, default),
                language);
        }
        catch (FormatException exception)
        {
            throw new ResourceObservationException(
                $"Cannot classify resource '{request.Source}': {exception.Message}",
                exception);
        }

        IResourceObserver? observer = _observers.FirstOrDefault(item => item.CanObserve(descriptor));
        if (observer is null)
        {
            throw new ResourceObservationException(
                $"No resource observer handles '{request.Source}' ({descriptor.Reference.Kind}/{descriptor.Format}).");
        }
        return await observer.ObserveAsync(descriptor, request, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class FileResourceObserver(
    IFluNetFileSystem files,
    IResourceDecoderRegistry decoders) : IResourceObserver
{
    public string Id => "core.observer.file";

    public bool CanObserve(ResourceDescriptor descriptor) =>
        descriptor.Reference is FileResourceReference file &&
        !file.IsPattern &&
        descriptor.Format is ResourceFormat.Json or ResourceFormat.Csv or ResourceFormat.Xml or ResourceFormat.Text;

    public async ValueTask<ObservedStateSnapshot> ObserveAsync(
        ResourceDescriptor descriptor,
        ResourceObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        FileResourceReference file = (FileResourceReference)descriptor.Reference;
        string text = await files.ReadAllTextAsync(file.Path, cancellationToken).ConfigureAwait(false);
        ResourcePayload payload = ResourcePayload.FromText(text, MediaType(descriptor.Format));
        object value = decoders.Decode(descriptor, payload);
        return ResourceObservationSnapshot.Create(descriptor, request, value);
    }

    private static string MediaType(ResourceFormat format) => format switch
    {
        ResourceFormat.Json => "application/json",
        ResourceFormat.Csv => "text/csv",
        ResourceFormat.Xml => "application/xml",
        _ => "text/plain"
    };
}

public sealed class HttpResourceObserver(
    IHttpTransport http,
    IResourceDecoderRegistry decoders) : IResourceObserver
{
    public string Id => "core.observer.http";

    public bool CanObserve(ResourceDescriptor descriptor) =>
        descriptor.Reference is HttpResourceReference &&
        descriptor.Format is ResourceFormat.Json or ResourceFormat.Csv or ResourceFormat.Xml or ResourceFormat.Text;

    public async ValueTask<ObservedStateSnapshot> ObserveAsync(
        ResourceDescriptor descriptor,
        ResourceObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        Uri uri = ((HttpResourceReference)descriptor.Reference).Uri;
        HttpResourceResponse response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        object value = decoders.Decode(
            descriptor,
            new ResourcePayload(response.Content, response.MediaType, response.Charset, uri));
        return ResourceObservationSnapshot.Create(descriptor, request, value);
    }
}

public sealed class SqlResourceObserver(ISqlQueryExecutor sql) : IResourceObserver
{
    public string Id => "core.observer.sql";
    public bool CanObserve(ResourceDescriptor descriptor) => descriptor.Reference is SqlResourceReference;

    public async ValueTask<ObservedStateSnapshot> ObserveAsync(
        ResourceDescriptor descriptor,
        ResourceObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        string query = ((SqlResourceReference)descriptor.Reference).Query;
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
            await sql.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        JsonElement[] values = rows
            .Select(row => JsonSerializer.SerializeToElement(row))
            .ToArray();
        return ResourceObservationSnapshot.Create(descriptor, request, values);
    }
}

public sealed class EnvironmentResourceObserver(IEnvironmentReader environment) : IResourceObserver
{
    public string Id => "core.observer.environment";
    public bool CanObserve(ResourceDescriptor descriptor) => descriptor.Reference is EnvironmentResourceReference;

    public ValueTask<ObservedStateSnapshot> ObserveAsync(
        ResourceDescriptor descriptor,
        ResourceObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string name = ((EnvironmentResourceReference)descriptor.Reference).Name;
        string value = environment.Get(name)
            ?? throw new KeyNotFoundException($"Environment variable '{name}' is not defined.");
        JsonElement record = JsonSerializer.SerializeToElement(new { name, value });
        return ValueTask.FromResult(ResourceObservationSnapshot.Create(descriptor, request, new[] { record }));
    }
}

/// <summary>Observes secret identity/fingerprint only; plaintext never enters reconciliation state.</summary>
public sealed class SecretResourceObserver(
    ISecretStore secrets,
    ISecretAccessPolicy policy) : IResourceObserver
{
    public string Id => "core.observer.secret";
    public bool CanObserve(ResourceDescriptor descriptor) => descriptor.Reference is SecretResourceReference;

    public ValueTask<ObservedStateSnapshot> ObserveAsync(
        ResourceDescriptor descriptor,
        ResourceObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string name = ((SecretResourceReference)descriptor.Reference).Name;
        policy.EnsureSecretAccess(name);
        if (!secrets.TryGet(name, out SecretValue? secret) || secret is null)
            throw new KeyNotFoundException($"Secret '{name}' is not defined.");
        string fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(secret.Reveal())))
            .ToLowerInvariant();
        JsonElement record = JsonSerializer.SerializeToElement(new { name, fingerprint });
        return ValueTask.FromResult(ResourceObservationSnapshot.Create(descriptor, request, new[] { record }));
    }
}

internal static class ResourceObservationSnapshot
{
    public static ObservedStateSnapshot Create(
        ResourceDescriptor descriptor,
        ResourceObservationRequest request,
        object? value)
    {
        JsonElement[] records = ReconciliationValueNormalizer.ToRecords(value);
        ResourceIdentity identity = request.Identity ?? ResourceIdentity.Parse(request.Source);
        return new ObservedStateSnapshot(identity, request.KeyField, records);
    }
}

public static class ReconciliationValueNormalizer
{
    public static JsonElement[] ToRecords(object? value)
    {
        if (value is null) return [];
        if (value is JsonElement[] array) return array.Select(item => item.Clone()).ToArray();
        if (value is IEnumerable<JsonElement> elements) return elements.Select(item => item.Clone()).ToArray();
        if (value is JsonElement element) return FromElement(element);
        if (value is string text)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(text);
                return FromElement(document.RootElement);
            }
            catch (JsonException exception)
            {
                throw new ResourceObservationException(
                    "A reconciliation resource must contain a JSON object or array of objects.",
                    exception);
            }
        }

        JsonElement serialized = JsonSerializer.SerializeToElement(value, value.GetType());
        return FromElement(serialized);
    }

    private static JsonElement[] FromElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Array => element.EnumerateArray().Select(item => item.Clone()).ToArray(),
        JsonValueKind.Object => [element.Clone()],
        _ => throw new ResourceObservationException(
            $"Reconciliation requires object records; observed JSON root was {element.ValueKind}.")
    };
}
