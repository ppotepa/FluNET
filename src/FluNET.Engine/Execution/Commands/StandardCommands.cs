using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Syntax.Core;
using FluNET.Syntax.Verbs;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public abstract class FrameCommandBinder<TCommand, TResult, TImplementation>(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<TCommand, TResult>
    where TCommand : class, ICommand<TResult>
    where TImplementation : class, IVerb
{
    private readonly ExpressionBinder _expressions = new(
        language ?? throw new ArgumentNullException(nameof(language)),
        values ?? throw new ArgumentNullException(nameof(values)));

    public TCommand? TryBind(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Frame.ImplementationType == typeof(TImplementation)
            ? Bind(command)
            : null;
    }

    protected CommandBindingContext Context(BoundCommand command) =>
        new(command, _expressions);

    protected abstract TCommand Bind(BoundCommand command);
}

public sealed record GetTextCommand(IExpression<FileInfo> Source) : ICommand<string[]>;
public sealed record LoadTextCommand(IExpression<FileInfo> Source) : ICommand<string[]>;
public sealed record LoadConfigCommand(IExpression<FileInfo> Source)
    : ICommand<Dictionary<string, object>>;
public sealed record SaveTextCommand(
    IExpression<string> Theme,
    IExpression<FileInfo> Goal) : ICommand<string>;
public sealed record DeleteFileCommand(
    IExpression<string> Theme,
    IExpression<DirectoryInfo>? Source) : ICommand<string>;
public sealed record DownloadFileCommand(
    IExpression<Uri> Source,
    IExpression<FileInfo>? Goal) : ICommand<FileInfo>;
public sealed record PostJsonCommand(
    IExpression<string> Theme,
    IExpression<Uri> Goal) : ICommand<string>;
public sealed record SendEmailCommand(
    IExpression<string> Theme,
    IExpression<string> Recipient) : ICommand<string>;
public sealed record TransformEncodingCommand(
    IExpression<string> Theme,
    IExpression<System.Text.Encoding> Instrument) : ICommand<string>;

public sealed class GetTextCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<GetTextCommand, string[], GetText>(language, values)
{
    protected override GetTextCommand Bind(BoundCommand command) =>
        new(Context(command).Require<FileInfo>(SemanticRole.Source));
}

public sealed class LoadTextCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<LoadTextCommand, string[], LoadText>(language, values)
{
    protected override LoadTextCommand Bind(BoundCommand command) =>
        new(Context(command).Require<FileInfo>(SemanticRole.Source));
}

public sealed class LoadConfigCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<LoadConfigCommand, Dictionary<string, object>, LoadConfig>(language, values)
{
    protected override LoadConfigCommand Bind(BoundCommand command) =>
        new(Context(command).Require<FileInfo>(SemanticRole.Source));
}

public sealed class SaveTextCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<SaveTextCommand, string, SaveText>(language, values)
{
    protected override SaveTextCommand Bind(BoundCommand command)
    {
        CommandBindingContext context = Context(command);
        return new SaveTextCommand(
            context.RequireText(SemanticRole.Theme),
            context.Require<FileInfo>(SemanticRole.Goal));
    }
}

public sealed class DeleteFileCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<DeleteFileCommand, string, DeleteFile>(language, values)
{
    protected override DeleteFileCommand Bind(BoundCommand command)
    {
        CommandBindingContext context = Context(command);
        return new DeleteFileCommand(
            context.RequireText(SemanticRole.Theme),
            context.Optional<DirectoryInfo>(SemanticRole.Source));
    }
}

public sealed class DownloadFileCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<DownloadFileCommand, FileInfo, DownloadFile>(language, values)
{
    protected override DownloadFileCommand Bind(BoundCommand command)
    {
        CommandBindingContext context = Context(command);
        return new DownloadFileCommand(
            context.Require<Uri>(SemanticRole.Source),
            context.Optional<FileInfo>(SemanticRole.Goal));
    }
}

public sealed class PostJsonCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<PostJsonCommand, string, PostJson>(language, values)
{
    protected override PostJsonCommand Bind(BoundCommand command)
    {
        CommandBindingContext context = Context(command);
        return new PostJsonCommand(
            context.RequireText(SemanticRole.Theme, preserveStructuredReferences: true),
            context.Require<Uri>(SemanticRole.Goal));
    }
}

public sealed class SendEmailCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<SendEmailCommand, string, SendEmail>(language, values)
{
    protected override SendEmailCommand Bind(BoundCommand command)
    {
        CommandBindingContext context = Context(command);
        return new SendEmailCommand(
            context.RequireText(SemanticRole.Theme),
            context.Require<string>(SemanticRole.Recipient));
    }
}

public sealed class TransformEncodingCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) :
    FrameCommandBinder<TransformEncodingCommand, string, TransformEncoding>(language, values)
{
    protected override TransformEncodingCommand Bind(BoundCommand command)
    {
        CommandBindingContext context = Context(command);
        return new TransformEncodingCommand(
            context.RequireText(SemanticRole.Theme),
            context.Require<System.Text.Encoding>(SemanticRole.Instrument));
    }
}

