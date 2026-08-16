using FluNET.Syntax.Core;

namespace FluNET.Language;

internal static class MetadataConventionInference
{
    public static IReadOnlyList<string> InferCapabilities(
        string keyword,
        IReadOnlyList<VerbPatternDescriptor> patterns,
        IEnumerable<string> explicitCapabilities,
        Type verbType)
    {
        var capabilities = new HashSet<string>(explicitCapabilities, StringComparer.OrdinalIgnoreCase);
        Type[] types = patterns.SelectMany(x => x.Pattern.Clauses).SelectMany(x => Flatten(x.ValueType)).Distinct().ToArray();
        string verb = keyword.ToUpperInvariant();

        bool hasFileSystemType = types.Any(x => x == typeof(FileInfo) || x == typeof(DirectoryInfo));
        bool hasUri = types.Any(x => x == typeof(Uri));

        if (hasFileSystemType && verb is "GET" or "LOAD") capabilities.Add("filesystem.read");
        if (hasFileSystemType && verb is "SAVE" or "DELETE" or "DOWNLOAD") capabilities.Add("filesystem.write");
        if (hasUri || verb is "POST" or "DOWNLOAD" or "SEND") capabilities.Add("network");
        if (verb == "SEND" && verbType.Name.Contains("Email", StringComparison.OrdinalIgnoreCase)) capabilities.Add("email.send");

        return capabilities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static ExecutionTraitsDescriptor InferTraits(string keyword, Type verbType)
    {
        string verb = keyword.ToUpperInvariant();
        return new(
            Pure: typeof(IPureOperation).IsAssignableFrom(verbType),
            Idempotent: typeof(IIdempotentOperation).IsAssignableFrom(verbType) || verb is "GET" or "LOAD",
            Retryable: typeof(IRetryableOperation).IsAssignableFrom(verbType) || verb is "GET" or "LOAD",
            Transactional: typeof(ITransactionalOperation).IsAssignableFrom(verbType),
            LongRunning: typeof(ILongRunningOperation).IsAssignableFrom(verbType),
            SideEffecting: typeof(ISideEffectingOperation).IsAssignableFrom(verbType) || verb is "SAVE" or "DELETE" or "POST" or "SEND" or "DOWNLOAD" or "SAY");
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.IsArray && type.GetElementType() is Type element) yield return element;
        foreach (Type contract in type.GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            yield return contract.GetGenericArguments()[0];
    }
}
