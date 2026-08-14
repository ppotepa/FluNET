using FluNET.Compilation;using FluNET.Context;namespace FluNET.Tests.Compilation;[TestFixture]public sealed class SurfaceAuthenticationTests{[Test]public void AuthDirectiveBindsSecretNameToHttpFrameWithoutReadingSecret(){using FluNETContext c=SurfaceCompilationExtensions.CreateSurfaceContext();const string source="""
FROM https://api.example.test
    AUTH secret:api-token
    GET posts AS posts
""";SurfaceCompilationResult r=c.CompileSurface(source);Assert.That(r.IsValid,Is.True,string.Join(" | ",r.Lowering.Diagnostics.Select(d=>d.Message)));string[]tokens=r.Lowering.CanonicalSyntax.Commands.Single().AllTokens.Select(t=>t.Text).ToArray();Assert.Multiple(()=>{Assert.That(tokens,Does.Contain("USING"));Assert.That(tokens,Does.Contain("{api-token}"));Assert.That(tokens.Any(t=>t.Contains("Bearer",StringComparison.OrdinalIgnoreCase)),Is.False);});}}
