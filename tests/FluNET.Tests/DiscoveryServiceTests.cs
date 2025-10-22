using FluNET.Keywords;
using FluNET.Syntax.Core;
using FluNET.Syntax.Verbs;
using FluNET.Words;

namespace FluNET.Tests
{
    [TestFixture]
    public class DiscoveryServiceTests
    {
        private DiscoveryService? discoveryService;

        [SetUp]
        public void Setup()
        {
            // Force assembly loading by referencing types from engine assembly
            _ = typeof(GetText);        // FluNET.Engine assembly

            discoveryService = new DiscoveryService();
        }

        [Test]
        public void Constructor_ShouldDiscoverWords()
        {
            // Assert
            Assert.That(discoveryService!.Words, Is.Not.Empty, "Should discover at least some words");

            TestContext.WriteLine($"Total words discovered: {discoveryService.Words.Count}");
            TestContext.WriteLine("\nDiscovered word types:");
            foreach (var word in discoveryService.Words.Take(20))
            {
                TestContext.WriteLine($"  - {word.FullName}");
            }
        }

        [Test]
        public void Verbs_ShouldDiscoverApplicationVerbs()
        {
            // Assert
            Assert.That(discoveryService!.Verbs, Is.Not.Empty, "Should discover at least some verbs");

            TestContext.WriteLine($"Total verbs discovered: {discoveryService.Verbs.Count}");
            TestContext.WriteLine("\nDiscovered verbs:");
            foreach (var verb in discoveryService.Verbs)
            {
                TestContext.WriteLine($"  - {verb.FullName}");
            }

            // Check for specific application verbs
            Assert.That(discoveryService.Verbs.Any(v => v.Name == "GetText"), Is.True, "Should find GetText verb");
            Assert.That(discoveryService.Verbs.Any(v => v.Name == "DownloadFile"), Is.True, "Should find DownloadFile verb");
            Assert.That(discoveryService.Verbs.Any(v => v.Name == "SaveText"), Is.True, "Should find SaveText verb");
        }

        [Test]
        public void Verbs_CheckForCLIVerbs()
        {
            // Note: CLI verbs may not be loaded in test context since we don't reference CLI project
            // This test just checks if any CLI verbs happen to be discovered

            var cliVerbs = discoveryService!.Verbs.Where(v => v.Namespace?.Contains("FluNET.CLI.Verbs") == true).ToList();

            TestContext.WriteLine($"CLI verbs discovered: {cliVerbs.Count}");
            foreach (var verb in cliVerbs)
            {
                TestContext.WriteLine($"  - {verb.Name}");
            }

            // This is informational only - CLI verbs may not be present in test assembly context
            if (cliVerbs.Any())
            {
                TestContext.WriteLine("CLI verbs were discovered (CLI assembly is loaded)");
            }
            else
            {
                TestContext.WriteLine("CLI verbs were not discovered (CLI assembly not loaded in test context)");
            }
        }

        [Test]
        public void Verbs_ShouldNotIncludeAbstractVerbs()
        {
            // Assert
            var abstractVerbs = discoveryService!.Verbs.Where(v => v.IsAbstract).ToList();

            TestContext.WriteLine($"Abstract verbs found: {abstractVerbs.Count}");
            foreach (var verb in abstractVerbs)
            {
                TestContext.WriteLine($"  - {verb.Name}");
            }

            Assert.That(abstractVerbs, Is.Empty, "Should not include abstract verb base classes");
        }

        [Test]
        public void Verbs_ShouldNotIncludeInterfaces()
        {
            // Assert
            var interfaceVerbs = discoveryService!.Verbs.Where(v => v.IsInterface).ToList();

            Assert.That(interfaceVerbs, Is.Empty, "Should not include verb interfaces");
        }

        [Test]
        public void Nouns_ShouldDiscoverNouns()
        {
            // Assert
            Assert.That(discoveryService!.Nouns, Is.Not.Empty, "Should discover at least some nouns");

            TestContext.WriteLine($"Total nouns discovered: {discoveryService.Nouns.Count}");
            TestContext.WriteLine("\nDiscovered nouns:");
            foreach (var noun in discoveryService.Nouns.Take(20))
            {
                TestContext.WriteLine($"  - {noun.FullName}");
            }
        }

        [Test]
        public void ClearCache_ShouldRefreshDiscovery()
        {
            // Arrange
            int initialWordCount = discoveryService!.Words.Count;
            int initialVerbCount = discoveryService.Verbs.Count;

            // Act
            discoveryService.ClearCache();

            // Assert
            Assert.That(discoveryService.Words.Count, Is.EqualTo(initialWordCount),
                "Word count should remain the same after cache clear");
            Assert.That(discoveryService.Verbs.Count, Is.EqualTo(initialVerbCount),
                "Verb count should remain the same after cache clear");
        }

