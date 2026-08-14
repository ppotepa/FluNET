namespace FluNET.Language.Resources;

/// <summary>Immutable binary language value with media metadata.</summary>
public sealed record BinaryValue
{
    public BinaryValue(ReadOnlyMemory<byte> content, string? mediaType = null)
    {
        Content = content.ToArray();
        MediaType = string.IsNullOrWhiteSpace(mediaType) ? "application/octet-stream" : mediaType.Trim();
    }

    public ReadOnlyMemory<byte> Content { get; }
    public string MediaType { get; }
    public override string ToString() => $"<binary {Content.Length} bytes {MediaType}>";
}

/// <summary>Image bytes plus media/dimension metadata. FluNET does not impose a graphics library.</summary>
public sealed record ImageValue
{
    public ImageValue(ReadOnlyMemory<byte> content, string mediaType, int? width = null, int? height = null)
    {
        Content = content.ToArray();
        MediaType = string.IsNullOrWhiteSpace(mediaType) ? "application/octet-stream" : mediaType.Trim();
        Width = width; Height = height;
    }

    public ReadOnlyMemory<byte> Content { get; }
    public string MediaType { get; }
    public int? Width { get; }
    public int? Height { get; }
    public override string ToString() => Width is int w && Height is int h
        ? $"<image {w}x{h} {Content.Length} bytes {MediaType}>"
        : $"<image {Content.Length} bytes {MediaType}>";
}

public sealed class BinaryResourceDecoder : IResourceDecoder
{
    public string Id => "surface.decoder.binary";
    public bool CanDecode(ResourceDescriptor descriptor, ResourcePayload payload) => descriptor.Format == ResourceFormat.Binary;
    public object Decode(ResourceDescriptor descriptor, ResourcePayload payload) => new BinaryValue(payload.Content, payload.MediaType);
}

public sealed class ImageResourceDecoder : IResourceDecoder
{
    public string Id => "surface.decoder.image";
    public bool CanDecode(ResourceDescriptor descriptor, ResourcePayload payload) => descriptor.Format == ResourceFormat.Image;
    public object Decode(ResourceDescriptor descriptor, ResourcePayload payload)
    {
        string media = payload.MediaType ?? MediaType(descriptor.Reference.DisplayName);
        (int? width, int? height) = Dimensions(payload.Content.Span, media);
        return new ImageValue(payload.Content, media, width, height);
    }

    private static string MediaType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif",
        ".webp" => "image/webp", ".bmp" => "image/bmp", _ => "application/octet-stream"
    };

    private static (int?, int?) Dimensions(ReadOnlySpan<byte> bytes, string mediaType)
    {
        if (mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase) && bytes.Length >= 24 &&
            bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G')
        {
            int width = ReadBigEndian(bytes[16..20]); int height = ReadBigEndian(bytes[20..24]);
            return (width > 0 ? width : null, height > 0 ? height : null);
        }
        if (mediaType.Equals("image/gif", StringComparison.OrdinalIgnoreCase) && bytes.Length >= 10)
        {
            int width = bytes[6] | bytes[7] << 8; int height = bytes[8] | bytes[9] << 8;
            return (width > 0 ? width : null, height > 0 ? height : null);
        }
        return (null, null);
    }

    private static int ReadBigEndian(ReadOnlySpan<byte> bytes) =>
        bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3];
}

public sealed class BinaryResourceEncoder : IResourceEncoder
{
    public string Id => "surface.encoder.binary";
    public bool CanEncode(ResourceFormat format, object? value) => format == ResourceFormat.Binary && value is BinaryValue;
    public ResourcePayload Encode(ResourceFormat format, object? value)
    {
        BinaryValue binary = (BinaryValue)value!;
        return new(binary.Content, binary.MediaType);
    }
}

public sealed class ImageResourceEncoder : IResourceEncoder
{
    public string Id => "surface.encoder.image";
    public bool CanEncode(ResourceFormat format, object? value) => format == ResourceFormat.Image && value is ImageValue;
    public ResourcePayload Encode(ResourceFormat format, object? value)
    {
        ImageValue image = (ImageValue)value!;
        return new(image.Content, image.MediaType);
    }
}
