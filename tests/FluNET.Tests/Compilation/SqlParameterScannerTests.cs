using FluNET.Compilation.Sql;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SqlParameterScannerTests
{
    [Test]
    public void IgnoresLiteralsAndCommentsAndReturnsDistinctParameters()
    {
        IReadOnlyList<string> names = SqlParameterScanner.Scan(
            "SELECT '$ignored', \"$alsoIgnored\" -- $comment\n" +
            "FROM items /* $block */ WHERE id = $id OR owner = $id");

        Assert.That(names, Is.EqualTo(new[] { "id" }));
    }
}
