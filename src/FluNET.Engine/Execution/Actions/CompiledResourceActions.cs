using FluNET.Capabilities;
using FluNET.Compilation.Inference;
using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Language.Values;
using FluNET.Prompt.Surface;
using FluNET.Prompt.Expressions;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Actions;

public sealed class CompiledResourceReadAction(
    string kind,
    IExpression<string> resource,
    string alias,
    LanguageSnapshot language,
    IExecutionPolicy policy,
    IHttpTransport http,
    IResourceDecoderRegistry decoders,
    ISecretStore secrets,
    ISecretAccessPolicy secretPolicy,
    ISqlQueryExecutor sql) : ICompiledAction
{
    public string Kind => kind;

    public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default)
    {
        string source = resource.Evaluate(variables);
        SurfaceValueSyntax syntax = new(source, default);
        ResourceReference reference = new ResourceClassifier().Classify(syntax);
        ResourceFormat format = new FormatInference().Infer(reference);
        if (Kind.Equals("LOAD", StringComparison.OrdinalIgnoreCase) && reference is not FileResourceReference)
            throw new InvalidOperationException("LOAD inside FOR EACH accepts local file resources; use GET for other resource schemes.");

        object value = reference switch
        {
            FileResourceReference file => await ReadFileAsync(file, format, cancellationToken).ConfigureAwait(false),
            HttpResourceReference remote => await ReadHttpAsync(remote, format, cancellationToken).ConfigureAwait(false),
            EnvironmentResourceReference environment => ReadEnvironment(environment),
            SecretResourceReference secret => ReadSecret(secret),
            SqlResourceReference query => await ReadSqlAsync(query, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Nested resource '{reference.DisplayName}' has no action reader.")
        };
        variables.Register(alias, value);
    }

    private async ValueTask<object> ReadFileAsync(FileResourceReference file, ResourceFormat format, CancellationToken cancellationToken)
    {
        if (file.IsPattern) throw new NotSupportedException("Glob reads inside FOR EACH are not supported by the nested action contract.");
        policy.EnsureFileAccess(file.Path);
        byte[] content = await File.ReadAllBytesAsync(file.Path, cancellationToken).ConfigureAwait(false);
        ResourceDescriptor descriptor = Descriptor(file, format);
        return decoders.Decode(descriptor, new ResourcePayload(content, MediaType(file.Path, format)));
    }

    private async ValueTask<object> ReadHttpAsync(HttpResourceReference remote, ResourceFormat format, CancellationToken cancellationToken)
    {
        HttpResourceResponse response = await http.GetAsync(remote.Uri, cancellationToken).ConfigureAwait(false);
        ResourceDescriptor descriptor = Descriptor(remote, format);
        return decoders.Decode(descriptor, new ResourcePayload(response.Content, response.MediaType, response.Charset, remote.Uri));
    }

    private object ReadEnvironment(EnvironmentResourceReference environment)
    {
        string? value = new ProcessEnvironmentReader().Get(environment.Name);
        return value ?? throw new KeyNotFoundException($"Environment variable '{environment.Name}' is not defined.");
    }

    private object ReadSecret(SecretResourceReference secret)
    {
        secretPolicy.EnsureSecretAccess(secret.Name);
        return secrets.TryGet(secret.Name, out SecretValue? value) && value is not null
            ? value
            : throw new KeyNotFoundException($"Secret '{secret.Name}' is not defined.");
    }

    private async ValueTask<object> ReadSqlAsync(SqlResourceReference query, CancellationToken cancellationToken)
    {
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows = await sql.QueryAsync(query.Query, cancellationToken).ConfigureAwait(false);
        return rows.Select(row => JsonSerializer.SerializeToElement(row)).ToArray();
    }

    private ResourceDescriptor Descriptor(ResourceReference reference, ResourceFormat format)
    {
        TypeSymbol type = format switch
        {
            ResourceFormat.Json => language.Types.Json,
            ResourceFormat.Csv => language.Types.List(language.Types.Json),
            ResourceFormat.Xml => language.Types.Json,
            ResourceFormat.Text => language.Types.Text,
            ResourceFormat.Binary => language.Types.Get<BinaryValue>(),
            ResourceFormat.Image => language.Types.Get<ImageValue>(),
            _ => language.Types.Json
        };
        return new(reference, format, type, alias);
    }

    private static string? MediaType(string path, ResourceFormat format) => format switch
    {
        ResourceFormat.Json => "application/json", ResourceFormat.Text => "text/plain", ResourceFormat.Csv => "text/csv",
        ResourceFormat.Xml => "application/xml", ResourceFormat.Image => Path.GetExtension(path).ToLowerInvariant() switch
        { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", _ => null },
        ResourceFormat.Binary => "application/octet-stream", _ => null
    };
}

public sealed class CompiledSaveAction(
    string valueExpression,
    IExpression<string> target,
    IFluNetFileSystem files) : ICompiledAction
{
    public string Kind => "SAVE";
    public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default)
    {
        object? value = NestedActionValue.Resolve(valueExpression, variables);
        string path = target.Evaluate(variables);
        switch (value)
        {
            case BinaryValue binary:
                await files.WriteAllBytesAsync(path, binary.Content.ToArray(), cancellationToken).ConfigureAwait(false); break;
            case ImageValue image:
                await files.WriteAllBytesAsync(path, image.Content.ToArray(), cancellationToken).ConfigureAwait(false); break;
            case JsonElement json:
                await files.WriteAllTextAsync(path, json.GetRawText(), cancellationToken).ConfigureAwait(false); break;
            case JsonElement[] rows:
                await files.WriteAllTextAsync(path, JsonSerializer.Serialize(rows), cancellationToken).ConfigureAwait(false); break;
            case string text:
                await files.WriteAllTextAsync(path, text, cancellationToken).ConfigureAwait(false); break;
            default:
                await files.WriteAllTextAsync(path, JsonSerializer.Serialize(value), cancellationToken).ConfigureAwait(false); break;
        }
    }
}

