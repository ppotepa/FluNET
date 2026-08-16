using System.Diagnostics;

namespace FluNET.Capabilities;

public interface IFluNetClipboard
{
    ValueTask<string?> ReadTextAsync(CancellationToken cancellationToken = default);
}

public interface IFluNetClipboardWriter
{
    ValueTask WriteTextAsync(string value, CancellationToken cancellationToken = default);
}

public sealed class DenyFluNetClipboard : IFluNetClipboard
{
    public ValueTask<string?> ReadTextAsync(CancellationToken cancellationToken = default) =>
        throw new CapabilityDeniedException("Clipboard access is not available for this FluNET host.");
}

public sealed class DenyFluNetClipboardWriter : IFluNetClipboardWriter
{
    public ValueTask WriteTextAsync(string value, CancellationToken cancellationToken = default) =>
        throw new CapabilityDeniedException("Clipboard access is not available for this FluNET host.");
}

/// <summary>
/// Reads the native desktop clipboard using direct platform tools. No shell is
/// invoked; hosts without a desktop clipboard receive null rather than a crash.
/// </summary>
public sealed class SystemFluNetClipboard : IFluNetClipboard, IFluNetClipboardWriter
{
    public bool IsAvailable => FindCommand() is not null;

    public async ValueTask<string?> ReadTextAsync(CancellationToken cancellationToken = default)
    {
        (string fileName, string[] arguments)? command = FindCommand();
        if (command is null) return null;

        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = command.Value.fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in command.Value.arguments)
            process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) return null;

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode == 0 ? output.TrimEnd('\r', '\n') : null;
    }

    public async ValueTask WriteTextAsync(string value, CancellationToken cancellationToken = default)
    {
        (string fileName, string[] arguments)? command = FindWriteCommand();
        if (command is null)
            throw new CapabilityUnavailableException("The host does not expose a writable text clipboard.");

        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = command.Value.fileName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in command.Value.arguments)
            process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start())
            throw new InvalidOperationException("The clipboard process could not be started.");

        await process.StandardInput.WriteAsync(value.AsMemory(), cancellationToken).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        process.StandardInput.Close();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new CapabilityUnavailableException("The host clipboard rejected the text.");
    }

    private static (string fileName, string[] arguments)? FindCommand()
    {
        if (OperatingSystem.IsWindows())
            return ("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", "Get-Clipboard -Raw"]);
        if (OperatingSystem.IsMacOS()) return ("pbpaste", []);
        if (OperatingSystem.IsLinux())
        {
            if (File.Exists("/usr/bin/wl-paste") || File.Exists("/bin/wl-paste")) return ("wl-paste", ["--no-newline"]);
            if (File.Exists("/usr/bin/xclip") || File.Exists("/bin/xclip")) return ("xclip", ["-selection", "clipboard", "-out"]);
            if (File.Exists("/usr/bin/xsel") || File.Exists("/bin/xsel")) return ("xsel", ["--clipboard", "--output"]);
        }
        return null;
    }

    private static (string fileName, string[] arguments)? FindWriteCommand()
    {
        if (OperatingSystem.IsWindows())
            return ("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", "$input | Set-Clipboard"]);
        if (OperatingSystem.IsMacOS()) return ("pbcopy", []);
        if (OperatingSystem.IsLinux())
        {
            if (File.Exists("/usr/bin/wl-copy") || File.Exists("/bin/wl-copy")) return ("wl-copy", []);
            if (File.Exists("/usr/bin/xclip") || File.Exists("/bin/xclip")) return ("xclip", ["-selection", "clipboard"]);
            if (File.Exists("/usr/bin/xsel") || File.Exists("/bin/xsel")) return ("xsel", ["--clipboard", "--input"]);
        }
        return null;
    }
}

public sealed class ClipboardCapabilityProvider(IFluNetClipboard clipboard) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.clipboard",
        "1.0",
        [FluNetPlatform.Windows, FluNetPlatform.MacOS, FluNetPlatform.Linux],
        ["system.clipboard.read", "system.clipboard.write"]);

    public bool IsAvailable => clipboard switch
    {
        DenyFluNetClipboard => false,
        SystemFluNetClipboard system => system.IsAvailable,
        _ => true
    };
}
