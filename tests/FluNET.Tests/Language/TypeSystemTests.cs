using FluNET.Compilation;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Syntax.Verbs;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class TypeSystemTests
{
    [Test]
    public void ModuleCanDeclareFrameSpecificRolesAndTypeNames()
    {
        FrameRoleId condition = new("Condition");
        LanguageBuilder builder = new LanguageBuilder().Type<Predicate>("Predicate");
        builder.Command<SayText, string>("CHECK", "Predicate")
            .Positional<Predicate>(condition);

        LanguageSnapshot language = builder.Build();
        CommandSlotDescriptor slot = language.FindCommand("CHECK")!.Frames.Single().Slots.Single();

        Assert.Multiple(() =>
        {
            Assert.That(slot.RoleId, Is.EqualTo(condition));
            Assert.That(slot.ValueTypeSymbol.Name, Is.EqualTo("Predicate"));
            Assert.That(slot.ValueTypeSymbol.Id, Is.EqualTo(new TypeId("type.predicate")));
            Assert.That(slot.ValueTypeSymbol.Kind, Is.EqualTo(TypeKind.Scalar));
            Assert.That(language.Types.Get<string>().Name, Is.EqualTo("Text"));
        });
    }

    [Test]
    public void BuiltInsHaveStableIdsIndependentOfClrRuntimeTypes()
    {
        LanguageTypeSystem types = StandardLanguage.CreateSnapshot().Types;

        Assert.Multiple(() =>
        {
            Assert.That(types.Unit.Id, Is.EqualTo(BuiltInTypeIds.Unit));
            Assert.That(types.Text.Id, Is.EqualTo(BuiltInTypeIds.Text));
            Assert.That(types.Boolean.Id, Is.EqualTo(BuiltInTypeIds.Boolean));
            Assert.That(types.Number.Id, Is.EqualTo(BuiltInTypeIds.Number));
            Assert.That(types.File.Id, Is.EqualTo(BuiltInTypeIds.File));
            Assert.That(types.Directory.Id, Is.EqualTo(BuiltInTypeIds.Directory));
            Assert.That(types.Uri.Id, Is.EqualTo(BuiltInTypeIds.Uri));
            Assert.That(types.Json.Id, Is.EqualTo(BuiltInTypeIds.Json));
            Assert.That(types.Object.Id, Is.EqualTo(BuiltInTypeIds.Object));
            Assert.That(types.Get<int>(), Is.SameAs(types.Number));
            Assert.That(types.Get<double>(), Is.SameAs(types.Number));
            Assert.That(types.Number.RuntimeTypes, Does.Contain(typeof(decimal)));
            Assert.That(types.Number.RuntimeTypes, Does.Contain(typeof(int)));
        });
    }

    [Test]
    public void StructuralListMapAndOptionalTypesAreInterned()
    {
        LanguageTypeSystem types = StandardLanguage.CreateSnapshot().Types;

        TypeSymbol arrayList = types.Get<string[]>();
        TypeSymbol genericList = types.Get<List<string>>();
        TypeSymbol mapFromClr = types.Get<Dictionary<string, int>>();
        TypeSymbol mapFromLanguage = types.Map(types.Text, types.Number);
        TypeSymbol optionalFromClr = types.Get<int?>();
        TypeSymbol optionalFromLanguage = types.Optional(types.Number);

        Assert.Multiple(() =>
        {
            Assert.That(arrayList, Is.SameAs(genericList));
            Assert.That(arrayList, Is.SameAs(types.List(types.Text)));
            Assert.That(arrayList.Id, Is.EqualTo(new TypeId("list<flunet.text>")));
            Assert.That(arrayList.Kind, Is.EqualTo(TypeKind.List));
            Assert.That(arrayList.ElementType, Is.SameAs(types.Text));
            Assert.That(arrayList.RuntimeTypes, Does.Contain(typeof(string[])));
            Assert.That(arrayList.RuntimeTypes, Does.Contain(typeof(List<string>)));

            Assert.That(mapFromClr, Is.SameAs(mapFromLanguage));
            Assert.That(mapFromClr.Id, Is.EqualTo(new TypeId("map<flunet.text,flunet.number>")));
            Assert.That(mapFromClr.Kind, Is.EqualTo(TypeKind.Map));
            Assert.That(mapFromClr.KeyType, Is.SameAs(types.Text));
            Assert.That(mapFromClr.ValueType, Is.SameAs(types.Number));

            Assert.That(optionalFromClr, Is.SameAs(optionalFromLanguage));
            Assert.That(optionalFromClr.Name, Is.EqualTo("Optional<Number>"));
            Assert.That(optionalFromClr.Nullability, Is.EqualTo(TypeNullability.Nullable));
            Assert.That(optionalFromClr.NonNullableType, Is.SameAs(types.Number));
        });
    }

    [Test]
    public void AssignabilityUsesLanguageStructureRatherThanClrInheritance()
    {
        LanguageTypeSystem types = StandardLanguage.CreateSnapshot().Types;
        TypeSymbol optionalNumber = types.Optional(types.Number);
        TypeSymbol numberOrBoolean = types.Union(types.Number, types.Boolean);
        TypeSymbol sameUnionDifferentOrder = types.Union(types.Boolean, types.Number);

        Assert.Multiple(() =>
        {
            Assert.That(optionalNumber.IsAssignableFrom(types.Number), Is.True);
            Assert.That(types.Number.IsAssignableFrom(optionalNumber), Is.False);
            Assert.That(numberOrBoolean, Is.SameAs(sameUnionDifferentOrder));
            Assert.That(numberOrBoolean.Kind, Is.EqualTo(TypeKind.Union));
            Assert.That(numberOrBoolean.IsAssignableFrom(types.Number), Is.True);
            Assert.That(numberOrBoolean.IsAssignableFrom(types.Boolean), Is.True);
            Assert.That(types.Number.IsAssignableFrom(numberOrBoolean), Is.False);
        });
    }

    [Test]
    public void ObjectTypesExposeFieldsAndUseStructuralAssignability()
    {
        LanguageTypeSystem types = StandardLanguage.CreateSnapshot().Types;
        TypeSymbol named = types.ObjectType(
            new TypeId("tests.named"),
            "Named",
            [new TypeFieldSymbol("name", types.Text)]);
        TypeSymbol person = types.ObjectType(
            new TypeId("tests.person"),
            "Person",
            [
                new TypeFieldSymbol("name", types.Text),
                new TypeFieldSymbol("age", types.Number)
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(named.Kind, Is.EqualTo(TypeKind.Object));
            Assert.That(person.Fields.Keys, Is.EquivalentTo(new[] { "name", "age" }));
            Assert.That(named.IsAssignableFrom(person), Is.True);
            Assert.That(person.IsAssignableFrom(named), Is.False);
        });
    }

    [Test]
    public void CustomRoleDiagnosticsDoNotDependOnTheCompatibilityEnum()
    {
        FrameRoleId comparison = new("Comparison");
        LanguageBuilder builder = new();
        builder.Command<SayText, string>("CHECK", "Text")
            .Positional<string>(SemanticRole.Theme)
            .Marked<string>(comparison, "AS");
        LanguageSnapshot language = builder.Build();
        ProcessedPrompt prompt = new("CHECK value AS first second", language.Grammar);
        IReadOnlyList<BoundCommand> commands = new SemanticCommandBinder(language)
            .BindProgram(prompt.Syntax);
        BoundProgram program = new(
            new FluNetProgram(prompt),
            commands.Select(command => new BoundCommandStatement(command)));

        DiagnosticBag diagnostics = new SemanticProgramValidator(language).Validate(program);
        CompilationDiagnostic diagnostic = diagnostics.Single(item =>
            item.Code == CompilationDiagnosticCodes.SurplusArgument);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Phase, Is.EqualTo(CompilationPhase.Validate));
            Assert.That(diagnostic.Message, Does.Contain("COMPARISON"));
        });
    }

    private sealed record Predicate(string Text);
}
