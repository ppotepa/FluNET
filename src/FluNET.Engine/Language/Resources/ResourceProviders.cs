using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Language.Resources;

public enum ResourceReadIntent { Get, Load }
public enum ResourceCapability { FileRead, NetworkRead, EnvironmentRead, SecretRead, DatabaseRead }
public sealed record ResourceProviderContext(ResourceDescriptor Descriptor, string OutputVariable, SurfaceCommandSyntax SurfaceCommand, SurfaceValueSyntax Source, PromptGrammar Grammar, ResourceReadIntent Intent);
public sealed record ResourceProviderResult(IReadOnlyList<CommandSyntax> Commands, string? ErrorCode = null, string? ErrorMessage = null)
{ public bool IsSuccess => ErrorCode is null; public static ResourceProviderResult Error(string code, string message) => new([], code, message); }
public interface IResourceProvider { string Id { get; } IReadOnlyList<ResourceCapability> RequiredCapabilities => []; bool CanHandle(ResourceDescriptor descriptor); ResourceProviderResult LowerRead(ResourceProviderContext context); }
public interface IResourceProviderRegistry { IReadOnlyList<IResourceProvider> Providers { get; } IResourceProvider? Resolve(ResourceDescriptor descriptor); }
internal sealed record ResourceProviderRegistration(Type ProviderType, Func<IServiceProvider, IResourceProvider> Create);

public sealed class ResourceProviderRegistry : IResourceProviderRegistry
{
    private readonly IResourceProvider[] _providers;
    internal ResourceProviderRegistry(IServiceProvider services, IEnumerable<ResourceProviderRegistration> registrations)
    {
        _providers = new IResourceProvider[] { new FileResourceProvider(), new HttpResourceProvider(), new EnvironmentResourceProvider(), new SecretResourceProvider() }.Concat(registrations.Select(registration => registration.Create(services))).ToArray();
        string[] duplicateIds = _providers.GroupBy(provider => provider.Id, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicateIds.Length > 0) throw new LanguageDefinitionException($"Resource provider ids must be unique: {string.Join(", ", duplicateIds)}.");
    }
    public IReadOnlyList<IResourceProvider> Providers => _providers;
    public IResourceProvider? Resolve(ResourceDescriptor descriptor)
    {
        IResourceProvider[] matches = _providers.Where(provider => provider.CanHandle(descriptor)).ToArray();
        return matches.Length switch { 0 => null, 1 => matches[0], _ => throw new LanguageDefinitionException($"Resource '{descriptor.Reference.DisplayName}' matches multiple providers: {string.Join(", ", matches.Select(item => item.Id))}.") };
    }
}

public sealed class FileResourceProvider : IResourceProvider
{
    public string Id => "core.file"; public IReadOnlyList<ResourceCapability> RequiredCapabilities => [ResourceCapability.FileRead];
    public bool CanHandle(ResourceDescriptor descriptor) => descriptor.Reference is FileResourceReference;
    public ResourceProviderResult LowerRead(ResourceProviderContext context)
    {
        FileResourceReference file = (FileResourceReference)context.Descriptor.Reference;
        if (file.IsPattern)
        {
            if (context.Descriptor.Format != ResourceFormat.Json) return ResourceProviderResult.Error("FLN225", $"Glob LOAD currently supports JSON patterns; '{context.Descriptor.Format}' needs a collection codec.");
            return new([Command("LOADGLOB", context, file.Path)]);
        }
        string verb = context.Descriptor.Format switch
        {
            ResourceFormat.Csv => "LOADCSV",
            ResourceFormat.Xml => "LOADXML",
            _ => string.Empty
        };
        if (verb.Length > 0) return new([Command(verb, context, file.Path)]);
        string qualifier = context.Descriptor.Format switch { ResourceFormat.Json => "CONFIG", ResourceFormat.Text => "TEXT", _ => string.Empty };
        if (qualifier.Length == 0) return ResourceProviderResult.Error("FLN224", $"No canonical file decoder is registered for format '{context.Descriptor.Format}'.");
        return new([new CommandSyntax([Token("LOAD", PromptTokenKind.Word, context.SurfaceCommand.Span.Start), Token(qualifier, PromptTokenKind.Word, context.Source.Span.Start), Token($"[{context.OutputVariable}]", PromptTokenKind.Variable, context.Source.Span.Start), Token("FROM", PromptTokenKind.Word, context.Source.Span.Start), Token($"{{{file.Path}}}", PromptTokenKind.Reference, context.Source.Span.Start)], context.Grammar)]);
    }
    private static CommandSyntax Command(string verb, ResourceProviderContext context, string path) => new([Token(verb, PromptTokenKind.Word, context.SurfaceCommand.Span.Start), Token($"[{context.OutputVariable}]", PromptTokenKind.Variable, context.Source.Span.Start), Token("FROM", PromptTokenKind.Word, context.Source.Span.Start), Token($"{{{path}}}", PromptTokenKind.Reference, context.Source.Span.Start)], context.Grammar);
    private static PromptToken Token(string text, PromptTokenKind kind, int start) => new(text, kind, Math.Max(0, start), 0);
}

