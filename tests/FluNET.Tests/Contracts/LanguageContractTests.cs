using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Workflow;
using FluNET.Language;
using FluNET.Prompt;

namespace FluNET.Tests.Contracts;

[TestFixture]
public sealed class LanguageContractTests
{
    [Test]
    public void StandardLanguage_PublicSurface_MatchesGoldenContract()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();

        string actual = string.Join('\n', language.Commands
            .OrderBy(command => command.Id.Value, StringComparer.Ordinal)
            .SelectMany(command => command.Frames
                .OrderBy(frame => frame.Id.Value, StringComparer.Ordinal)
                .Select(frame => SnapshotFrame(command, frame))));

        const string expected = """
flunet.core.delete|DELETE|aliases=-|core.delete.file|File|result=Text|default=False|qualifiers=-|THEME:$:Text:Input:Required;SOURCE:FROM:Directory:Input:Optional
flunet.core.download|DOWNLOAD|aliases=GRAB,OBTAIN,PULL|core.download.file|File|result=File|default=False|qualifiers=-|OUTPUT:$:File:Output:Required;SOURCE:FROM:Uri:Input:Required;GOAL:TO:File:Input:Optional
flunet.core.format|FORMAT|aliases=-|core.format.json|Json|result=Text|default=False|qualifiers=JSON|OUTPUT:$:Text:Output:Required;SOURCE:FROM:Json:Input:Required
flunet.core.get|GET|aliases=FETCH,RETRIEVE|core.get.text|Text|result=List<Text>|default=False|qualifiers=TEXT|OUTPUT:$:List<Text>:Output:Required;SOURCE:FROM:File:Input:Required
flunet.core.load|LOAD|aliases=-|core.load.config|Config|result=Object|default=False|qualifiers=CONFIG,JSON|OUTPUT:$:Object:Output:Required;SOURCE:FROM:File:Input:Required
flunet.core.load|LOAD|aliases=-|core.load.text|Text|result=List<Text>|default=True|qualifiers=TEXT|OUTPUT:$:List<Text>:Output:Required;SOURCE:FROM:File:Input:Required
flunet.core.parse|PARSE|aliases=-|core.parse.json|Json|result=Json|default=False|qualifiers=JSON|OUTPUT:$:Json:Output:Required;SOURCE:FROM:Text:Input:Required
flunet.core.post|POST|aliases=-|core.post.json|Json|result=Text|default=False|qualifiers=JSON|THEME:$:Text:Input:Required;GOAL:TO:Uri:Input:Required
flunet.core.save|SAVE|aliases=-|core.save.text|Text|result=Text|default=False|qualifiers=TEXT|THEME:$:Text:Input:Required;GOAL:TO:File:Input:Required
flunet.core.say|SAY|aliases=ECHO,OUTPUT,PRINT,WRITE|core.say.text|Text|result=Text|default=False|qualifiers=-|THEME:$:Text:Input:Required
flunet.core.send|SEND|aliases=-|core.send.email|Email|result=Text|default=False|qualifiers=-|THEME:$:Text:Input:Required;RECIPIENT:TO:Text:Input:Required
flunet.core.set|SET|aliases=-|core.set.boolean|Boolean|result=Boolean|default=False|qualifiers=BOOL,BOOLEAN|OUTPUT:$:Boolean:Output:Required;THEME:TO:Boolean:Input:Required
flunet.core.set|SET|aliases=-|core.set.json|Json|result=Json|default=False|qualifiers=JSON|OUTPUT:$:Json:Output:Required;THEME:TO:Json:Input:Required
flunet.core.set|SET|aliases=-|core.set.number|Number|result=Number|default=False|qualifiers=NUMBER|OUTPUT:$:Number:Output:Required;THEME:TO:Number:Input:Required
flunet.core.set|SET|aliases=-|core.set.text|Text|result=Text|default=True|qualifiers=TEXT|OUTPUT:$:Text:Output:Required;THEME:TO:Text:Input:Repeated
flunet.core.transform|TRANSFORM|aliases=-|core.transform.encoding|Encoding|result=Text|default=False|qualifiers=-|THEME:$:Text:Input:Required;INSTRUMENT:USING:System.Text.Encoding:Input:Required
""";

