using FluNET.Execution.Commands;

namespace FluNET.Language;

public sealed class SurfaceWhileLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<WhileCommand, bool>("WHILELOOP", "Boolean")
            .FrameId("surface.flow.while")
            .CommandId("flunet.surface.while")
            .Positional<bool>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(new FrameRoleId("Condition"), "WHERE")
            .Marked<string>(new FrameRoleId("Template"), "FROM")
            .BindWith<WhileCommandBinder>()
            .HandleWith<WhileCommandHandler>();
    }
}
