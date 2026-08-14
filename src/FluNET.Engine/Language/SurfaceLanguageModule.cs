using FluNET.Execution.Commands;
using System.Text.Json;

namespace FluNET.Language;

/// <summary>
/// Generated canonical operations used only as lowering targets for compact
/// surface syntax. They still use the normal typed command route/executor.
/// </summary>
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