        Assert.Multiple(() =>
        {
            Assert.That(language.Version, Is.EqualTo(new LanguageVersion("0.3")));
            Assert.That(actual, Is.EqualTo(expected.Replace("\r\n", "\n")));
        });
    }

    [Test]
    public void Parser_CommandClausesAndLinks_MatchGoldenContract()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        ProcessedPrompt prompt = new ProcessedPrompt(
            "GET [left] FROM {a.txt} AND GET [right] FROM {b.txt} THEN SAY [left] [right].")
            .WithGrammar(language.Grammar);

        string commands = string.Join('\n', prompt.Syntax.Commands.Select((command, index) =>
            $"{index}|{command.Verb.Text}|" + string.Join(';', command.Clauses.Select(clause =>
                $"{clause.Kind}={string.Join(',', clause.Values.Select(value => value.Text))}"))));
        string links = string.Join('\n', prompt.Syntax.Links.Select(link =>
            $"{link.PredecessorIndex}>{link.SuccessorIndex}|{link.Kind}|{link.Connector.Text}"));

        const string expectedCommands = """
0|GET|Subject=[left];From={a.txt}
1|GET|Subject=[right];From={b.txt}
2|SAY|Subject=[left],[right]
""";
        const string expectedLinks = """
0>1|Parallel|AND
1>2|Sequence|THEN
""";

        Assert.Multiple(() =>
        {
            Assert.That(prompt.IsValid, Is.True);
            Assert.That(commands, Is.EqualTo(expectedCommands.Replace("\r\n", "\n")));
            Assert.That(links, Is.EqualTo(expectedLinks.Replace("\r\n", "\n")));
        });
    }

    [TestCase("GET [text] FROM {input.txt} THEN SAVE [text] TO {copy.txt}.")]
    [TestCase("SAY \"Hello from FluNET!\"")]
    [TestCase("DOWNLOAD [file] FROM {https://example.com/file.txt} TO {file.txt}.")]
    [TestCase("SET BOOLEAN [enabled] TO true THEN SAY enabled IF [enabled] ELSE SAY disabled.")]
    [TestCase("SET JSON [config] TO {\"enabled\":true} THEN FORMAT JSON [pretty] FROM [config].")]
    [TestCase("GET [left] FROM {a.txt} AND GET [right] FROM {b.txt} THEN SAY [left] [right].")]
    public void ReadmeScenario_CompilesWithoutEffects(string source)
    {
        using FluNETContext context = FluNETContext.Create();

        CompilationResult result = context.GetEngine().Analyze(new ProcessedPrompt(source));

        Assert.That(result.IsCompilationSuccessful, Is.True,
            string.Join(" | ", result.DiagnosticBag.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}")));
    }

    [TestCase("LOAD [text] FROM {input.txt}.", "core.load.text")]
    [TestCase("LOAD TEXT [text] FROM {input.txt}.", "core.load.text")]
    [TestCase("LOAD [config] FROM {config.json}.", "core.load.config")]
    [TestCase("LOAD CONFIG [config] FROM {config.json}.", "core.load.config")]
    public void LoadCompatibility_SelectsStableFrame(string source, string expectedFrameId)
    {
        using FluNETContext context = FluNETContext.Create();

        CompilationResult result = context.GetEngine().Analyze(new ProcessedPrompt(source));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompilationSuccessful, Is.True);
            Assert.That(result.BoundCommands, Has.Count.EqualTo(1));
            Assert.That(result.BoundCommands[0].Frame.Id, Is.EqualTo(new FrameId(expectedFrameId)));
        });
    }

    [TestCase("SAY [broken.", "FLN003", CompilationPhase.Parse)]
    [TestCase("MISSING value.", CompilationDiagnosticCodes.BindingFailure, CompilationPhase.Bind)]
    [TestCase("SAY hello FROM {input.txt}.", CompilationDiagnosticCodes.UnknownMarker, CompilationPhase.Validate)]
    [TestCase("DOWNLOAD [file] TO {output.txt}.", CompilationDiagnosticCodes.MissingRequiredRole, CompilationPhase.Validate)]
    public void Diagnostics_KeepStableCodesAndPhases(
        string source,
        string expectedCode,
        CompilationPhase expectedPhase)
    {
        using FluNETContext context = FluNETContext.Create();

        CompilationResult result = context.GetEngine().Analyze(new ProcessedPrompt(source));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompilationSuccessful, Is.False);
            Assert.That(result.FailedPhase, Is.EqualTo(expectedPhase));
            Assert.That(result.DiagnosticBag.Select(diagnostic => diagnostic.Code),
                Does.Contain(expectedCode));
        });
    }

    [Test]
    public void WorkflowValueSerialization_RoundTripsPublicBuiltInTypes()
    {
        JsonWorkflowValueSerializer serializer = new();
        string path = Path.GetFullPath("contract.txt");
        object[] values =
        [
            "text",
            new[] { "left", "right" },
            true,
            12.5m,
            new Uri("https://example.com/value"),
            new FileInfo(path)
        ];

        foreach (object value in values)
        {
            Type type = value.GetType();
            string? json = serializer.Serialize(value, type);
            object? restored = serializer.Deserialize(json, type);

            if (value is FileInfo expectedFile)
            {
                Assert.That(((FileInfo)restored!).FullName, Is.EqualTo(expectedFile.FullName));
            }
            else
            {
                Assert.That(restored, Is.EqualTo(value));
            }
        }
    }

    private static string SnapshotFrame(
        CommandDescriptor command,
        CommandFrameDescriptor frame)
    {
        string aliases = JoinOrDash(command.Aliases);
        string qualifiers = JoinOrDash(frame.Qualifiers);
        string slots = string.Join(';', frame.Slots.Select(slot =>
            $"{slot.RoleId}:{slot.Marker ?? "$"}:{slot.ValueTypeSymbol.Name}:{slot.Direction}:{slot.Cardinality}"));
        return $"{command.Id}|{command.Name}|aliases={aliases}|{frame.Id}|{frame.UsageName}|" +
            $"result={frame.ResultTypeSymbol.Name}|default={frame.IsDefault}|qualifiers={qualifiers}|{slots}";
    }

    private static string JoinOrDash(IEnumerable<string> values)
    {
        string[] snapshot = values.Order(StringComparer.Ordinal).ToArray();
        return snapshot.Length == 0 ? "-" : string.Join(',', snapshot);
    }
}