public sealed class GetTextCommandHandler(
    IVariableResolver variables,
    IFluNetFileSystem files) : ICommandHandler<GetTextCommand, string[]>
{
    public ValueTask<string[]> HandleAsync(GetTextCommand command, CancellationToken cancellationToken = default) =>
        new(files.ReadAllLinesAsync(command.Source.Evaluate(variables).FullName, cancellationToken));
}

public sealed class LoadTextCommandHandler(
    IVariableResolver variables,
    IFluNetFileSystem files) : ICommandHandler<LoadTextCommand, string[]>
{
    public ValueTask<string[]> HandleAsync(LoadTextCommand command, CancellationToken cancellationToken = default) =>
        new(files.ReadAllLinesAsync(command.Source.Evaluate(variables).FullName, cancellationToken));
}

public sealed class LoadConfigCommandHandler(
    IVariableResolver variables,
    IFluNetFileSystem files) : ICommandHandler<LoadConfigCommand, Dictionary<string, object>>
{
    public async ValueTask<Dictionary<string, object>> HandleAsync(
        LoadConfigCommand command,
        CancellationToken cancellationToken = default)
    {
        FileInfo source = command.Source.Evaluate(variables);
        string json = await files.ReadAllTextAsync(source.FullName, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json)
            ?? throw new InvalidDataException($"Configuration file '{source.FullName}' contains JSON null.");
    }
}

public sealed class SaveTextCommandHandler(
    IVariableResolver variables,
    IFluNetFileSystem files) : ICommandHandler<SaveTextCommand, string>
{
    public async ValueTask<string> HandleAsync(
        SaveTextCommand command,
        CancellationToken cancellationToken = default)
    {
        string value = command.Theme.Evaluate(variables);
        FileInfo goal = command.Goal.Evaluate(variables);
        await files.WriteAllTextAsync(goal.FullName, value, cancellationToken).ConfigureAwait(false);
        return value;
    }
}

public sealed class DeleteFileCommandHandler(
    IVariableResolver variables,
    IFluNetFileSystem files) : ICommandHandler<DeleteFileCommand, string>
{
    public async ValueTask<string> HandleAsync(
        DeleteFileCommand command,
        CancellationToken cancellationToken = default)
    {
        string target = command.Theme.Evaluate(variables);
        string path = target.Contains(Path.DirectorySeparatorChar) || target.Contains(Path.AltDirectorySeparatorChar)
            ? target
            : Path.Combine(
                (command.Source?.Evaluate(variables) ?? new DirectoryInfo(".")).FullName,
                target);
        if (!await files.FileExistsAsync(path, cancellationToken).ConfigureAwait(false))
        {
            return $"File not found: {path}";
        }

        await files.DeleteFileAsync(path, cancellationToken).ConfigureAwait(false);
        return $"Deleted: {path}";
    }
}

public sealed class DownloadFileCommandHandler(
    IVariableResolver variables,
    IHttpTransport http,
    IFluNetFileSystem files) : ICommandHandler<DownloadFileCommand, FileInfo>
{
    public async ValueTask<FileInfo> HandleAsync(
        DownloadFileCommand command,
        CancellationToken cancellationToken = default)
    {
        Uri source = command.Source.Evaluate(variables);
        if (source.Scheme is not ("http" or "https"))
        {
            throw new FormatException("DOWNLOAD FROM requires an absolute HTTP or HTTPS URL.");
        }

        FileInfo goal = command.Goal?.Evaluate(variables) ?? new FileInfo(DefaultDownloadPath(source));
        byte[] content = await http.GetBytesAsync(source, cancellationToken).ConfigureAwait(false);
        await files.WriteAllBytesAsync(goal.FullName, content, cancellationToken).ConfigureAwait(false);
        return goal;
    }

    private static string DefaultDownloadPath(Uri source)
    {
        string name = Path.GetFileName(source.LocalPath);
        if (string.IsNullOrWhiteSpace(name) || name is "/" or "\\")
        {
            name = "downloaded_file";
        }
        if (!Path.HasExtension(name))
        {
            name += ".bin";
        }
        return Path.Combine(Directory.GetCurrentDirectory(), name);
    }
}

public sealed class PostJsonCommandHandler(
    IVariableResolver variables,
    IHttpTransport http) : ICommandHandler<PostJsonCommand, string>
{
    public async ValueTask<string> HandleAsync(
        PostJsonCommand command,
        CancellationToken cancellationToken = default) =>
        await http.PostJsonAsync(
            command.Goal.Evaluate(variables),
            command.Theme.Evaluate(variables),
            cancellationToken).ConfigureAwait(false);
}

public sealed class SendEmailCommandHandler(
    IVariableResolver variables,
    IEmailTransport email) : ICommandHandler<SendEmailCommand, string>
{
    public async ValueTask<string> HandleAsync(
        SendEmailCommand command,
        CancellationToken cancellationToken = default) =>
        await email.SendAsync(
            command.Recipient.Evaluate(variables),
            command.Theme.Evaluate(variables),
            cancellationToken).ConfigureAwait(false);
}

public sealed class TransformEncodingCommandHandler(IVariableResolver variables)
    : ICommandHandler<TransformEncodingCommand, string>
{
    public ValueTask<string> HandleAsync(
        TransformEncodingCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = command.Instrument.Evaluate(variables)
            .GetBytes(command.Theme.Evaluate(variables));
        return ValueTask.FromResult(Convert.ToBase64String(bytes));
    }
}
