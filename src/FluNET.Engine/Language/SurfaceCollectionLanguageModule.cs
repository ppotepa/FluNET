using FluNET.Execution.Commands;
using System.Text.Json;

namespace FluNET.Language;

public sealed class SurfaceCollectionLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<DistinctJsonCommand, JsonElement[]>("DISTINCTJSON", "JsonList")
            .FrameId("surface.data.distinct.json")
            .CommandId("flunet.surface.distinctjson")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement[]>(SemanticRole.Source, "FROM")
            .Marked<string>(new FrameRoleId("Key"), "USING", SlotCardinality.Optional)
            .BindWith<DistinctJsonCommandBinder>()
            .HandleWith<DistinctJsonCommandHandler>();

        module
            .Command<SkipJsonCommand, JsonElement[]>("SKIPJSON", "JsonList")
            .FrameId("surface.data.skip.json")
            .CommandId("flunet.surface.skipjson")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement[]>(SemanticRole.Source, "FROM")
            .Marked<int>(new FrameRoleId("Count"), "USING")
            .BindWith<SkipJsonCommandBinder>()
            .HandleWith<SkipJsonCommandHandler>();

        module
            .Command<ScanFilesJsonCommand, JsonElement[]>("SCANFILES", "JsonList")
            .FrameId("surface.files.scan.json")
            .CommandId("flunet.surface.scanfiles")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<int>(new FrameRoleId("Limit"), "LIMIT", SlotCardinality.Optional)
            .BindWith<ScanFilesJsonCommandBinder>()
            .HandleWith<ScanFilesJsonCommandHandler>();

        module
            .Command<ListDirectoryJsonCommand, JsonElement[]>("LISTFILES", "JsonList")
            .FrameId("surface.files.list.json")
            .CommandId("flunet.surface.listfiles")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<bool>(new FrameRoleId("Recursive"), "USING", SlotCardinality.Optional)
            .BindWith<ListDirectoryJsonCommandBinder>()
            .HandleWith<ListDirectoryJsonCommandHandler>();

        module
            .Command<HashFileCommand, string>("HASHFILE", "Text")
            .FrameId("surface.files.hash")
            .CommandId("flunet.surface.hashfile")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<HashFileCommandBinder>()
            .HandleWith<HashFileCommandHandler>();

        module
            .Command<CopyFileCommand, FileInfo>("COPYFILE", "File")
            .FrameId("surface.files.copy")
            .CommandId("flunet.surface.copyfile")
            .Positional<FileInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM")
            .Marked<FileInfo>(SemanticRole.Goal, "TO")
            .BindWith<CopyFileCommandBinder>()
            .HandleWith<CopyFileCommandHandler>();

        module
            .Command<MoveFileCommand, FileInfo>("MOVEFILE", "File")
            .FrameId("surface.files.move")
            .CommandId("flunet.surface.movefile")
            .Positional<FileInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM")
            .Marked<FileInfo>(SemanticRole.Goal, "TO")
            .BindWith<MoveFileCommandBinder>()
            .HandleWith<MoveFileCommandHandler>();

        module
            .Command<TrashFileCommand, FileInfo>("TRASHFILE", "File")
            .FrameId("surface.files.trash")
            .CommandId("flunet.surface.trashfile")
            .Positional<FileInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<FileInfo>(SemanticRole.Source, "FROM")
            .BindWith<TrashFileCommandBinder>()
            .HandleWith<TrashFileCommandHandler>();

        module
            .Command<SaveJsonCommand, string>("SAVEJSON", "Json")
            .FrameId("core.save.json")
            .CommandId("flunet.core.save.json")
            .Positional<JsonElement[]>(SemanticRole.Theme)
            .Marked<FileInfo>(SemanticRole.Goal, "TO")
            .BindWith<SaveJsonCommandBinder>()
            .HandleWith<SaveJsonCommandHandler>();

        module
            .Command<SaveCsvCommand, string>("SAVECSV", "Csv")
            .FrameId("core.save.csv")
            .CommandId("flunet.core.save.csv")
            .Positional<JsonElement[]>(SemanticRole.Theme)
            .Marked<FileInfo>(SemanticRole.Goal, "TO")
            .BindWith<SaveCsvCommandBinder>()
            .HandleWith<SaveCsvCommandHandler>();

        RegisterAggregate<AggregateJsonCommandBinder, AggregateJsonCommandHandler>(module, "COUNTJSON", "surface.data.count.json", "flunet.surface.countjson");
        RegisterAggregate<AggregateJsonCommandBinder, AggregateJsonCommandHandler>(module, "AVGJSON", "surface.data.avg.json", "flunet.surface.avgjson");
        RegisterAggregate<AggregateJsonCommandBinder, AggregateJsonCommandHandler>(module, "MINJSON", "surface.data.min.json", "flunet.surface.minjson");
        RegisterAggregate<AggregateJsonCommandBinder, AggregateJsonCommandHandler>(module, "MAXJSON", "surface.data.max.json", "flunet.surface.maxjson");
    }

    private static void RegisterAggregate<TBinder, THandler>(FluNetModuleBuilder module, string name, string frame, string commandId)
        where TBinder : class, ICommandBinder<AggregateJsonCommand, decimal>
        where THandler : class, ICommandHandler<AggregateJsonCommand, decimal>
    {
        module
            .Command<AggregateJsonCommand, decimal>(name, "Number")
            .FrameId(frame)
            .CommandId(commandId)
            .Positional<decimal>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement[]>(SemanticRole.Source, "FROM")
            .Marked<string>(new FrameRoleId("Value"), "USING", SlotCardinality.Optional)
            .BindWith<TBinder>()
            .HandleWith<THandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.surface.collections");
        language.Command<DistinctJsonCommand, JsonElement[]>("DISTINCT", "JsonList")
            .FrameId("surface.data.distinct.json")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement[]>(SemanticRole.Source, "FROM")
            .Marked<string>(new FrameRoleId("Key"), "USING", SlotCardinality.Optional);
        language.Command<SkipJsonCommand, JsonElement[]>("SKIP", "JsonList")
            .FrameId("surface.data.skip.json")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement[]>(SemanticRole.Source, "FROM")
            .Marked<int>(new FrameRoleId("Count"), "USING");
        language.Command<ScanFilesJsonCommand, JsonElement[]>("SCAN", "JsonList")
            .FrameId("surface.files.scan.json")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");
        language.Command<SearchFilesCommand, JsonElement[]>("FIND", "JsonList")
            .FrameId("surface.files.scan.json")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");
        language.Command<ListDirectoryJsonCommand, JsonElement[]>("LIST", "JsonList")
            .FrameId("surface.files.list.json")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<bool>(new FrameRoleId("Recursive"), "USING", SlotCardinality.Optional);
        language.Command<HashFileCommand, string>("HASH", "Text")
            .FrameId("surface.files.hash")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");
    }
}
