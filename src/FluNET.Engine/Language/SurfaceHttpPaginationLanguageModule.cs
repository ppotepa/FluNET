using FluNET.Execution.Commands;

namespace FluNET.Language;

public sealed class SurfaceHttpPaginationLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<PaginateJsonCommand, System.Text.Json.JsonElement[]>("PAGINATEJSON", "JsonList")
            .FrameId("network.http.pagination")
            .CommandId("flunet.network.http.pagination")
            .Positional<System.Text.Json.JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<Uri>(SemanticRole.Source, "FROM")
            .Marked<string>(new FrameRoleId("Items"), "ITEMS")
            .Marked<string>(new FrameRoleId("Next"), "NEXT")
            .Marked<int>(new FrameRoleId("MaxPages"), "LIMIT")
            .Marked<string>(new FrameRoleId("Credential"), "USING", SlotCardinality.Optional)
            .BindWith<PaginateJsonCommandBinder>()
            .HandleWith<PaginateJsonCommandHandler>();
    }
}
