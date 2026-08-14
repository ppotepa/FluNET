namespace FluNET.Language.Resources;

public enum ResourceKind
{
    LocalFile,
    Http,
    Environment,
    Secret,
    Sql,
    Module
}

public enum ResourceFormat
{
    Unknown,
    Text,
    Json,
    Csv,
    Xml,
    Binary,
    Image
}

public abstract record ResourceReference(ResourceKind Kind, string DisplayName);

public sealed record FileResourceReference(string Path, bool IsRelative)
    : ResourceReference(ResourceKind.LocalFile, Path);

public sealed record HttpResourceReference(Uri Uri)
    : ResourceReference(ResourceKind.Http, Uri.ToString());

public sealed record EnvironmentResourceReference(string Name)
    : ResourceReference(ResourceKind.Environment, $"env:{Name}");

public sealed record SecretResourceReference(string Name)
    : ResourceReference(ResourceKind.Secret, $"secret:{Name}");

public sealed record SqlResourceReference(string Query)
    : ResourceReference(ResourceKind.Sql, "sql:<query>");

public sealed record ResourceDescriptor(
    ResourceReference Reference,
    ResourceFormat Format,
    TypeSymbol Type,
    string SuggestedVariableName);
