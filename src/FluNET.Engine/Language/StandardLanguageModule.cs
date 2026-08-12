using FluNET.Keywords;
using FluNET.Syntax.Verbs;
using System.Text;
using FluNET.Execution.Commands;

namespace FluNET.Language;

/// <summary>Declarative definition of the language shipped with FluNET.</summary>
public sealed class StandardLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        ArgumentNullException.ThrowIfNull(module);
        Register(module.Language);
        module
            .Route<SayText, SayCommand, string, SayCommandBinder, SayCommandHandler>()
            .Route<GetText, GetTextCommand, string[], GetTextCommandBinder, GetTextCommandHandler>()
            .Route<LoadText, LoadTextCommand, string[], LoadTextCommandBinder, LoadTextCommandHandler>()
            .Route<LoadConfig, LoadConfigCommand, Dictionary<string, object>, LoadConfigCommandBinder, LoadConfigCommandHandler>()
            .Route<SaveText, SaveTextCommand, string, SaveTextCommandBinder, SaveTextCommandHandler>()
            .Route<DeleteFile, DeleteFileCommand, string, DeleteFileCommandBinder, DeleteFileCommandHandler>()
            .Route<DownloadFile, DownloadFileCommand, FileInfo, DownloadFileCommandBinder, DownloadFileCommandHandler>()
            .Route<PostJson, PostJsonCommand, string, PostJsonCommandBinder, PostJsonCommandHandler>()
            .Route<SendEmail, SendEmailCommand, string, SendEmailCommandBinder, SendEmailCommandHandler>()
            .Route<TransformEncoding, TransformEncodingCommand, string, TransformEncodingCommandBinder, TransformEncodingCommandHandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.ClauseMarker("FROM", Prompt.PromptClauseKind.From)
            .ClauseMarker("TO", Prompt.PromptClauseKind.To)
            .ClauseMarker("USING", Prompt.PromptClauseKind.Using)
            .CommandConnector("THEN", Prompt.CommandLinkKind.Sequence);

        language.Keyword<From>("FROM")
            .Keyword<To>("TO")
            .Keyword<Using>("USING")
            .Keyword<Then>("THEN")
            .Keyword<And>("AND");

        language.Command<GetText, string[]>("GET", "Text")
            .Aliases("FETCH", "RETRIEVE")
            .Qualifiers("TEXT")
            .Positional<string[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM");

        language.Command<SaveText, string>("SAVE", "Text")
            .Qualifiers("TEXT")
            .Positional<string>(SemanticRole.Theme)
            .Marked<FileInfo>(SemanticRole.Goal, "TO");

        language.Command<LoadText, string[]>("LOAD", "Text")
            .Default()
            .Qualifiers("TEXT")
            .Positional<string[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM");

        language.Command<LoadConfig, Dictionary<string, object>>("LOAD", "Config")
            .Qualifiers("CONFIG", "JSON")
            .Positional<Dictionary<string, object>>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM");

        language.Command<DeleteFile, string>("DELETE", "File")
            .Positional<string>(SemanticRole.Theme)
            .Marked<DirectoryInfo>(SemanticRole.Source, "FROM", SlotCardinality.Optional);

        language.Command<DownloadFile, FileInfo>("DOWNLOAD", "File")
            .Aliases("PULL", "GRAB", "OBTAIN")
            .Positional<FileInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<Uri>(SemanticRole.Source, "FROM")
            .Marked<FileInfo>(SemanticRole.Goal, "TO", SlotCardinality.Optional);

        language.Command<PostJson, string>("POST", "Json")
            .Qualifiers("JSON")
            .Positional<string>(SemanticRole.Theme)
            .Marked<Uri>(SemanticRole.Goal, "TO");

        language.Command<SayText, string>("SAY", "Text")
            .Aliases("ECHO", "PRINT", "OUTPUT", "WRITE")
            .Positional<string>(SemanticRole.Theme);

        language.Command<SendEmail, string>("SEND", "Email")
            .Positional<string>(SemanticRole.Theme)
            .Marked<string>(SemanticRole.Recipient, "TO");

        language.Command<TransformEncoding, string>("TRANSFORM", "Encoding")
            .Positional<string>(SemanticRole.Theme)
            .Marked<Encoding>(SemanticRole.Instrument, "USING");
    }
}

public static class StandardLanguage
{
    public static LanguageSnapshot CreateSnapshot() =>
        new LanguageBuilder().AddModule(new StandardLanguageModule()).Build();

    public static FluNetRuntimeDefinition CreateRuntime() =>
        new FluNetModuleBuilder().AddModule(new StandardLanguageModule()).Build();
}
