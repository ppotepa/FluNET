using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Resources;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record LoadCsvCommand(IExpression<string> Source) : ICommand<JsonElement[]>;
public sealed record LoadXmlCommand(IExpression<string> Source) : ICommand<JsonElement>;

public sealed class LoadCsvCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<LoadCsvCommand, JsonElement[]>
{
    public LoadCsvCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.load.csv")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new LoadCsvCommand(context.RequireText(SemanticRole.Source));
    }
}

public sealed class LoadXmlCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<LoadXmlCommand, JsonElement>
{
    public LoadXmlCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.load.xml")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new LoadXmlCommand(context.RequireText(SemanticRole.Source));
    }
}

public sealed class LoadCsvCommandHandler(
    IVariableResolver variables,
    IFluNetFileSystem files,
    IResourceDecoderRegistry decoders,
    LanguageSnapshot language) : ICommandHandler<LoadCsvCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(LoadCsvCommand command, CancellationToken cancellationToken = default)
    {
        string path = command.Source.Evaluate(variables);
        string text = await files.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        ResourceDescriptor descriptor = new(new FileResourceReference(path, !Path.IsPathRooted(path)), ResourceFormat.Csv, language.Types.List(language.Types.Json), Path.GetFileNameWithoutExtension(path));
        object decoded = decoders.Decode(descriptor, ResourcePayload.FromText(text, "text/csv"));
        return decoded as JsonElement[] ?? throw new InvalidOperationException("CSV decoder must return JsonElement[].");
    }
}

public sealed class LoadXmlCommandHandler(
    IVariableResolver variables,
    IFluNetFileSystem files,
    IResourceDecoderRegistry decoders,
    LanguageSnapshot language) : ICommandHandler<LoadXmlCommand, JsonElement>
{
    public async ValueTask<JsonElement> HandleAsync(LoadXmlCommand command, CancellationToken cancellationToken = default)
    {
        string path = command.Source.Evaluate(variables);
        string text = await files.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        ResourceDescriptor descriptor = new(new FileResourceReference(path, !Path.IsPathRooted(path)), ResourceFormat.Xml, language.Types.Json, Path.GetFileNameWithoutExtension(path));
        object decoded = decoders.Decode(descriptor, ResourcePayload.FromText(text, "application/xml"));
        return decoded is JsonElement json ? json : throw new InvalidOperationException("XML decoder must return JsonElement.");
    }
}