        [Test]
        public void RefreshAssemblies_ShouldRediscoverWords()
        {
            // Arrange
            int initialWordCount = discoveryService!.Words.Count;

            // Act
            discoveryService.RefreshAssemblies();

            // Assert
            Assert.That(discoveryService.Words.Count, Is.GreaterThanOrEqualTo(initialWordCount),
                "Word count should not decrease after refresh");
        }

        [Test]
        public void Words_ShouldIncludeKeywords()
        {
            // Assert - Keywords like FROM, TO should be discovered
            var keywords = discoveryService!.Words.Where(w =>
                w.Namespace?.Contains("FluNET.Keywords") == true).ToList();

            TestContext.WriteLine($"Keywords discovered: {keywords.Count}");
            foreach (var keyword in keywords)
            {
                TestContext.WriteLine($"  - {keyword.Name}");
            }

            Assert.That(keywords, Is.Not.Empty, "Should discover keyword implementations");
            Assert.That(keywords.Any(k => k.Name == "From"), Is.True, "Should find FROM keyword");
            Assert.That(keywords.Any(k => k.Name == "To"), Is.True, "Should find TO keyword");
        }

        [Test]
        public void Verbs_GroupedByBaseType_ShouldShowInheritance()
        {
            // Arrange - Group verbs by their base type
            var verbGroups = discoveryService!.Verbs
                .Where(v => v.BaseType != null && v.BaseType.IsGenericType)
                .GroupBy(v => v.BaseType!.GetGenericTypeDefinition())
                .OrderByDescending(g => g.Count())
                .ToList();

            // Assert
            TestContext.WriteLine($"Verb inheritance groups: {verbGroups.Count}");
            foreach (var group in verbGroups)
            {
                TestContext.WriteLine($"\n{group.Key.Name} ({group.Count()} implementations):");
                foreach (var verb in group)
                {
                    TestContext.WriteLine($"  - {verb.Name}");
                }
            }

            Assert.That(verbGroups, Is.Not.Empty, "Should have verb inheritance groups");

            // Check for specific base types
            Assert.That(verbGroups.Any(g => g.Key.Name.Contains("Get")), Is.True,
                "Should have Get<,> base type implementations");
            Assert.That(verbGroups.Any(g => g.Key.Name.Contains("Download")), Is.True,
                "Should have Download<,,> base type implementations");
        }

        [Test]
        public void Verbs_ShouldHaveValidText()
        {
            // Arrange - Try to instantiate each verb and check its Text property
            var verbsWithInvalidText = new List<string>();

            foreach (var verbType in discoveryService!.Verbs)
            {
                try
                {
                    // Try parameterless constructor
                    var instance = Activator.CreateInstance(verbType);
                    if (instance is IKeyword keyword)
                    {
                        if (string.IsNullOrEmpty(keyword.Text))
                        {
                            verbsWithInvalidText.Add($"{verbType.Name} has empty Text");
                        }
                    }
                }
                catch
                {
                    // Some verbs might require constructor parameters, that's okay
                }
            }

            // Assert
            TestContext.WriteLine($"Verbs checked: {discoveryService.Verbs.Count}");
            TestContext.WriteLine($"Verbs with invalid text: {verbsWithInvalidText.Count}");

            foreach (var issue in verbsWithInvalidText)
            {
                TestContext.WriteLine($"  - {issue}");
            }

            Assert.That(verbsWithInvalidText, Is.Empty,
                "All instantiable verbs should have non-empty Text property");
        }

        [Test]
        public void DiscoveryService_ShouldFindVerbsFromLoadedAssemblies()
        {
            // Arrange - Get verbs from different assemblies
            var engineVerbs = discoveryService!.Verbs
                .Where(v => v.Assembly.GetName().Name?.Contains("FluNET") == true &&
                           !v.Assembly.GetName().Name?.Contains("CLI") == true)
                .ToList();

            var cliVerbs = discoveryService.Verbs
                .Where(v => v.Assembly.GetName().Name?.Contains("CLI") == true)
                .ToList();

            // Assert
            TestContext.WriteLine($"Engine verbs: {engineVerbs.Count}");
            TestContext.WriteLine($"CLI verbs: {cliVerbs.Count}");

            TestContext.WriteLine("\nEngine verbs:");
            foreach (var verb in engineVerbs)
            {
                TestContext.WriteLine($"  - {verb.Name} from {verb.Assembly.GetName().Name}");
            }

            TestContext.WriteLine("\nCLI verbs:");
            foreach (var verb in cliVerbs)
            {
                TestContext.WriteLine($"  - {verb.Name} from {verb.Assembly.GetName().Name}");
            }

            TestContext.WriteLine($"\nTotal assemblies loaded: {AppDomain.CurrentDomain.GetAssemblies().Length}");
            TestContext.WriteLine("Loaded assemblies:");
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.GetName().Name))
            {
                TestContext.WriteLine($"  - {asm.GetName().Name}");
            }

            Assert.That(engineVerbs, Is.Not.Empty, "Should find verbs from engine assembly");
            // CLI verbs are optional in test context since CLI assembly may not be referenced
        }
    }
}
