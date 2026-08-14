using FluNET.Execution.Commands;
using FluNET.Keywords;
using FluNET.Language.Values;
using FluNET.Syntax.Verbs;
using System.Text;
using System.Text.Json;

namespace FluNET.Language;

/// <summary>Declarative definition of the language shipped with FluNET.</summary>
public sealed class StandardLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        ArgumentNullException.ThrowIfNull(module);
        Register(module.Language);
        module
            .Route<SayCommand, string, SayCommandBinder, SayCommandHandler>("core.say.text")
            .Route<GetTextCommand, string[], GetTextCommandBinder, GetTextCommandHandler>("core.get.text")
            .Route<LoadTextCommand, string[], LoadTextCommandBinder, LoadTextCommandHandler>("core.load.text")
            .Route<LoadConfigCommand, Dictionary<string, object>, LoadConfigCommandBinder, LoadConfigCommandHandler>("core.load.config")
            .Route<SaveTextCommand, string, SaveTextCommandBinder, SaveTextCommandHandler>("core.save.text")
            .Route<DeleteFileCommand, string, DeleteFileCommandBinder, DeleteFileCommandHandler>("core.delete.file")
            .Route<DownloadFileCommand, FileInfo, DownloadFileCommandBinder, DownloadFileCommandHandler>("core.download.file")
            .Route<PostJsonCommand, string, PostJsonCommandBinder, PostJsonCommandHandler>("core.post.json")
            .Route<SendEmailCommand, string, SendEmailCommandBinder, SendEmailCommandHandler>("core.send.email")
            .Route<TransformEncodingCommand, string, TransformEncodingCommandBinder, TransformEncodingCommandHandler>("core.transform.encoding")
            .Route<SetTextCommand, string, SetTextCommandBinder, SetTextCommandHandler>("core.set.text")
            .Route<SetJsonCommand, JsonElement, SetJsonCommandBinder, SetJsonCommandHandler>("core.set.json")
            .Route<SetNumberCommand, decimal, SetNumberCommandBinder, SetNumberCommandHandler>("core.set.number")
            .Route<SetBooleanCommand, bool, SetBooleanCommandBinder, SetBooleanCommandHandler>("core.set.boolean")
            .Route<ParseJsonCommand, JsonElement, ParseJsonCommandBinder, ParseJsonCommandHandler>("core.parse.json")
            .Route<FormatJsonCommand, string, FormatJsonCommandBinder, FormatJsonCommandHandler>("core.format.json")
            .Conversion<IReadOnlyList<string>, string, TextListToTextConversion>();
    }

    public void Register(LanguageBuilder language)
    {
        language
            .Module(StandardLanguageIdentity.Module.Value)
            .Version(StandardLanguageIdentity.Version.Value)
            .Type<decimal>("Number");

        language.ClauseMarker("FROM", Prompt.PromptClauseKind.From)
            .ClauseMarker("TO", Prompt.PromptClauseKind.To)
            .ClauseMarker("USING", Prompt.PromptClauseKind.Using)
            .CommandConnector("THEN", Prompt.CommandLinkKind.Sequence)
            .CommandConnector("AND", Prompt.CommandLinkKind.Parallel)
            .CommandConnector("ELSE", Prompt.CommandLinkKind.Alternative)
            .CommandModifier("WITH", "RETRY", Prompt.CommandModifierKind.Retry)
            .CommandModifier("WITH", "TIMEOUT", Prompt.CommandModifierKind.Timeout)
            .CommandModifier("ON", "ERROR", Prompt.CommandModifierKind.ErrorPolicy)
            .CommandModifier("IF", null, Prompt.CommandModifierKind.Condition);

        language.Keyword<From>("FROM")
            .Keyword<To>("TO")
            .Keyword<Using>("USING")
            .Keyword<Then>("THEN")
            .Keyword<And>("AND")
            .Keyword<Else>("ELSE");

        language.Command<GetText, string[]>("GET", "Text")
            .FrameId("core.get.text")
            .Aliases("FETCH", "RETRIEVE")
            .Qualifiers("TEXT")
            .Positional<string[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM");

        language.Command<SaveText, string>("SAVE", "Text")
            .FrameId("core.save.text")
            .Qualifiers("TEXT")
            .Positional<string>(SemanticRole.Theme)
            .Marked<FileInfo>(SemanticRole.Goal, "TO");

        language.Command<LoadText, string[]>("LOAD", "Text")
            .FrameId("core.load.text")
            .Default()
            .Qualifiers("TEXT")
            .Positional<string[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM");

        language.Command<LoadConfig, Dictionary<string, object>>("LOAD", "Config")
            .FrameId("core.load.config")
            .Qualifiers("CONFIG", "JSON")
            .Positional<Dictionary<string, object>>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM");

        language.Command<DeleteFile, string>("DELETE", "File")
            .FrameId("core.delete.file")
            .Positional<string>(SemanticRole.Theme)
            .Marked<DirectoryInfo>(SemanticRole.Source, "FROM", SlotCardinality.Optional);

        language.Command<DownloadFile, FileInfo>("DOWNLOAD", "File")
            .FrameId("core.download.file")
            .Aliases("PULL", "GRAB", "OBTAIN")
            .Positional<FileInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<Uri>(SemanticRole.Source, "FROM")
            .Marked<FileInfo>(SemanticRole.Goal, "TO", SlotCardinality.Optional);

        language.Command<PostJson, string>("POST", "Json")
            .FrameId("core.post.json")
            .Qualifiers("JSON")
            .Positional<string>(SemanticRole.Theme)
            .Marked<Uri>(SemanticRole.Goal, "TO");

        language.Command<SayText, string>("SAY", "Text")
            .FrameId("core.say.text")
            .Aliases("ECHO", "PRINT", "OUTPUT", "WRITE")
            .Positional<string>(SemanticRole.Theme);

        language.Command<SendEmail, string>("SEND", "Email")
            .FrameId("core.send.email")
            .Positional<string>(SemanticRole.Theme)
            .Marked<string>(SemanticRole.Recipient, "TO");

        language.Command<TransformEncoding, string>("TRANSFORM", "Encoding")
            .FrameId("core.transform.encoding")
            .Positional<string>(SemanticRole.Theme)
            .Marked<Encoding>(SemanticRole.Instrument, "USING");

        language.Command<SetText, string>("SET", "Text")
            .FrameId("core.set.text")
            .Default()
            .Qualifiers("TEXT")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Theme, "TO", SlotCardinality.Repeated);

        language.Command<SetJson, JsonElement>("SET", "Json")
            .FrameId("core.set.json")
            .Qualifiers("JSON")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement>(SemanticRole.Theme, "TO");

        language.Command<SetNumber, decimal>("SET", "Number")
            .FrameId("core.set.number")
            .Qualifiers("NUMBER")
            .Positional<decimal>(SemanticRole.Output, SlotDirection.Output)
            .Marked<decimal>(SemanticRole.Theme, "TO");

        language.Command<SetBoolean, bool>("SET", "Boolean")
            .FrameId("core.set.boolean")
            .Qualifiers("BOOLEAN", "BOOL")
            .Positional<bool>(SemanticRole.Output, SlotDirection.Output)
            .Marked<bool>(SemanticRole.Theme, "TO");

        language.Command<ParseJson, JsonElement>("PARSE", "Json")
            .FrameId("core.parse.json")
            .Qualifiers("JSON")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");

        language.Command<FormatJson, string>("FORMAT", "Json")
            .FrameId("core.format.json")
            .Qualifiers("JSON")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement>(SemanticRole.Source, "FROM");
    }
}

public static class StandardLanguage
{
    public static LanguageSnapshot CreateSnapshot() =>
        new LanguageBuilder().AddModule(new StandardLanguageModule()).Build();

    public static FluNetRuntimeDefinition CreateRuntime() =>
        new FluNetModuleBuilder().AddModule(new StandardLanguageModule()).Build();
}
