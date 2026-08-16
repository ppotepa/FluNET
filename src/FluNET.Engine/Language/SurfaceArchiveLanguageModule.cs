using FluNET.Execution.Commands;

namespace FluNET.Language;

public sealed class SurfaceArchiveLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<CreateArchiveCommand, string>("CREATEARCHIVE", "Text")
            .FrameId("filesystem.archive.create")
            .CommandId("flunet.filesystem.archive.create")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Goal, "TO")
            .BindWith<CreateArchiveCommandBinder>()
            .HandleWith<CreateArchiveCommandHandler>();

        module
            .Command<ExtractArchiveCommand, string>("EXTRACTARCHIVE", "Text")
            .FrameId("filesystem.archive.extract")
            .CommandId("flunet.filesystem.archive.extract")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Goal, "TO")
            .BindWith<ExtractArchiveCommandBinder>()
            .HandleWith<ExtractArchiveCommandHandler>();

        module
            .Command<ListArchiveCommand, System.Text.Json.JsonElement[]>("LISTARCHIVE", "JsonList")
            .FrameId("filesystem.archive.list")
            .CommandId("flunet.filesystem.archive.list")
            .Positional<System.Text.Json.JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<ListArchiveCommandBinder>()
            .HandleWith<ListArchiveCommandHandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.filesystem.archive");
        language.Command<CreateArchiveCommand, string>("PACK", "Text")
            .FrameId("filesystem.archive.create")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output);
        language.Command<ExtractArchiveCommand, string>("UNPACK", "Text")
            .FrameId("filesystem.archive.extract")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output);
    }
}

public sealed class PackArchive
{
    public string Text => "PACK";
}

public sealed class UnpackArchive
{
    public string Text => "UNPACK";
}
