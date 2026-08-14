using FluNET.Compilation.Schema;
using FluNET.Language;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class JsonSchemaInferenceTests
{
    [Test]
    public void InferenceMergesAllRecordsAndMarksMissingFieldsOptional()
    {
        LanguageTypeSystem types = StandardLanguage.CreateSnapshot().Types;
        using JsonDocument document = JsonDocument.Parse("[{\"id\":1,\"name\":\"Ada\"},{\"id\":2,\"active\":true}]");
        JsonElement[] samples = document.RootElement.EnumerateArray().Select(item => item.Clone()).ToArray();
        TypeSymbol type = new JsonSchemaInferencer().Infer(samples, types, "User").Type;

        Assert.Multiple(() =>
        {
            Assert.That(type.Kind, Is.EqualTo(TypeKind.Object));
            Assert.That(type.Fields["id"].Type.Id, Is.EqualTo(BuiltInTypeIds.Number));
            Assert.That(type.Fields["id"].IsRequired, Is.True);
            Assert.That(type.Fields["name"].IsRequired, Is.False);
            Assert.That(type.Fields["active"].IsRequired, Is.False);
        });
    }

    [Test]
    public void InferenceCreatesUnionForHeterogeneousFieldValues()
    {
        LanguageTypeSystem types = StandardLanguage.CreateSnapshot().Types;
        using JsonDocument document = JsonDocument.Parse("[{\"value\":1},{\"value\":\"one\"},{\"value\":null}]");
        TypeSymbol type = new JsonSchemaInferencer().Infer(
            document.RootElement.EnumerateArray().Select(item => item.Clone()), types).Type;
        TypeSymbol value = type.Fields["value"].Type;
        Assert.Multiple(() =>
        {
            Assert.That(value.IsNullable, Is.True);
            Assert.That(value.NonNullableType.Kind, Is.EqualTo(TypeKind.Union));
            Assert.That(value.NonNullableType.UnionTypes.Select(item => item.Id),
                Is.EquivalentTo(new[] { BuiltInTypeIds.Number, BuiltInTypeIds.Text }));
        });
    }

    [Test]
    public void EquivalentSamplesProduceTheSameStableTypeId()
    {
        LanguageTypeSystem types = StandardLanguage.CreateSnapshot().Types;
        using JsonDocument a = JsonDocument.Parse("[{\"name\":\"Ada\",\"id\":1},{\"id\":2,\"name\":\"Bob\"}]");
        using JsonDocument b = JsonDocument.Parse("[{\"id\":9,\"name\":\"X\"}]");
        JsonSchemaInferencer inferencer = new();
        TypeSymbol left = inferencer.Infer(a.RootElement.EnumerateArray().Select(item => item.Clone()), types).Type;
        TypeSymbol right = inferencer.Infer(b.RootElement.EnumerateArray().Select(item => item.Clone()), types).Type;
        Assert.That(left.Id, Is.EqualTo(right.Id));
    }
}
