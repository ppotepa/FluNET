using FluNET.Execution.Commands;
using FluNET.Syntax.Validation;
using System.Text.Json;

namespace FluNET.Language;

public sealed class SurfaceSystemLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<SystemInfoCommand, JsonElement>("SYSTEMINFO", "Json")
            .FrameId("surface.system.info")
            .CommandId("flunet.surface.systeminfo")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .BindWith<SystemInfoCommandBinder>()
            .HandleWith<SystemInfoCommandHandler>();

        module
            .Command<SystemMetricsCommand, JsonElement>("SYSTEMMETRICS", "Json")
            .FrameId("surface.system.metrics")
            .CommandId("flunet.surface.systemmetrics")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .BindWith<SystemMetricsCommandBinder>()
            .HandleWith<SystemMetricsCommandHandler>();

        module
            .Command<CapabilitySnapshotCommand, JsonElement[]>("CAPABILITIES", "JsonList")
            .FrameId("surface.system.capabilities")
            .CommandId("flunet.surface.capabilities")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .BindWith<CapabilitySnapshotCommandBinder>()
            .HandleWith<CapabilitySnapshotCommandHandler>();

        module
            .Command<ProviderPackageSnapshotCommand, JsonElement[]>("PACKAGES", "JsonList")
            .FrameId("surface.system.packages")
            .CommandId("flunet.surface.packages")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .BindWith<ProviderPackageSnapshotCommandBinder>()
            .HandleWith<ProviderPackageSnapshotCommandHandler>();

        module
            .Command<HostDoctorCommand, JsonElement>("DOCTOR", "Json")
            .FrameId("surface.system.doctor")
            .CommandId("flunet.surface.doctor")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .BindWith<HostDoctorCommandBinder>()
            .HandleWith<HostDoctorCommandHandler>();

        module
            .Command<ResolvePathCommand, string>("PATHVALUE", "Text")
            .FrameId("surface.system.path")
            .CommandId("flunet.surface.path")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<ResolvePathCommandBinder>()
            .HandleWith<ResolvePathCommandHandler>();

        module
            .Command<NowCommand, string>("NOW", "Text")
            .FrameId("surface.system.now")
            .CommandId("flunet.surface.now")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .BindWith<NowCommandBinder>()
            .HandleWith<NowCommandHandler>();

        module
            .Command<WaitCommand, string>("WAIT", "Text")
            .FrameId("surface.system.wait")
            .CommandId("flunet.surface.wait")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<WaitCommandBinder>()
            .HandleWith<WaitCommandHandler>();

        module
            .Command<NotifyCommand, string>("NOTIFYTEXT", "Text")
            .FrameId("surface.system.notify")
            .CommandId("flunet.surface.notify")
            .Positional<string>(SemanticRole.Theme)
            .BindWith<NotifyCommandBinder>()
            .HandleWith<NotifyCommandHandler>();

        module
            .Command<ReadClipboardCommand, string>("READCLIPBOARD", "Text")
            .FrameId("surface.system.clipboard.read")
            .CommandId("flunet.surface.clipboard.read")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .BindWith<ReadClipboardCommandBinder>()
            .HandleWith<ReadClipboardCommandHandler>();

        module
            .Command<WriteClipboardCommand, string>("WRITECLIPBOARD", "Text")
            .FrameId("surface.system.clipboard.write")
            .CommandId("flunet.surface.clipboard.write")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<WriteClipboardCommandBinder>()
            .HandleWith<WriteClipboardCommandHandler>();

        module
            .Command<SetEnvironmentCommand, string>("SETENV", "Text")
            .FrameId("surface.system.environment.write")
            .CommandId("flunet.surface.environment.write")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Goal, "TO")
            .BindWith<SetEnvironmentCommandBinder>()
            .HandleWith<SetEnvironmentCommandHandler>();

        module
            .Command<CreateTemporaryArtifactCommand, string>("CREATETEMPFILE", "Text")
            .FrameId("surface.system.temp.file")
            .CommandId("flunet.surface.temp.file")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM", SlotCardinality.Optional)
            .BindWith<CreateTemporaryArtifactCommandBinder>()
            .HandleWith<CreateTemporaryArtifactCommandHandler>();

        module
            .Command<CreateTemporaryArtifactCommand, string>("CREATETEMPDIR", "Text")
            .FrameId("surface.system.temp.directory")
            .CommandId("flunet.surface.temp.directory")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .BindWith<CreateTemporaryArtifactCommandBinder>()
            .HandleWith<CreateTemporaryArtifactCommandHandler>();

        module
            .Command<CleanupTemporaryArtifactCommand, string>("CLEANUPTEMP", "Text")
            .FrameId("surface.system.temp.cleanup")
            .CommandId("flunet.surface.temp.cleanup")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<CleanupTemporaryArtifactCommandBinder>()
            .HandleWith<CleanupTemporaryArtifactCommandHandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.surface.system");
        language.Command<SystemInfoCommand, JsonElement>("SYSTEM", "Json")
            .FrameId("surface.system.info")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output);
        language.Command<SystemMetricsCommand, JsonElement>("METRICS", "Json")
            .FrameId("surface.system.metrics")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output);
        language.Command<CapabilitySnapshotCommand, JsonElement[]>("CAPABILITIES", "JsonList")
            .FrameId("surface.system.capabilities")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output);
        language.Command<ProviderPackageSnapshotCommand, JsonElement[]>("PACKAGES", "JsonList")
            .FrameId("surface.system.packages")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output);
        language.Command<HostDoctorCommand, JsonElement>("DOCTOR", "Json")
            .FrameId("surface.system.doctor")
            .Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output);
        language.Command<ResolvePathCommand, string>("PATH", "Text")
            .FrameId("surface.system.path")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");
        language.Command<NotifyCommand, string>("NOTIFY", "Text")
            .FrameId("surface.system.notify")
            .Positional<string>(SemanticRole.Theme);
        language.Command<NowCommand, string>("NOW", "Text")
            .FrameId("surface.system.now")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output);
        language.Command<WaitCommand, string>("WAIT", "Text")
            .FrameId("surface.system.wait")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");
        language.Command<ReadClipboardCommand, string>("CLIPBOARD", "Text")
            .FrameId("surface.system.clipboard.read")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output);
        language.Command<CreateTemporaryArtifactCommand, string>("TEMP", "Text")
            .FrameId("surface.system.temp.file")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output);
        language.Command<CleanupTemporaryArtifactCommand, string>("CLEANUP", "Text")
            .FrameId("surface.system.temp.cleanup")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");
    }
}

public sealed class SystemInfo
{
    public string Text => "SYSTEM";
}

public sealed class CapabilitySnapshot
{
    public string Text => "CAPABILITIES";
}

public sealed class ProviderPackageSnapshot
{
    public string Text => "PACKAGES";
}

public sealed class HostDoctor
{
    public string Text => "DOCTOR";
}

public sealed class PathValue
{
    public string Text => "PATH";
}

public sealed class ReadClipboard
{
    public string Text => "CLIPBOARD";
}

public sealed class Now
{
    public string Text => "NOW";
}

public sealed class Wait
{
    public string Text => "WAIT";
}

public sealed class TemporaryArtifact
{
    public string Text => "TEMP";
}

public sealed class Cleanup
{
    public string Text => "CLEANUP";
}
