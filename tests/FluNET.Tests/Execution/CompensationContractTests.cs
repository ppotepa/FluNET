using FluNET.Context;
using FluNET.Execution.Compensation;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class CompensationContractTests
{
    [Test]
    public void SaveMayOptIntoCompensationButPostMayNot()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        CompensatableCompilationResult save = context.CompileCompensatableSurface(
            "LOAD input.txt AS value; SAVE value TO output.txt COMPENSATE");
        CompensatableCompilationResult post = context.CompileCompensatableSurface(
            "LOAD data.json AS value; POST value TO https://example.test/items COMPENSATE");

        Assert.Multiple(() =>
        {
            Assert.That(save.Diagnostics, Is.Empty);
            Assert.That(save.CompensationSteps, Has.Count.EqualTo(1));
            Assert.That(post.Diagnostics.Any(item => item.Code == "FLN360"), Is.True);
        });
    }
}
