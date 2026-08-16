using FluNET.Execution.Commands;
using System.Text.Json;

namespace FluNET.Language;

public sealed class SurfaceIndexLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module.Command<IndexFilesCommand, JsonElement[]>("INDEX", "JsonList")
            .FrameId("surface.files.index")
            .CommandId("flunet.filesystem.index")
            .Qualifiers("FILES")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<bool>(new FrameRoleId("Recursive"), "RECURSIVE", SlotCardinality.Optional)
            .BindWith<IndexFilesCommandBinder>()
            .HandleWith<IndexFilesCommandHandler>();

        module.Command<ReadIndexFilesCommand, JsonElement[]>("READ", "JsonList")
            .FrameId("surface.files.index.read")
            .CommandId("flunet.filesystem.index.read")
            .Qualifiers("INDEX")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(new FrameRoleId("Predicate"), "WHERE", SlotCardinality.Optional)
            .Marked<string>(new FrameRoleId("OrderBy"), "ORDER BY", SlotCardinality.Optional)
            .Marked<int>(new FrameRoleId("Take"), "TAKE", SlotCardinality.Optional)
            .Marked<int>(new FrameRoleId("Skip"), "SKIP", SlotCardinality.Optional)
            .BindWith<ReadIndexFilesCommandBinder>()
            .HandleWith<ReadIndexFilesCommandHandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.filesystem.index");
        language.Command<IndexFilesCommand, JsonElement[]>("INDEX", "JsonList")
            .FrameId("surface.files.index")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<bool>(new FrameRoleId("Recursive"), "RECURSIVE", SlotCardinality.Optional);
        language.Command<ReadIndexFilesCommand, JsonElement[]>("READ", "JsonList")
            .FrameId("surface.files.index.read")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(new FrameRoleId("Predicate"), "WHERE", SlotCardinality.Optional)
            .Marked<string>(new FrameRoleId("OrderBy"), "ORDER BY", SlotCardinality.Optional)
            .Marked<int>(new FrameRoleId("Take"), "TAKE", SlotCardinality.Optional)
            .Marked<int>(new FrameRoleId("Skip"), "SKIP", SlotCardinality.Optional);
    }
}

public sealed class IndexFiles
{
    public string Text => "INDEX";
}

public sealed class ReadIndexFiles
{
    public string Text => "READ";
}
