using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Resources;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record GetHttpJsonCommand(IExpression<Uri> Source) : ICommand<JsonElement>;
public sealed record GetHttpTextCommand(IExpression<Uri> Source) : ICommand<string>;
public sealed record GetHttpCsvCommand(IExpression<Uri> Source) : ICommand<JsonElement[]>;
public sealed record GetHttpXmlCommand(IExpression<Uri> Source) : ICommand<JsonElement>;
public sealed record GetHttpBinaryCommand(IExpression<Uri> Source) : ICommand<BinaryValue>;
public sealed record GetHttpImageCommand(IExpression<Uri> Source) : ICommand<ImageValue>;
public sealed record GetEnvironmentCommand(IExpression<string> Name) : ICommand<string>;

public sealed class GetHttpJsonCommandBinder(LanguageSnapshot language,IValueCodecRegistry values):ICommandBinder<GetHttpJsonCommand,JsonElement>{public GetHttpJsonCommand? TryBind(BoundCommand command)=>Bind(command,"surface.get.http.json",context=>new GetHttpJsonCommand(context.Require<Uri>(SemanticRole.Source)));private T? Bind<T>(BoundCommand command,string id,Func<CommandBindingContext,T> bind)where T:class{if(command.Frame.Id!=new FrameId(id))return null;return bind(new(command,new ExpressionBinder(language,values)));}}
public sealed class GetHttpTextCommandBinder(LanguageSnapshot language,IValueCodecRegistry values):ICommandBinder<GetHttpTextCommand,string>{public GetHttpTextCommand? TryBind(BoundCommand command){if(command.Frame.Id!=new FrameId("surface.get.http.text"))return null;CommandBindingContext c=new(command,new ExpressionBinder(language,values));return new(c.Require<Uri>(SemanticRole.Source));}}
public sealed class GetHttpCsvCommandBinder(LanguageSnapshot language,IValueCodecRegistry values):ICommandBinder<GetHttpCsvCommand,JsonElement[]>{public GetHttpCsvCommand? TryBind(BoundCommand command){if(command.Frame.Id!=new FrameId("surface.get.http.csv"))return null;CommandBindingContext c=new(command,new ExpressionBinder(language,values));return new(c.Require<Uri>(SemanticRole.Source));}}
public sealed class GetHttpXmlCommandBinder(LanguageSnapshot language,IValueCodecRegistry values):ICommandBinder<GetHttpXmlCommand,JsonElement>{public GetHttpXmlCommand? TryBind(BoundCommand command){if(command.Frame.Id!=new FrameId("surface.get.http.xml"))return null;CommandBindingContext c=new(command,new ExpressionBinder(language,values));return new(c.Require<Uri>(SemanticRole.Source));}}
public sealed class GetHttpBinaryCommandBinder(LanguageSnapshot language,IValueCodecRegistry values):ICommandBinder<GetHttpBinaryCommand,BinaryValue>{public GetHttpBinaryCommand? TryBind(BoundCommand command){if(command.Frame.Id!=new FrameId("surface.get.http.binary"))return null;CommandBindingContext c=new(command,new ExpressionBinder(language,values));return new(c.Require<Uri>(SemanticRole.Source));}}
public sealed class GetHttpImageCommandBinder(LanguageSnapshot language,IValueCodecRegistry values):ICommandBinder<GetHttpImageCommand,ImageValue>{public GetHttpImageCommand? TryBind(BoundCommand command){if(command.Frame.Id!=new FrameId("surface.get.http.image"))return null;CommandBindingContext c=new(command,new ExpressionBinder(language,values));return new(c.Require<Uri>(SemanticRole.Source));}}

public sealed class GetEnvironmentCommandBinder(LanguageSnapshot language,IValueCodecRegistry values):ICommandBinder<GetEnvironmentCommand,string>{public GetEnvironmentCommand? TryBind(BoundCommand command){if(command.Frame.Id!=new FrameId("surface.get.environment"))return null;CommandBindingContext context=new(command,new ExpressionBinder(language,values));return new(context.RequireText(SemanticRole.Source));}}