public sealed class CompiledPostAction(
    string valueExpression,
    IExpression<string> target,
    IHttpTransport http) : ICompiledAction
{
    public string Kind => "POST";
    public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default)
    {
        string targetText = target.Evaluate(variables);
        if (!Uri.TryCreate(targetText, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
            throw new FormatException($"POST target '{targetText}' is not an absolute HTTP(S) URI.");
        object? value = NestedActionValue.Resolve(valueExpression, variables);
        string json = value switch
        {
            JsonElement element => element.GetRawText(),
            JsonElement[] rows => JsonSerializer.Serialize(rows),
            string text when LooksLikeJson(text) => text,
            _ => JsonSerializer.Serialize(value)
        };
        _ = await http.PostJsonAsync(uri, json, cancellationToken).ConfigureAwait(false);
    }
    private static bool LooksLikeJson(string value)
    {
        string text = value.Trim(); return (text.StartsWith('{') && text.EndsWith('}')) || (text.StartsWith('[') && text.EndsWith(']'));
    }
}

internal static class NestedActionValue
{
    public static object? Resolve(string expression, IVariableResolver variables)
    {
        string text = expression.Trim();
        if (text.Length >= 2 && text[0] == '[' && text[^1] == ']') text = text[1..^1].Trim();
        if (DynamicPathExpression.TryParse(text, out DynamicPathExpression? path)) return path!.Evaluate(variables);
        if (text.Length >= 2 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\''))) return text[1..^1];
        try { using JsonDocument document = JsonDocument.Parse(text); return document.RootElement.Clone(); }
        catch (JsonException) { return variables.Resolve<object>($"[{text}]") ?? text; }
    }
}

