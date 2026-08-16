using FluNET.Execution.Commands;

namespace FluNET.Language;

public sealed class SurfaceTextLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Route<TrimTextCommand, string, TrimTextCommandBinder, TrimTextCommandHandler>("surface.text.trim")
            .Route<UpperTextCommand, string, UpperTextCommandBinder, UpperTextCommandHandler>("surface.text.upper")
            .Route<LowerTextCommand, string, LowerTextCommandBinder, LowerTextCommandHandler>("surface.text.lower")
            .Route<ReplaceTextCommand, string, ReplaceTextCommandBinder, ReplaceTextCommandHandler>("surface.text.replace")
            .Route<SplitTextCommand, string[], SplitTextCommandBinder, SplitTextCommandHandler>("surface.text.split")
            .Route<JoinTextCommand, string, JoinTextCommandBinder, JoinTextCommandHandler>("surface.text.join")
            .Route<LinesTextCommand, string[], LinesTextCommandBinder, LinesTextCommandHandler>("surface.text.lines")
            .Route<ExpectTextCommand, bool, ExpectTextCommandBinder, ExpectTextCommandHandler>("surface.text.expect");
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.surface.text");
        language.Command<TrimTextCommand, string>("TRIMTEXT", "Text").FrameId("surface.text.trim").Aliases("TRIM").Positional<string>(SemanticRole.Output, SlotDirection.Output).Marked<string>(SemanticRole.Source, "FROM");
        language.Command<UpperTextCommand, string>("UPPERTEXT", "Text").FrameId("surface.text.upper").Aliases("UPPER", "UPPERCASE").Positional<string>(SemanticRole.Output, SlotDirection.Output).Marked<string>(SemanticRole.Source, "FROM");
        language.Command<LowerTextCommand, string>("LOWERTEXT", "Text").FrameId("surface.text.lower").Aliases("LOWER", "LOWERCASE").Positional<string>(SemanticRole.Output, SlotDirection.Output).Marked<string>(SemanticRole.Source, "FROM");
        language.Command<ReplaceTextCommand, string>("REPLACETEXT", "Text").FrameId("surface.text.replace").Aliases("REPLACE").Positional<string>(SemanticRole.Output, SlotDirection.Output).Marked<string>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Old"), "USING").Marked<string>(new FrameRoleId("New"), "WITH");
        language.Command<SplitTextCommand, string[]>("SPLITTEXT", "TextList").FrameId("surface.text.split").Aliases("SPLIT").Positional<string[]>(SemanticRole.Output, SlotDirection.Output).Marked<string>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Separator"), "USING");
        language.Command<JoinTextCommand, string>("JOINTEXT", "Text").FrameId("surface.text.join").Aliases("COMBINE", "CONCATENATE").Positional<string>(SemanticRole.Output, SlotDirection.Output).Marked<string[]>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Separator"), "USING");
        language.Command<LinesTextCommand, string[]>("LINES", "TextList").FrameId("surface.text.lines").Positional<string[]>(SemanticRole.Output, SlotDirection.Output).Marked<string>(SemanticRole.Source, "FROM");
        language.Command<ExpectTextCommand, bool>("EXPECTTEXT", "Boolean").FrameId("surface.text.expect").Aliases("EXPECT").Positional<bool>(SemanticRole.Output, SlotDirection.Output).Marked<string>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Expected"), "USING").Marked<string>(new FrameRoleId("Operator"), "WITH");
    }
}
