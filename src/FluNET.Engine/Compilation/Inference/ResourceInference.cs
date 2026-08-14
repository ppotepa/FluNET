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
            return new EnvironmentResourceReference(RequiredSuffix(text, "env:"));
        if (text.StartsWith("secret:", StringComparison.OrdinalIgnoreCase))
            return new SecretResourceReference(RequiredSuffix(text, "secret:"));
        if (text.StartsWith("sql:", StringComparison.OrdinalIgnoreCase))
            return new SqlResourceReference(RequiredSuffix(text, "sql:").Trim('"', '\''));
        if (Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
            return new HttpResourceReference(uri);
        return new FileResourceReference(text, !Path.IsPathRooted(text));
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
        HttpResourceReference http => HttpFormat(http.Uri),
        EnvironmentResourceReference => ResourceFormat.Text,
        SecretResourceReference => ResourceFormat.Text,
        SqlResourceReference => ResourceFormat.Unknown,
        _ => ResourceFormat.Unknown
    };

    private static ResourceFormat HttpFormat(Uri uri)
    {
        ResourceFormat extension = FromExtension(Path.GetExtension(uri.AbsolutePath));
        return extension == ResourceFormat.Unknown ? ResourceFormat.Json : extension;
    }

    private static ResourceFormat FromExtension(string extension) => extension.ToLowerInvariant() switch
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
        FileResourceReference file when file.IsPattern => PatternName(file.Path),
        FileResourceReference file => Normalize(Path.GetFileNameWithoutExtension(file.Path)),
        HttpResourceReference http => Normalize(HttpName(http.Uri)),
        EnvironmentResourceReference environment => Normalize(environment.Name),
        SecretResourceReference secret => Normalize(secret.Name),
        SqlResourceReference => "result",
        _ => "value"
    };

    private static string PatternName(string pattern)
    {
        string? directory = Path.GetDirectoryName(pattern)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Normalize(string.IsNullOrWhiteSpace(directory) ? "items" : Path.GetFileName(directory));
    }

    private static string HttpName(Uri uri)
    {
        string[] segments = uri.Segments.Select(segment => segment.Trim('/')).Where(segment => segment.Length > 0).ToArray();
        if (segments.Length == 0) return uri.Host.Split('.').FirstOrDefault() ?? "response";
        string last = Path.GetFileNameWithoutExtension(segments[^1]);
        if (last.All(char.IsDigit) && segments.Length > 1)
        {
            last = Path.GetFileNameWithoutExtension(segments[^2]);
        }
        return last;
    }

    private static string Normalize(string value)
    {
        string normalized = new(value.Trim().Select(character =>
            char.IsLetterOrDigit(character) || character == '_'
                ? char.ToLowerInvariant(character)
                : '_').ToArray()).Trim('_');
        if (normalized.Length == 0) return "value";
        return char.IsDigit(normalized[0]) ? $"value_{normalized}" : normalized;
    }
}
