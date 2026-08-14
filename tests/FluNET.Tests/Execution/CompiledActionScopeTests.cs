using FluNET.Execution.Actions;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class CompiledActionScopeTests
{
    [Test]
    public void ActionScopeKeepsIterationOutputsLocal()
    {
        VariableResolver parent = new();
        using JsonDocument document = JsonDocument.Parse("{\"id\":1}");
        ActionScopeVariableResolver scope = new(parent,
            [new KeyValuePair<string, object?>("item", document.RootElement.Clone())], ["item"]);
        scope.Register("profile", "local");
        Assert.Multiple(() => { Assert.That(scope.Resolve<string>("[profile]"), Is.EqualTo("local")); Assert.That(parent.IsRegistered("profile"), Is.False); Assert.That(() => scope.Register("item", "changed"), Throws.InvalidOperationException); });
    }
}
