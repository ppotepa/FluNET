using FluNET.Execution.Capabilities;
using FluNET.Language;

namespace FluNET.Tests;

public class CapabilityPolicyTests
{
    [Fact]
    public void Explicit_policy_denies_unlisted_capability()
    {
        VerbDescriptor get = new LanguageRegistry().Snapshot.GetVerbOverloads("GET").First();
        var policy = new ExplicitCapabilityPolicy(["filesystem.write"]);
        Assert.False(policy.IsAllowed("filesystem.read", get));
    }
}
