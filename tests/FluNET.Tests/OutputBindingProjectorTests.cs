using FluNET.Binding;
using FluNET.Execution;
using FluNET.Language;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Core;

namespace FluNET.Tests;

public class OutputBindingProjectorTests
{
    [Test]
    public void Projector_maps_multiple_outputs_from_named_properties()
    {
        var verb = new VerbDescriptor(typeof(DummyVerb), "GET", [], new SentencePattern("GET", []), () => null)
        {
            ResultType = typeof(UserResult)
        };
        BoundRole name = OutputRole("name", "userName");
        BoundRole email = OutputRole("email", "userEmail");
        var sentence = new BoundSentence(verb, null, [name, email], typeof(UserResult), 0);

        IReadOnlyDictionary<string, object?> projected = new OutputBindingProjector()
            .Project(sentence, new UserResult("Ada", "ada@example.test"));

        Assert.That(projected["userName"], Is.EqualTo("Ada"));
        Assert.That(projected["userEmail"], Is.EqualTo("ada@example.test"));
    }

    private static BoundRole OutputRole(string slot, string variable)
    {
        var descriptor = new ClauseDescriptor(ClauseKind.What, typeof(string), true, slot, RoleDirection.Output);
        var value = new BoundValue(new VariableExpression(variable), typeof(string), typeof(string), null, 0);
        return new BoundRole(descriptor, [value]);
    }

    private sealed class DummyVerb { }
    private sealed record UserResult(string Name, string Email);
}
