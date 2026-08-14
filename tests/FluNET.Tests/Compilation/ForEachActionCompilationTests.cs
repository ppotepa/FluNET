using FluNET.Compilation;using FluNET.Context;namespace FluNET.Tests.Compilation;[TestFixture]public sealed class ForEachActionCompilationTests{[Test]public void ExplicitInCollectionCompilesEffectfulNestedActions(){using FluNETContext c=SurfaceCompilationExtensions.CreateSurfaceContext();const string source="""
GET https://api.example.test/users AS users
FOR EACH user IN users PARALLEL 8
    GET https://api.example.test/profiles/{user.id} AS profile
    SAVE profile TO profiles/{user.id}.json
    SAY "processed {user.id}"
""";SurfaceCompilationResult r=c.CompileSurface(source);Assert.That(r.IsValid,Is.True,string.Join(" | ",r.Lowering.Diagnostics.Select(d=>d.Code+":"+d.Message)));var command=r.BoundProgram!.Commands.Last();Assert.Multiple(()=>{Assert.That(command.Frame.Id.Value,Is.EqualTo("surface.flow.foreach.json"));Assert.That(command[FluNET.Language.SemanticRole.Source].Tokens.Any(t=>t.Text.Contains("users",StringComparison.OrdinalIgnoreCase)),Is.True);});}}