public sealed class SurfaceNestedActionCompiler(
    LanguageSnapshot language,
    IValueCodecRegistry values,
    ITextOutput output,
    IExecutionPolicy policy,
    IFluNetFileSystem files,
    IHttpTransport http,
    IResourceDecoderRegistry decoders,
    ISecretStore secrets,
    ISecretAccessPolicy secretPolicy,
    ISqlQueryExecutor sql,
    IFluNetDirectoryOperations directories,
    IFluNetFileOperations fileOperations,
    IFluNetFileTrash trash,
    IFluNetArchive archive,
    IFluNetMessageBus bus,
    IFluNetNotifier notifier)
{
    public CompiledActionTemplate Compile(IEnumerable<SurfaceIterationActionDescriptor> descriptors)
    {
        List<ICompiledAction> result = [];
        foreach (SurfaceIterationActionDescriptor descriptor in descriptors)
        {
            result.Add(descriptor.Kind.ToUpperInvariant() switch
            {
                "SAY" => new CompiledSayAction(Text(descriptor.Source), output),
                "GET" or "LOAD" => new CompiledResourceReadAction(descriptor.Kind, Text(descriptor.Source), descriptor.Alias!, language, policy, http, decoders, secrets, secretPolicy, sql),
                "SAVE" => new CompiledSaveAction(descriptor.Source, Text(descriptor.Target!), files),
                "POST" => new CompiledPostAction(descriptor.Source, Text(descriptor.Target!), http),
                "MKDIR" => new CompiledCreateDirectoryAction(Text(descriptor.Source), directories),
                "COPY" => new CompiledFileTransferAction(Text(descriptor.Source), Text(descriptor.Target!), fileOperations, move: false),
                "MOVE" => new CompiledFileTransferAction(Text(descriptor.Source), Text(descriptor.Target!), fileOperations, move: true),
                "TRASH" => new CompiledTrashAction(Text(descriptor.Source), trash),
                "PACK" => new CompiledArchiveAction(Text(descriptor.Source), Text(descriptor.Target!), archive, extract: false),
                "UNPACK" => new CompiledArchiveAction(Text(descriptor.Source), Text(descriptor.Target!), archive, extract: true),
                "PUBLISH" => new CompiledPublishAction(Text(descriptor.Source), Text(descriptor.Target!), bus),
                "NOTIFY" => new CompiledNotifyAction(Text(descriptor.Source), notifier),
                "INCREMENT" => new CompiledIncrementAction(Text(descriptor.Source)),
                "SET" => new CompiledSetAction(Text(descriptor.Source), Text(descriptor.Alias!)),
                "BREAK" => new CompiledLoopControlAction(LoopControlKind.Break, CompileCondition(descriptor.Source)),
                "CONTINUE" => new CompiledLoopControlAction(LoopControlKind.Continue, CompileCondition(descriptor.Source)),
                _ => throw new NotSupportedException($"Nested action '{descriptor.Kind}' is not supported.")
            });
        }
        return new CompiledActionTemplate(result);
    }

    private static CompiledCondition CompileCondition(string source) =>
        new ConditionExpressionCompiler().Compile(
            ExpressionSyntaxParser.Parse(ConditionExpressionCompiler.NormalizeNaturalCondition(source)));

    private IExpression<string> Text(string source)
    {
        string text = Unquote(source.Trim());
        return InterpolatedTextExpression.TryCreate(text, language, values, out IExpression<string>? expression)
            ? expression!
            : new LiteralExpression<string>(text);
    }
    private static string Unquote(string value) => value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')) ? value[1..^1] : value;
}

public sealed class CompiledIncrementAction(IExpression<string> target) : ICompiledAction
{
    public string Kind => "INCREMENT";

    public ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string name = target.Evaluate(variables).Trim().TrimStart('[').TrimEnd(']');
        object? current = variables.Resolve<object>($"[{name}]");
        decimal number = current switch
        {
            decimal value => value,
            IConvertible value => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture),
            null => 0m,
            _ => throw new InvalidOperationException($"INCREMENT requires numeric variable [{name}].")
        };
        variables.Register(name, number + 1m);
        return ValueTask.CompletedTask;
    }
}

public sealed class CompiledSetAction(IExpression<string> value, IExpression<string> target) : ICompiledAction
{
    public string Kind => "SET";

    public ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string name = target.Evaluate(variables).Trim().TrimStart('[').TrimEnd(']');
        object? resolved = NestedActionValue.Resolve(value.Evaluate(variables), variables);
        variables.Register(name, resolved ?? string.Empty);
        return ValueTask.CompletedTask;
    }
}

public sealed class CompiledLoopControlAction(LoopControlKind kind, CompiledCondition condition) : ICompiledAction
{
    public string Kind => kind.ToString().ToUpperInvariant();

    public ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (condition.Expression.Evaluate(new ExpressionEvaluationContext(variables)))
            throw new LoopControlSignal(kind);
        return ValueTask.CompletedTask;
    }
}