internal static class HttpResourceRuntime
{
    public static async ValueTask<T> ReadAsync<T>(Uri uri, ResourceFormat format, TypeSymbol type, IHttpTransport http, IResourceDecoderRegistry decoders, CancellationToken cancellationToken)
    {
        HttpResourceResponse response = await http.GetAsync(uri,cancellationToken).ConfigureAwait(false);
        ResourceFormat mediaFormat = FormatFromMediaType(response.MediaType);
        if (mediaFormat != ResourceFormat.Unknown && mediaFormat != format && !(format == ResourceFormat.Json && response.MediaType?.EndsWith("+json",StringComparison.OrdinalIgnoreCase)==true))
            throw new InvalidDataException($"HTTP resource '{uri}' was compiled as {format} but responded with '{response.MediaType}'.");
        ResourceDescriptor descriptor = new(new HttpResourceReference(uri),format,type,"response");
        object value = decoders.Decode(descriptor,new ResourcePayload(response.Content,response.MediaType,response.Charset,uri));
        return value is T typed ? typed : throw new InvalidOperationException($"Decoder returned {value.GetType().Name}; expected {typeof(T).Name}.");
    }
    private static ResourceFormat FormatFromMediaType(string? mediaType)
    {
        if(string.IsNullOrWhiteSpace(mediaType))return ResourceFormat.Unknown;string value=mediaType.Split(';')[0].Trim().ToLowerInvariant();
        if(value=="application/json"||value.EndsWith("+json",StringComparison.Ordinal))return ResourceFormat.Json;
        if(value is "text/plain" or "text/markdown")return ResourceFormat.Text;
        if(value is "text/csv" or "application/csv")return ResourceFormat.Csv;
        if(value is "application/xml" or "text/xml"||value.EndsWith("+xml",StringComparison.Ordinal))return ResourceFormat.Xml;
        if(value.StartsWith("image/",StringComparison.Ordinal))return ResourceFormat.Image;
        if(value=="application/octet-stream")return ResourceFormat.Binary;
        return ResourceFormat.Unknown;
    }
}

public sealed class GetHttpJsonCommandHandler(IVariableResolver variables,IHttpTransport http,IResourceDecoderRegistry decoders,LanguageSnapshot language):ICommandHandler<GetHttpJsonCommand,JsonElement>{public ValueTask<JsonElement> HandleAsync(GetHttpJsonCommand command,CancellationToken cancellationToken=default)=>HttpResourceRuntime.ReadAsync<JsonElement>(command.Source.Evaluate(variables),ResourceFormat.Json,language.Types.Json,http,decoders,cancellationToken);}
public sealed class GetHttpTextCommandHandler(IVariableResolver variables,IHttpTransport http,IResourceDecoderRegistry decoders,LanguageSnapshot language):ICommandHandler<GetHttpTextCommand,string>{public ValueTask<string> HandleAsync(GetHttpTextCommand command,CancellationToken cancellationToken=default)=>HttpResourceRuntime.ReadAsync<string>(command.Source.Evaluate(variables),ResourceFormat.Text,language.Types.Text,http,decoders,cancellationToken);}
public sealed class GetHttpCsvCommandHandler(IVariableResolver variables,IHttpTransport http,IResourceDecoderRegistry decoders,LanguageSnapshot language):ICommandHandler<GetHttpCsvCommand,JsonElement[]>{public ValueTask<JsonElement[]> HandleAsync(GetHttpCsvCommand command,CancellationToken cancellationToken=default)=>HttpResourceRuntime.ReadAsync<JsonElement[]>(command.Source.Evaluate(variables),ResourceFormat.Csv,language.Types.List(language.Types.Json),http,decoders,cancellationToken);}
public sealed class GetHttpXmlCommandHandler(IVariableResolver variables,IHttpTransport http,IResourceDecoderRegistry decoders,LanguageSnapshot language):ICommandHandler<GetHttpXmlCommand,JsonElement>{public ValueTask<JsonElement> HandleAsync(GetHttpXmlCommand command,CancellationToken cancellationToken=default)=>HttpResourceRuntime.ReadAsync<JsonElement>(command.Source.Evaluate(variables),ResourceFormat.Xml,language.Types.Json,http,decoders,cancellationToken);}
public sealed class GetHttpBinaryCommandHandler(IVariableResolver variables,IHttpTransport http,IResourceDecoderRegistry decoders,LanguageSnapshot language):ICommandHandler<GetHttpBinaryCommand,BinaryValue>{public ValueTask<BinaryValue> HandleAsync(GetHttpBinaryCommand command,CancellationToken cancellationToken=default)=>HttpResourceRuntime.ReadAsync<BinaryValue>(command.Source.Evaluate(variables),ResourceFormat.Binary,language.Types.Get<BinaryValue>(),http,decoders,cancellationToken);}
public sealed class GetHttpImageCommandHandler(IVariableResolver variables,IHttpTransport http,IResourceDecoderRegistry decoders,LanguageSnapshot language):ICommandHandler<GetHttpImageCommand,ImageValue>{public ValueTask<ImageValue> HandleAsync(GetHttpImageCommand command,CancellationToken cancellationToken=default)=>HttpResourceRuntime.ReadAsync<ImageValue>(command.Source.Evaluate(variables),ResourceFormat.Image,language.Types.Get<ImageValue>(),http,decoders,cancellationToken);}

public sealed class GetEnvironmentCommandHandler(IVariableResolver variables):ICommandHandler<GetEnvironmentCommand,string>{private readonly IEnvironmentReader _environment=new ProcessEnvironmentReader();public ValueTask<string> HandleAsync(GetEnvironmentCommand command,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();string name=command.Name.Evaluate(variables);string value=_environment.Get(name)??throw new KeyNotFoundException($"Environment variable '{name}' is not defined.");return ValueTask.FromResult(value);}}
