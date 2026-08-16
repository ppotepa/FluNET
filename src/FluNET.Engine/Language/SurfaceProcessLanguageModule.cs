using System.Text.Json;
using FluNET.Execution.Commands;

namespace FluNET.Language;

public sealed class SurfaceProcessLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<RunProcessCommand, JsonElement>("RUNPROCESS", "Json")
            .FrameId("system.process.run")
            .CommandId("flunet.system.process.run")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Theme, "USING", SlotCardinality.Optional)
            .Marked<string>(new FrameRoleId("WorkingDirectory"), "IN", SlotCardinality.Optional)
            .Marked<string>(new FrameRoleId("Environment"), "ENV", SlotCardinality.Optional)
            .BindWith<RunProcessCommandBinder>()
            .HandleWith<RunProcessCommandHandler>();

        module
            .Command<StartProcessSessionCommand, string>("STARTPROCESS", "Text")
            .FrameId("system.process.session.start")
            .CommandId("flunet.system.process.session.start")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Theme, "USING", SlotCardinality.Optional)
            .Marked<string>(new FrameRoleId("WorkingDirectory"), "IN", SlotCardinality.Optional)
            .Marked<string>(new FrameRoleId("Environment"), "ENV", SlotCardinality.Optional)
            .BindWith<StartProcessSessionCommandBinder>()
            .HandleWith<StartProcessSessionCommandHandler>();

        module
            .Command<SendProcessSessionCommand, JsonElement>("SENDPROCESS", "Json")
            .FrameId("system.process.session.send")
            .CommandId("flunet.system.process.session.send")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Theme, "USING")
            .BindWith<SendProcessSessionCommandBinder>()
            .HandleWith<SendProcessSessionCommandHandler>();

        module
            .Command<StopProcessSessionCommand, JsonElement>("STOPPROCESS", "Json")
            .FrameId("system.process.session.stop")
            .CommandId("flunet.system.process.session.stop")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<StopProcessSessionCommandBinder>()
            .HandleWith<StopProcessSessionCommandHandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.system.process");
        language.Command<RunProcessCommand, JsonElement>("EXECUTE", "Json")
            .FrameId("system.process.run")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output);
        language.Command<StartProcessSessionCommand, string>("START", "Text")
            .FrameId("system.process.session.start")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output);
        language.Command<SendProcessSessionCommand, JsonElement>("SEND", "Json")
            .FrameId("system.process.session.send")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output);
        language.Command<StopProcessSessionCommand, JsonElement>("STOP", "Json")
            .FrameId("system.process.session.stop")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output);
    }
}

public sealed class ExecuteProcess
{
    public string Text => "EXECUTE";
}

public sealed class StartProcess
{
    public string Text => "START";
}

public sealed class SendProcess
{
    public string Text => "SEND";
}

public sealed class StopProcess
{
    public string Text => "STOP";
}
