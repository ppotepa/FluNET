using FluNET.Execution.Commands;
using System.Text.Json;

namespace FluNET.Language;

public sealed class SurfaceHttpResponseLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<RequestJsonCommand, JsonElement>("REQUESTJSON", "Json")
            .FrameId("network.http.response")
            .CommandId("flunet.network.http.response")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<Uri>(SemanticRole.Source, "FROM")
            .Marked<string>(new FrameRoleId("Credential"), "USING", SlotCardinality.Optional)
            .BindWith<RequestJsonCommandBinder>()
            .HandleWith<RequestJsonCommandHandler>();
    }
}