public sealed class HttpResourceProvider : IResourceProvider
{
    public string Id => "core.http-json"; public IReadOnlyList<ResourceCapability> RequiredCapabilities => [ResourceCapability.NetworkRead];
    public bool CanHandle(ResourceDescriptor descriptor) => descriptor.Reference is HttpResourceReference;
    public ResourceProviderResult LowerRead(ResourceProviderContext context)
    {
        if (context.Intent != ResourceReadIntent.Get) return ResourceProviderResult.Error("FLN223", "HTTP resources belong to GET rather than LOAD.");
        if (context.Descriptor.Format != ResourceFormat.Json) return ResourceProviderResult.Error("FLN233", $"Compact HTTP GET currently has a Json contract; inferred format was '{context.Descriptor.Format}'.");
        Uri uri = ((HttpResourceReference)context.Descriptor.Reference).Uri;
        return new([new CommandSyntax([Token("GETHTTP", PromptTokenKind.Word, context.SurfaceCommand.Span.Start), Token($"[{context.OutputVariable}]", PromptTokenKind.Variable, context.Source.Span.Start), Token("FROM", PromptTokenKind.Word, context.Source.Span.Start), Token($"{{{uri}}}", PromptTokenKind.Reference, context.Source.Span.Start)], context.Grammar)]);
    }
    private static PromptToken Token(string text, PromptTokenKind kind, int start) => new(text, kind, Math.Max(0, start), 0);
}

public sealed class EnvironmentResourceProvider : IResourceProvider
{
    public string Id => "core.environment"; public IReadOnlyList<ResourceCapability> RequiredCapabilities => [ResourceCapability.EnvironmentRead];
    public bool CanHandle(ResourceDescriptor descriptor) => descriptor.Reference is EnvironmentResourceReference;
    public ResourceProviderResult LowerRead(ResourceProviderContext context)
    {
        if (context.Intent != ResourceReadIntent.Get) return ResourceProviderResult.Error("FLN223", "Environment resources belong to GET rather than LOAD.");
        string name = ((EnvironmentResourceReference)context.Descriptor.Reference).Name;
        return new([new CommandSyntax([Token("GETENV", PromptTokenKind.Word, context.SurfaceCommand.Span.Start), Token($"[{context.OutputVariable}]", PromptTokenKind.Variable, context.Source.Span.Start), Token("FROM", PromptTokenKind.Word, context.Source.Span.Start), Token($"{{{name}}}", PromptTokenKind.Reference, context.Source.Span.Start)], context.Grammar)]);
    }
    private static PromptToken Token(string text, PromptTokenKind kind, int start) => new(text, kind, Math.Max(0, start), 0);
}

public sealed class SecretResourceProvider : IResourceProvider
{
    public string Id => "core.secret"; public IReadOnlyList<ResourceCapability> RequiredCapabilities => [ResourceCapability.SecretRead];
    public bool CanHandle(ResourceDescriptor descriptor) => descriptor.Reference is SecretResourceReference;
    public ResourceProviderResult LowerRead(ResourceProviderContext context)
    {
        if (context.Intent != ResourceReadIntent.Get) return ResourceProviderResult.Error("FLN307", "Secrets belong to GET rather than LOAD.");
        string name = ((SecretResourceReference)context.Descriptor.Reference).Name;
        return new([new CommandSyntax([Token("GETSECRET", PromptTokenKind.Word, context.SurfaceCommand.Span.Start), Token($"[{context.OutputVariable}]", PromptTokenKind.Variable, context.Source.Span.Start), Token("FROM", PromptTokenKind.Word, context.Source.Span.Start), Token($"{{{name}}}", PromptTokenKind.Reference, context.Source.Span.Start)], context.Grammar)]);
    }
    private static PromptToken Token(string text, PromptTokenKind kind, int start) => new(text, kind, Math.Max(0, start), 0);
}
