using System.Runtime.InteropServices;

namespace FluNET.Capabilities;

public sealed record FluNetSystemInfo(
    string OperatingSystem,
    string Architecture,
    string Framework,
    string Machine,
    string User,
    string CurrentDirectory,
    string TempDirectory,
    string HomeDirectory,
    int ProcessId,
    long WorkingSetBytes);

public interface IFluNetSystemInfoProvider
{
    FluNetSystemInfo Read();
}

public sealed class PhysicalFluNetSystemInfoProvider : IFluNetSystemInfoProvider
{
    public FluNetSystemInfo Read() => new(
        RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture.ToString(),
        RuntimeInformation.FrameworkDescription,
        Environment.MachineName,
        Environment.UserName,
        Environment.CurrentDirectory,
        Path.GetTempPath(),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.ProcessId,
        Environment.WorkingSet);
}

public sealed class SystemInfoCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.info",
        "1.0",
        [FluNetPlatform.Any],
        ["system.read"]);

    public bool IsAvailable => true;
}
