using FluNET.Compilation;using FluNET.Context;using FluNET.Execution.Planning;namespace FluNET.Tests.Compilation;[TestFixture]public sealed class AdvancedPolicyTests{[Test]public void PolicyCompilesBackoffJitterAndStatusMatchers(){using FluNETContext c=SurfaceCompilationExtensions.CreateSurfaceContext();const string source="""
POLICY resilient
    RETRY 4 ON 429, 502, 503
    BACKOFF EXPONENTIAL 250ms
    JITTER 20%
    CONTINUE ON 404
    FAIL ON 401
WITH resilient
    GET https://example.test/data.json AS data
""";SurfaceCompilationResult r=c.CompileSurface(source);Assert.That(r.IsValid,Is.True,string.Join(" | ",r.Lowering.Diagnostics.Select(d=>d.Message)));CommandExecutionPolicy p=r.Plan!.Steps.Single().Policy;Assert.Multiple(()=>{Assert.That(p.RetryCount,Is.EqualTo(4));Assert.That(p.Backoff!.Kind,Is.EqualTo(RetryBackoffKind.Exponential));Assert.That(p.Backoff.BaseDelay,Is.EqualTo(TimeSpan.FromMilliseconds(250)));Assert.That(p.Backoff.JitterFraction,Is.EqualTo(.2).Within(.0001));Assert.That(p.RetryOnStatusCodes,Is.EquivalentTo(new[]{429,502,503}));Assert.That(p.ContinueOnStatusCodes,Is.EquivalentTo(new[]{404}));Assert.That(p.FailOnStatusCodes,Is.EquivalentTo(new[]{401}));});}}
