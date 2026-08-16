using FluNET.Execution.Commands;

namespace FluNET.Language;

public sealed class SurfaceMessagingLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<PublishMessageCommand, string>("PUBLISHMESSAGE", "Text")
            .FrameId("messaging.publish")
            .CommandId("flunet.messaging.publish")
            .Positional<string>(SemanticRole.Theme)
            .Marked<string>(SemanticRole.Goal, "TO")
            .BindWith<PublishMessageCommandBinder>()
            .HandleWith<PublishMessageCommandHandler>();

        module
            .Command<ReceiveMessageCommand, System.Text.Json.JsonElement>("RECEIVEMESSAGE", "Json")
            .FrameId("messaging.receive")
            .CommandId("flunet.messaging.receive")
            .Positional<System.Text.Json.JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<ReceiveMessageCommandBinder>()
            .HandleWith<ReceiveMessageCommandHandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.messaging");
        language.Command<PublishMessageCommand, string>("PUBLISH", "Text")
            .FrameId("messaging.publish")
            .Positional<string>(SemanticRole.Theme)
            .Marked<string>(SemanticRole.Goal, "TO");
        language.Command<ReceiveMessageCommand, System.Text.Json.JsonElement>("RECEIVE", "Json")
            .FrameId("messaging.receive")
            .Positional<System.Text.Json.JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");
    }
}
