using FluNET.Execution.Commands;
using FluNET.Language.Resources;

namespace FluNET.Language;

public sealed class SurfaceConfigurationLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module.ResourceProvider<ConfigurationResourceProvider>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.configuration");
        language.Command<GetConfigurationCommand, string>("GETCONFIG", "Text")
            .FrameId("surface.get.configuration")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");
    }
}

public sealed class GetConfiguration
{
    public string Text => "GET";
}
