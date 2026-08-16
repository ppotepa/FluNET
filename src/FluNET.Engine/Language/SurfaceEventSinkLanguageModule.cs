using FluNET.Execution.Commands;

namespace FluNET.Language;

public sealed class SurfaceEventSinkLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module.Command<EmitEventCommand, string>("EMITEVENT", "Text")
            .FrameId("events.emit.webhook")
            .CommandId("flunet.events.emit.webhook")
            .Positional<string>(SemanticRole.Theme)
            .Marked<Uri>(SemanticRole.Goal, "TO")
            .Marked<string>(new FrameRoleId("Credential"), "USING", SlotCardinality.Optional)
            .BindWith<EmitEventCommandBinder>()
            .HandleWith<EmitEventCommandHandler>();
    }
}
