using FluNET.Execution.Commands;
using System.Text.Json;

namespace FluNET.Language;

/// <summary>Generated canonical operations used only as compact-syntax lowering targets.</summary>
public sealed class SurfaceLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        ArgumentNullException.ThrowIfNull(module);
        module.Language.Module("flunet.surface");

        module.Command<LoadJsonGlobCommand, JsonElement[]>("LOADGLOB", "JsonGlob")
            .FrameId("surface.load.glob.json")
            .CommandId("flunet.surface.loadglob")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<LoadJsonGlobCommandBinder>()
            .HandleWith<LoadJsonGlobCommandHandler>();

        module.Command<GetHttpJsonCommand, JsonElement>("GETHTTP", "Json")
            .FrameId("surface.get.http.json")
            .CommandId("flunet.surface.gethttp")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<Uri>(SemanticRole.Source, "FROM")
            .BindWith<GetHttpJsonCommandBinder>()
            .HandleWith<GetHttpJsonCommandHandler>();

        module.Command<GetEnvironmentCommand, string>("GETENV", "Text")
            .FrameId("surface.get.environment")
            .CommandId("flunet.surface.getenv")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<GetEnvironmentCommandBinder>()
            .HandleWith<GetEnvironmentCommandHandler>();
    }
}

public static class SurfaceLanguage
{
    public static FluNetRuntimeDefinition CreateRuntime() =>
        new FluNetModuleBuilder()
            .AddModule(new StandardLanguageModule())
            .AddModule(new SurfaceLanguageModule())
            .Build();
}
