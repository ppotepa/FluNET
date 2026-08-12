using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Syntax.Core;
using FluNET.Syntax.Verbs;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public abstract class FrameCommandBinder<TCommand, TResult, TImplementation>
    : ICommandBinder<TCommand, TResult>
    where TCommand : class, ICommand<TResult>
    where TImplementation : class, IVerb
{
    public TCommand? TryBind(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Frame.ImplementationType == typeof(TImplementation)
            ? Bind(command)
            : null;
    }

    protected abstract TCommand Bind(BoundCommand command);
}

public sealed record GetTextCommand(IValueExpression<FileInfo> Source) : ICommand<string[]>;
public sealed record LoadTextCommand(IValueExpression<FileInfo> Source) : ICommand<string[]>;
public sealed record LoadConfigCommand(IValueExpression<FileInfo> Source)
    : ICommand<Dictionary<string, object>>;
public sealed record SaveTextCommand(
    IValueExpression<string> Theme,
    IValueExpression<FileInfo> Goal) : ICommand<string>;
public sealed record DeleteFileCommand(
    IValueExpression<string> Theme,
    IValueExpression<DirectoryInfo>? Source) : ICommand<string>;
public sealed record DownloadFileCommand(
    IValueExpression<Uri> Source,
    IValueExpression<FileInfo>? Goal) : ICommand<FileInfo>;
public sealed record PostJsonCommand(
    IValueExpression<string> Theme,
    IValueExpression<Uri> Goal) : ICommand<string>;
public sealed record SendEmailCommand(
    IValueExpression<string> Theme,
    IValueExpression<string> Recipient) : ICommand<string>;
public sealed record TransformEncodingCommand(
    IValueExpression<string> Theme,
    IValueExpression<System.Text.Encoding> Instrument) : ICommand<string>;

public sealed class GetTextCommandBinder :
    FrameCommandBinder<GetTextCommand, string[], GetText>
{
    protected override GetTextCommand Bind(BoundCommand command) =>
        new(Expressions.File(command[SemanticRole.Source]));
}

public sealed class LoadTextCommandBinder :
    FrameCommandBinder<LoadTextCommand, string[], LoadText>
{
    protected override LoadTextCommand Bind(BoundCommand command) =>
        new(Expressions.File(command[SemanticRole.Source]));
}

public sealed class LoadConfigCommandBinder :
    FrameCommandBinder<LoadConfigCommand, Dictionary<string, object>, LoadConfig>
{
    protected override LoadConfigCommand Bind(BoundCommand command) =>
        new(Expressions.File(command[SemanticRole.Source]));
}

public sealed class SaveTextCommandBinder(LanguageSnapshot language) :
    FrameCommandBinder<SaveTextCommand, string, SaveText>
{
    protected override SaveTextCommand Bind(BoundCommand command) => new(
        TextExpression.Bind(command[SemanticRole.Theme], language),
        Expressions.File(command[SemanticRole.Goal]));
}

public sealed class DeleteFileCommandBinder(LanguageSnapshot language) :
    FrameCommandBinder<DeleteFileCommand, string, DeleteFile>
{
    protected override DeleteFileCommand Bind(BoundCommand command)
    {
        BoundArgument source = command[SemanticRole.Source];
        return new DeleteFileCommand(
            TextExpression.Bind(command[SemanticRole.Theme], language),
            source.IsPresent ? Expressions.Directory(source) : null);
    }
}

public sealed class DownloadFileCommandBinder :
    FrameCommandBinder<DownloadFileCommand, FileInfo, DownloadFile>
{
    protected override DownloadFileCommand Bind(BoundCommand command)
    {
        BoundArgument goal = command[SemanticRole.Goal];
        return new DownloadFileCommand(
            Expressions.Uri(command[SemanticRole.Source]),
            goal.IsPresent ? Expressions.File(goal) : null);
    }
}

public sealed class PostJsonCommandBinder(LanguageSnapshot language) :
    FrameCommandBinder<PostJsonCommand, string, PostJson>
{
    protected override PostJsonCommand Bind(BoundCommand command) => new(
        TextExpression.Bind(
            command[SemanticRole.Theme],
            language,
            preserveStructuredReferences: true),
        Expressions.Uri(command[SemanticRole.Goal]));
}

public sealed class SendEmailCommandBinder(LanguageSnapshot language) :
    FrameCommandBinder<SendEmailCommand, string, SendEmail>
{
    protected override SendEmailCommand Bind(BoundCommand command) => new(
        TextExpression.Bind(command[SemanticRole.Theme], language),
        Expressions.String(command[SemanticRole.Recipient]));
}

public sealed class TransformEncodingCommandBinder(LanguageSnapshot language) :
    FrameCommandBinder<TransformEncodingCommand, string, TransformEncoding>
{
    protected override TransformEncodingCommand Bind(BoundCommand command) => new(
        TextExpression.Bind(command[SemanticRole.Theme], language),
        Expressions.Encoding(command[SemanticRole.Instrument]));
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

internal static class Expressions
{
    internal static ScalarExpression<string> String(BoundArgument argument) =>
        new(argument, new StringValueConverter());
    internal static ScalarExpression<FileInfo> File(BoundArgument argument) =>
        new(argument, new FileInfoValueConverter());
    internal static ScalarExpression<DirectoryInfo> Directory(BoundArgument argument) =>
        new(argument, new DirectoryInfoValueConverter());
    internal static ScalarExpression<Uri> Uri(BoundArgument argument) =>
        new(argument, new UriValueConverter());
    internal static ScalarExpression<System.Text.Encoding> Encoding(BoundArgument argument) =>
        new(argument, new EncodingValueConverter());
}
