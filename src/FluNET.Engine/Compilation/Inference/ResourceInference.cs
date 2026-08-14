using FluNET.Language.Resources;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Inference;

public sealed class ResourceClassifier
{
    public ResourceReference Classify(SurfaceValueSyntax value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string text = value.UnquotedText.Trim();
        if (text.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            string name = RequiredSuffix(text, "env:");
            return new EnvironmentResourceReference(name);
        }
        if (text.StartsWith("secret:", StringComparison.OrdinalIgnoreCase))
        {
            string name = RequiredSuffix(text, "secret:");
            return new SecretResourceReference(name);
        }
        if (text.StartsWith("sql:", StringComparison.OrdinalIgnoreCase))
        {
            string query = RequiredSuffix(text, "sql:").Trim('"', '\'');
            return new SqlResourceReference(query);
        }
        if (Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) &&
            uri.Scheme is "http" or "https")
        {
            return new HttpResourceReference(uri);
        }

        bool relative = !Path.IsPathRooted(text);
        return new FileResourceReference(text, relative);
    }

    private static string RequiredSuffix(string text, string prefix)
    {
        string value = text[prefix.Length..].Trim();
        return value.Length == 0
            ? throw new FormatException($"Resource '{prefix[..^1]}' requires a value.")
            : value;
    }
}

public sealed class FormatInference
{
    public ResourceFormat Infer(ResourceReference reference) => reference switch
    {
        FileResourceReference file => FromExtension(Path.GetExtension(file.Path)),
        HttpResourceReference http => FromExtension(Path.GetExtension(http.Uri.AbsolutePath)),
        EnvironmentResourceReference => ResourceFormat.Text,
        SecretResourceReference => ResourceFormat.Text,
        SqlResourceReference => ResourceFormat.Unknown,
        _ => ResourceFormat.Unknown
    };

    private static ResourceFormat FromExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".json" => ResourceFormat.Json,
            ".csv" => ResourceFormat.Csv,
            ".xml" => ResourceFormat.Xml,
            ".txt" or ".md" or ".log" => ResourceFormat.Text,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" => ResourceFormat.Image,
            ".bin" or ".dat" => ResourceFormat.Binary,
            _ => ResourceFormat.Unknown
        };
}

public sealed class VariableNameInference
{
    public string Infer(ResourceReference reference) => reference switch
    {
        FileResourceReference file => Normalize(Path.GetFileNameWithoutExtension(file.Path)),
        HttpResourceReference http => Normalize(HttpName(http.Uri)),
        EnvironmentResourceReference environment => Normalize(environment.Name),
        SecretResourceReference secret => Normalize(secret.Name),
        SqlResourceReference => "result",
        _ => "value"
    };

    private static string HttpName(Uri uri)
    {
        string segment = uri.Segments.LastOrDefault()?.Trim('/') ?? string.Empty;
        if (segment.Length == 0) return uri.Host.Split('.').FirstOrDefault() ?? "response";
        return Path.GetFileNameWithoutExtension(segment);
    }

    private static string Normalize(string value)
    {
        string normalized = new(value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || character == '_'
                ? char.ToLowerInvariant(character)
                : '_')
            .ToArray());
        normalized = normalized.Trim('_');
        if (normalized.Length == 0) return "value";
        return char.IsDigit(normalized[0]) ? $"value_{normalized}" : normalized;
    }
}
