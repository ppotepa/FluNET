using FluNET.Language;
using FluNET.Variables;

namespace FluNET.Tests.Variables;

[TestFixture]
public sealed class VariableStoreContractTests
{
    [Test]
    public void HostIntegerIsStoredAsCanonicalNumberRuntimeValue()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        VariableStore store = new(language);

        store.RegisterHost("count", 42);
        RuntimeValue value = store.Get("count");

        Assert.Multiple(() =>
        {
            Assert.That(value.Type.Id, Is.EqualTo(BuiltInTypeIds.Number));
            Assert.That(value.Value, Is.TypeOf<decimal>());
            Assert.That(value.Value, Is.EqualTo(42m));
        });
    }

    [Test]
    public void MoreSpecificScopeShadowsHostWithoutChangingLanguageType()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        VariableStore store = new(language);
        store.RegisterHost("count", 1);
        VariableSymbol symbol = new("count", language.Types.Number, 0);

        store.Set(symbol, 2d, VariableScopeKind.Workflow);
        RuntimeValue value = store.Get("count");

        Assert.Multiple(() =>
        {
            Assert.That(value.Type.Id, Is.EqualTo(BuiltInTypeIds.Number));
            Assert.That(value.Value, Is.EqualTo(2m));
        });
    }
}
