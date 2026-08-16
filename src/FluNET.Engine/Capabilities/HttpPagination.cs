using System.Text.Json;

namespace FluNET.Capabilities;

/// <summary>Provider-neutral JSON pagination contract. The transport remains responsible for policy and auth.</summary>
public interface IHttpJsonPaginator
{
    Task<IReadOnlyList<JsonElement>> FetchAsync(
        Uri source,
        string itemsPath,
        string nextPath,
        int maxPages,
        SecretValue? credential = null,
        CancellationToken cancellationToken = default);
}

public sealed class HttpJsonPaginator(
    IHttpTransport transport,
    IAuthenticatedHttpTransport authenticated) : IHttpJsonPaginator
{
    public async Task<IReadOnlyList<JsonElement>> FetchAsync(
        Uri source,
        string itemsPath,
        string nextPath,
        int maxPages,
        SecretValue? credential = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(nextPath);
        if (maxPages is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maxPages), "Maximum pages must be between 1 and 1000.");

        List<JsonElement> items = [];
        HashSet<Uri> visited = [];
        Uri current = source;
        for (int page = 0; page < maxPages; page++)
        {
            if (!visited.Add(current)) throw new InvalidDataException($"Pagination link loop detected at '{current}'.");
            HttpResourceResponse response = credential is null
                ? await transport.GetAsync(current, cancellationToken).ConfigureAwait(false)
                : await authenticated.GetAsync(current, credential, cancellationToken).ConfigureAwait(false);

            using JsonDocument document = JsonDocument.Parse(response.Content);
            JsonElement pageRoot = document.RootElement;
            JsonElement pageItems = ReadPath(pageRoot, itemsPath);
            if (pageItems.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Pagination items path '{itemsPath}' is not an array at '{current}'.");
            items.AddRange(pageItems.EnumerateArray().Select(item => item.Clone()));

            JsonElement next = ReadPath(pageRoot, nextPath);
            if (next.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return items;
            if (next.ValueKind == JsonValueKind.Object && next.TryGetProperty("href", out JsonElement href)) next = href;
            if (next.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(next.GetString()))
                throw new InvalidDataException($"Pagination next path '{nextPath}' must contain a URL string.");
            current = new Uri(current, next.GetString()!);
        }

        throw new InvalidOperationException($"Pagination reached the configured limit of {maxPages} pages before the next link ended.");
    }

    private static JsonElement ReadPath(JsonElement root, string path)
    {
        string normalized = path.Trim();
        if (normalized is "$" or "") return root;
        if (normalized.StartsWith("$.", StringComparison.Ordinal)) normalized = normalized[2..];
        JsonElement current = root;
        foreach (string segment in normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return default;
        }
        return current;
    }
}

public sealed class HttpPaginationCapabilityProvider(IHttpJsonPaginator paginator) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "network.http.pagination",
        "1.0",
        [FluNetPlatform.Any],
        ["network.connect"]);

    public bool IsAvailable => paginator is not null;
}
