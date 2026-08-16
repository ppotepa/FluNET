using FluNET.Execution.Commands;

namespace FluNET.Language;

public sealed class SurfaceSqlLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<ApplySqlCommand, int>("APPLYSQL", "Number")
            .FrameId("surface.apply.sql")
            .CommandId("flunet.surface.apply.sql")
            .Positional<int>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<ApplySqlCommandBinder>()
            .HandleWith<ApplySqlCommandHandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.surface.sql");
    }
}
