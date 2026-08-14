using System.Text;
using System.Text.Json;

namespace FluNET.Language.Resources;

/// <summary>Raw bytes plus transport metadata. Providers acquire payloads; decoders interpret them.</summary>
public sealed record ResourcePayload(
    ReadOnlyMemory<byte> Content,
    string? MediaType = null,
    string? Charset = null,
    Uri? SourceUri = null)
{
    public static ResourcePayload FromText(string value, string mediaType = "text/plain", Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        return new ResourcePayload(encoding.GetBytes(value ?? string.Empty), mediaType, encoding.WebName);
    }
}

public interface IResourceDecoder
{
    string Id { get; }
    bool CanDecode(ResourceDescriptor descriptor, ResourcePayload payload);
    object Decode(ResourceDescriptor descriptor, ResourcePayload payload);
}

public interface IResourceEncoder
{
    string Id { get; }
    bool CanEncode(ResourceFormat format, object? value);
    ResourcePayload Encode(ResourceFormat format, object? value);
}

public interface IResourceDecoderRegistry
{
    IReadOnlyList<IResourceDecoder> Decoders { get; }
    IResourceDecoder Resolve(ResourceDescriptor descriptor, ResourcePayload payload);
    object Decode(ResourceDescriptor descriptor, ResourcePayload payload);
}

public interface IResourceEncoderRegistry
{
    IReadOnlyList<IResourceEncoder> Encoders { get; }
    IResourceEncoder Resolve(ResourceFormat format, object? value);
    ResourcePayload Encode(ResourceFormat format, object? value);
}

internal sealed record ResourceDecoderRegistration(Type DecoderType, Func<IServiceProvider, IResourceDecoder> Create);
internal sealed record ResourceEncoderRegistration(Type EncoderType, Func<IServiceProvider, IResourceEncoder> Create);

public sealed class ResourceDecoderRegistry : IResourceDecoderRegistry
{
    private readonly IResourceDecoder[] _decoders;

    internal ResourceDecoderRegistry(IServiceProvider services, IEnumerable<ResourceDecoderRegistration> registrations)
    {
        _decoders = new IResourceDecoder[] { new JsonResourceDecoder(), new TextResourceDecoder() }
            .Concat(registrations.Select(registration => registration.Create(services)))
            .ToArray();
        EnsureUniqueIds(_decoders.Select(item => item.Id), "decoder");
    }

    public IReadOnlyList<IResourceDecoder> Decoders => _decoders;

    public IResourceDecoder Resolve(ResourceDescriptor descriptor, ResourcePayload payload)
    {
        IResourceDecoder[] matches = _decoders.Where(decoder => decoder.CanDecode(descriptor, payload)).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"No resource decoder can decode '{descriptor.Reference.DisplayName}' as {descriptor.Format}."),
            _ => throw new InvalidOperationException($"Resource '{descriptor.Reference.DisplayName}' matches multiple decoders: {string.Join(", ", matches.Select(item => item.Id))}.")
        };
    }

    public object Decode(ResourceDescriptor descriptor, ResourcePayload payload) => Resolve(descriptor, payload).Decode(descriptor, payload);

    private static void EnsureUniqueIds(IEnumerable<string> ids, string kind)
    {
        string[] duplicates = ids.GroupBy(id => id, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicates.Length > 0) throw new LanguageDefinitionException($"Resource {kind} ids must be unique: {string.Join(", ", duplicates)}.");
    }
}

public sealed class ResourceEncoderRegistry : IResourceEncoderRegistry
{
    private readonly IResourceEncoder[] _encoders;

    internal ResourceEncoderRegistry(IServiceProvider services, IEnumerable<ResourceEncoderRegistration> registrations)
    {
        _encoders = new IResourceEncoder[] { new JsonResourceEncoder(), new TextResourceEncoder() }
            .Concat(registrations.Select(registration => registration.Create(services)))
            .ToArray();
        string[] duplicates = _encoders.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicates.Length > 0) throw new LanguageDefinitionException($"Resource encoder ids must be unique: {string.Join(", ", duplicates)}.");
    }

    public IReadOnlyList<IResourceEncoder> Encoders => _encoders;

    public IResourceEncoder Resolve(ResourceFormat format, object? value)
    {
        IResourceEncoder[] matches = _encoders.Where(encoder => encoder.CanEncode(format, value)).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"No resource encoder can encode value as {format}."),
            _ => throw new InvalidOperationException($"Value matches multiple {format} encoders: {string.Join(", ", matches.Select(item => item.Id))}.")
        };
    }

    public ResourcePayload Encode(ResourceFormat format, object? value) => Resolve(format, value).Encode(format, value);
}

public sealed class JsonResourceDecoder : IResourceDecoder
{
    public string Id => "core.decoder.json";
    public bool CanDecode(ResourceDescriptor descriptor, ResourcePayload payload) => descriptor.Format == ResourceFormat.Json;
    public object Decode(ResourceDescriptor descriptor, ResourcePayload payload)
    {
        using JsonDocument document = JsonDocument.Parse(payload.Content);
        return document.RootElement.Clone();
    }
}

public sealed class TextResourceDecoder : IResourceDecoder
{
    public string Id => "core.decoder.text";
    public bool CanDecode(ResourceDescriptor descriptor, ResourcePayload payload) => descriptor.Format == ResourceFormat.Text;
    public object Decode(ResourceDescriptor descriptor, ResourcePayload payload)
    {
        Encoding encoding = Encoding.UTF8;
        if (!string.IsNullOrWhiteSpace(payload.Charset))
        {
            try { encoding = Encoding.GetEncoding(payload.Charset); }
            catch (ArgumentException) { }
        }
        return encoding.GetString(payload.Content.Span);
    }
}

public sealed class JsonResourceEncoder : IResourceEncoder
{
    public string Id => "core.encoder.json";
    public bool CanEncode(ResourceFormat format, object? value) => format == ResourceFormat.Json;
    public ResourcePayload Encode(ResourceFormat format, object? value) =>
        new(JsonSerializer.SerializeToUtf8Bytes(value, value?.GetType() ?? typeof(object)), "application/json", "utf-8");
}

public sealed class TextResourceEncoder : IResourceEncoder
{
    public string Id => "core.encoder.text";
    public bool CanEncode(ResourceFormat format, object? value) => format == ResourceFormat.Text;
    public ResourcePayload Encode(ResourceFormat format, object? value) => ResourcePayload.FromText(value?.ToString() ?? string.Empty);
}
