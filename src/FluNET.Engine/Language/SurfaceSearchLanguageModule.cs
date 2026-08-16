using FluNET.Execution.Commands;
using System.Text.Json;

namespace FluNET.Language;

public sealed class SurfaceSearchLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<SearchFilesCommand, JsonElement[]>("SEARCHFILES", "JsonList")
            .FrameId("filesystem.search")
            .CommandId("flunet.filesystem.search")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(new FrameRoleId("Query"), "USING")
            .Marked<bool>(new FrameRoleId("Recursive"), "RECURSIVE", SlotCardinality.Optional)
            .Marked<bool>(new FrameRoleId("Regex"), "REGEX", SlotCardinality.Optional)
            .Marked<int>(new FrameRoleId("Limit"), "LIMIT", SlotCardinality.Optional)
            .BindWith<SearchFilesCommandBinder>()
            .HandleWith<SearchFilesCommandHandler>();
    }

    public void Register(LanguageBuilder language) => language.Module("flunet.filesystem.search");
}
