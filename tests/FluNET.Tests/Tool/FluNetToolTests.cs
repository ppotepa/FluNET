using System.Globalization;

namespace FluNET.Tests.Tool;

[TestFixture]
[NonParallelizable]
public sealed class FluNetToolTests
{
    [Test]
    public async Task VersionCommandReturnsSuccessAndWritesLanguageIdentity()
    {
        CommandResult result = await InvokeAsync("version");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.StandardOutput, Does.Contain("FluNET language identity"));
            Assert.That(result.StandardError, Is.Empty);
        });
    }

    [Test]
    public async Task UnknownCommandReturnsUsageError()
    {
        CommandResult result = await InvokeAsync("does-not-exist");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(64));
            Assert.That(result.StandardError, Does.Contain("Unknown command 'does-not-exist'."));
            Assert.That(result.StandardError, Does.Contain("flunet --help"));
        });
    }

    [Test]
    public async Task CheckCommandCompilesAFileWithoutExecutingIt()
    {
        string directory = Path.Combine(Path.GetTempPath(), "flunet-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "program.flu");
        await File.WriteAllTextAsync(path, "SAY \"checked only\"");

        try
        {
            CommandResult result = await InvokeAsync("check", path);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0), result.StandardError);
                Assert.That(result.StandardOutput, Does.StartWith("Valid:"));
                Assert.That(result.StandardOutput, Does.Not.Contain("checked only"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task RunVerbosityFlagsMayAppearBeforeTheFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "flunet-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "program.flu");
        await File.WriteAllTextAsync(path, "SAY \"hello\"");

        try
        {
            CommandResult result = await InvokeAsync("run", "-v", path);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0), result.StandardError);
                Assert.That(result.StandardOutput, Does.Contain("hello"));
                Assert.That(result.StandardError, Does.Contain("[run] #"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<CommandResult> InvokeAsync(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        TextReader originalIn = Console.In;
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);
        using StringReader input = new(string.Empty);
        Console.SetOut(output);
        Console.SetError(error);
        Console.SetIn(input);
        try
        {
            int exitCode = await global::FluNetTool.RunAsync(args);
            return new CommandResult(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Console.SetIn(originalIn);
        }
    }

    private sealed record CommandResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
