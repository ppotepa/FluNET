using FluNET.Execution.Commands;
using FluNET.Language.Values;
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
            .Route<LoadConfigCommand, object, LoadConfigCommandBinder, LoadConfigCommandHandler>("core.load.config")
            .Route<SaveTextCommand, string, SaveTextCommandBinder, SaveTextCommandHandler>("core.save.text")
            .Route<DeleteFileCommand, string, DeleteFileCommandBinder, DeleteFileCommandHandler>("core.delete.file")
            .Route<DownloadFileCommand, FileInfo, DownloadFileCommandBinder, DownloadFileCommandHandler>("core.download.file")
            .Route<PostJsonCommand, string, PostJsonCommandBinder, PostJsonCommandHandler>("core.post.json")
            .Route<PutJsonCommand, string, PutJsonCommandBinder, PutJsonCommandHandler>("core.put.json")
            .Route<PatchJsonCommand, string, PatchJsonCommandBinder, PatchJsonCommandHandler>("core.patch.json")
            .Route<DeleteHttpCommand, string, DeleteHttpCommandBinder, DeleteHttpCommandHandler>("core.delete.http")
            .Route<SendEmailCommand, string, SendEmailCommandBinder, SendEmailCommandHandler>("core.send.email")
            .Route<TransformEncodingCommand, string, TransformEncodingCommandBinder, TransformEncodingCommandHandler>("core.transform.encoding")
            .Route<SetTextCommand, string, SetTextCommandBinder, SetTextCommandHandler>("core.set.text")
            .Route<SetJsonCommand, JsonElement, SetJsonCommandBinder, SetJsonCommandHandler>("core.set.json")
            .Route<SetNumberCommand, decimal, SetNumberCommandBinder, SetNumberCommandHandler>("core.set.number")
            .Route<SetBooleanCommand, bool, SetBooleanCommandBinder, SetBooleanCommandHandler>("core.set.boolean")
            .Route<ParseJsonCommand, JsonElement, ParseJsonCommandBinder, ParseJsonCommandHandler>("core.parse.json")
            .Route<FormatJsonCommand, string, FormatJsonCommandBinder, FormatJsonCommandHandler>("core.format.json")
            .Conversion<IReadOnlyList<string>, string, TextListToTextConversion>();
        new SurfaceCollectionLanguageModule().Register(module);
        new SurfaceSystemLanguageModule().Register(module);
        new SurfaceStorageLanguageModule().Register(module);
        new SurfaceBlobStorageLanguageModule().Register(module);
        new SurfaceProcessLanguageModule().Register(module);
        new SurfaceArchiveLanguageModule().Register(module);
        new SurfaceSearchLanguageModule().Register(module);
        new SurfaceIndexLanguageModule().Register(module);
        new SurfaceSqlLanguageModule().Register(module);
        new SurfaceDirectoryLanguageModule().Register(module);
        new SurfaceMessagingLanguageModule().Register(module);
        new SurfaceHttpPaginationLanguageModule().Register(module);
        new SurfaceHttpResponseLanguageModule().Register(module);
        new SurfaceEventSinkLanguageModule().Register(module);
        new SurfaceTextLanguageModule().Register(module);
        new SurfaceWhileLanguageModule().Register(module);
        module
            .Route<GetConfigurationCommand, string, GetConfigurationCommandBinder, GetConfigurationCommandHandler>("surface.get.configuration");
        new SurfaceConfigurationLanguageModule().Register(module);
    }

    public void Register(LanguageBuilder language)
    {
        FrameRoleId credential = new("Credential");
        language
            .Module(StandardLanguageIdentity.Module.Value)
            .Version(StandardLanguageIdentity.Version.Value)
            .Type<decimal>("Number");

        language.ClauseMarker("FROM", Prompt.PromptClauseKind.From)
            .ClauseMarker("TO", Prompt.PromptClauseKind.To)
            .ClauseMarker("IN", Prompt.PromptClauseKind.From)
            .ClauseMarker("ENV", Prompt.PromptClauseKind.Using)
            .ClauseMarker("USING", Prompt.PromptClauseKind.Using)
            .CommandConnector("THEN", Prompt.CommandLinkKind.Sequence)
            .CommandConnector("AND", Prompt.CommandLinkKind.Parallel)
            .CommandConnector("SEQUENCE", Prompt.CommandLinkKind.Sequence)
            .CommandConnector("PARALLEL", Prompt.CommandLinkKind.Parallel)
            .CommandConnector("ELSE", Prompt.CommandLinkKind.Alternative)
            .CommandModifier("WITH", "RETRY", Prompt.CommandModifierKind.Retry)
            .CommandModifier("WITH", "TIMEOUT", Prompt.CommandModifierKind.Timeout)
            .CommandModifier("ON", "ERROR", Prompt.CommandModifierKind.ErrorPolicy)
            .CommandModifier("IF", null, Prompt.CommandModifierKind.Condition);

        language.Keyword("FROM")
            .Keyword("TO")
            .Keyword("IN")
            .Keyword("ENV")
            .Keyword("USING")
            .Keyword("THEN")
            .Keyword("AND")
            .Keyword("SEQUENCE")
            .Keyword("PARALLEL")
            .Keyword("ELSE");

        language.Command<GetTextCommand, string[]>("GET", "Text")
            .FrameId("core.get.text")
            .Aliases("FETCH", "RETRIEVE")
            .Qualifiers("TEXT")
            .Positional<string[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM");

        language.Command<SaveTextCommand, string>("SAVE", "Text")
            .FrameId("core.save.text")
            .Qualifiers("TEXT")
            .Positional<string>(SemanticRole.Theme)
            .Marked<FileInfo>(SemanticRole.Goal, "TO");

        language.Command<LoadTextCommand, string[]>("LOAD", "Text")
            .FrameId("core.load.text")
            .Default()
            .Qualifiers("TEXT")
            .Positional<string[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM");

        language.Command<LoadConfigCommand, object>("LOAD", "Config")
            .FrameId("core.load.config")
            .Qualifiers("CONFIG", "JSON")
            .Positional<Dictionary<string, object>>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM");

        language.Command<DeleteFileCommand, string>("DELETE", "File")
            .FrameId("core.delete.file")
            .Positional<string>(SemanticRole.Theme)
            .Marked<DirectoryInfo>(SemanticRole.Source, "FROM", SlotCardinality.Optional);

        language.Command<DownloadFileCommand, FileInfo>("DOWNLOAD", "File")
            .FrameId("core.download.file")
            .Aliases("PULL", "GRAB", "OBTAIN")
            .Positional<FileInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<Uri>(SemanticRole.Source, "FROM")
            .Marked<FileInfo>(SemanticRole.Goal, "TO", SlotCardinality.Optional);

        language.Command<PostJsonCommand, string>("POST", "Json")
            .FrameId("core.post.json")
            .Qualifiers("JSON")
            .Positional<string>(SemanticRole.Theme)
            .Marked<Uri>(SemanticRole.Goal, "TO")
            .Marked<string>(credential, "USING", SlotCardinality.Optional);

        language.Command<PutJsonCommand, string>("PUTJSON", "Json")
            .FrameId("core.put.json")
            .Qualifiers("JSON")
            .Positional<string>(SemanticRole.Theme)
            .Marked<Uri>(SemanticRole.Goal, "TO")
            .Marked<string>(credential, "USING", SlotCardinality.Optional);

        language.Command<PatchJsonCommand, string>("PATCHJSON", "Json")
            .FrameId("core.patch.json")
            .Qualifiers("JSON")
            .Positional<string>(SemanticRole.Theme)
            .Marked<Uri>(SemanticRole.Goal, "TO")
            .Marked<string>(credential, "USING", SlotCardinality.Optional);

        language.Command<DeleteHttpCommand, string>("DELETEHTTP", "Http")
            .FrameId("core.delete.http")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<Uri>(SemanticRole.Goal, "FROM")
            .Marked<string>(credential, "USING", SlotCardinality.Optional);

        language.Command<SayCommand, string>("SAY", "Text")
            .FrameId("core.say.text")
            .Aliases("ECHO", "PRINT", "OUTPUT", "WRITE")
            .Positional<string>(SemanticRole.Theme);

        language.Command<SendEmailCommand, string>("SEND", "Email")
            .FrameId("core.send.email")
            .Positional<string>(SemanticRole.Theme)
            .Marked<string>(SemanticRole.Recipient, "TO");

        language.Command<TransformEncodingCommand, string>("TRANSFORM", "Encoding")
            .FrameId("core.transform.encoding")
            .Positional<string>(SemanticRole.Theme)
            .Marked<Encoding>(SemanticRole.Instrument, "USING");

        language.Command<SetTextCommand, string>("SET", "Text")
            .FrameId("core.set.text")
            .Default()
            .Qualifiers("TEXT")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Theme, "TO", SlotCardinality.Repeated);

        language.Command<SetJsonCommand, JsonElement>("SET", "Json")
            .FrameId("core.set.json")
            .Qualifiers("JSON")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement>(SemanticRole.Theme, "TO");

        language.Command<SetNumberCommand, decimal>("SET", "Number")
            .FrameId("core.set.number")
            .Qualifiers("NUMBER")
            .Positional<decimal>(SemanticRole.Output, SlotDirection.Output)
            .Marked<decimal>(SemanticRole.Theme, "TO");

        language.Command<SetBooleanCommand, bool>("SET", "Boolean")
            .FrameId("core.set.boolean")
            .Qualifiers("BOOLEAN", "BOOL")
            .Positional<bool>(SemanticRole.Output, SlotDirection.Output)
            .Marked<bool>(SemanticRole.Theme, "TO");

        language.Command<ParseJsonCommand, JsonElement>("PARSE", "Json")
            .FrameId("core.parse.json")
            .Qualifiers("JSON")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");

        language.Command<FormatJsonCommand, string>("FORMAT", "Json")
            .FrameId("core.format.json")
            .Qualifiers("JSON")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement>(SemanticRole.Source, "FROM");

        new SurfaceConfigurationLanguageModule().Register(language);
        new SurfaceTextLanguageModule().Register(language);

    }
}

public static class StandardLanguage
{
    public static LanguageSnapshot CreateSnapshot() =>
        new LanguageBuilder().AddModule(new StandardLanguageModule()).Build();

    public static FluNetRuntimeDefinition CreateRuntime() =>
        new FluNetModuleBuilder().AddModule(new StandardLanguageModule()).Build();
}
