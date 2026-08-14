using FluNET.Context;
using FluNET.Execution.Compensation;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class SagaExecutionTests
{
    [Test]
    public async Task LaterFailureCompensatesEarlierSuccessfulSave()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FluNET_Saga_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "input.txt");
        string output = Path.Combine(directory, "output.txt");
        string missing = Path.Combine(directory, "missing.txt");
        await File.WriteAllTextAsync(input, "new-value");
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SagaPlan plan = context.GetSagaCompiler().Compile(
                ("write", $"LOAD {input} AS value; SAVE value TO {output} COMPENSATE"),
                ("fail", $"GET {missing} AS missing"));

            SagaExecutionResult result = await context.GetSagaExecutor().ExecuteAsync(plan);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.WasCompensated, Is.True);
                Assert.That(File.Exists(output), Is.False);
            });
        }
        finally { Directory.Delete(directory, true); }
    }
}
