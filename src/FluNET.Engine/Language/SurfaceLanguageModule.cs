using FluNET.Execution.Commands;
using FluNET.Language.Values;
using System.Text.Json;

namespace FluNET.Language;

public sealed class SurfaceLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        ArgumentNullException.ThrowIfNull(module);
        module.Language.Module("flunet.surface");

        module.Command<LoadJsonGlobCommand, JsonElement[]>("LOADGLOB", "JsonGlob")
            .FrameId("surface.load.glob.json").CommandId("flunet.surface.loadglob")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<LoadJsonGlobCommandBinder>().HandleWith<LoadJsonGlobCommandHandler>();

        module.Command<GetHttpJsonCommand, JsonElement>("GETHTTP", "Json")
            .FrameId("surface.get.http.json").CommandId("flunet.surface.gethttp")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<Uri>(SemanticRole.Source, "FROM")
            .BindWith<GetHttpJsonCommandBinder>().HandleWith<GetHttpJsonCommandHandler>();

        module.Command<GetEnvironmentCommand, string>("GETENV", "Text")
            .FrameId("surface.get.environment").CommandId("flunet.surface.getenv")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<GetEnvironmentCommandBinder>().HandleWith<GetEnvironmentCommandHandler>();

        module.Command<FilterJsonCommand, JsonElement[]>("FILTERJSON", "JsonList")
            .FrameId("surface.data.filter.json").CommandId("flunet.surface.filterjson")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement[]>(SemanticRole.Source, "FROM")
            .Marked<string>(new FrameRoleId("Predicate"), "USING")
            .BindWith<FilterJsonCommandBinder>().HandleWith<FilterJsonCommandHandler>();

        module.Command<SortJsonCommand, JsonElement[]>("SORTJSON", "JsonList")
            .FrameId("surface.data.sort.json").CommandId("flunet.surface.sortjson")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement[]>(SemanticRole.Source, "FROM")
            .Marked<string>(new FrameRoleId("Key"), "USING")
            .BindWith<SortJsonCommandBinder>().HandleWith<SortJsonCommandHandler>();

        module.Command<TakeJsonCommand, JsonElement[]>("TAKEJSON", "JsonList")
            .FrameId("surface.data.take.json").CommandId("flunet.surface.takejson")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<JsonElement[]>(SemanticRole.Source, "FROM")
            .Marked<int>(new FrameRoleId("Count"), "USING")
            .BindWith<TakeJsonCommandBinder>().HandleWith<TakeJsonCommandHandler>();

        module.Conversion<JsonElement, JsonElement[], JsonToJsonListConversion>();
    }
}

public static class SurfaceLanguage
{
    public static FluNetRuntimeDefinition CreateRuntime() =>
        new FluNetModuleBuilder().AddModule(new StandardLanguageModule()).AddModule(new SurfaceLanguageModule()).Build();
}
