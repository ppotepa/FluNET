using FluNET.Context;
using FluNET.Syntax.Core;
using FluNET.Syntax.Registry;
using NUnit.Framework;

namespace FluNET.Tests.Registry
{
    [TestFixture]
    public class VerbRegistryTests
    {
        private FluNetContext? _context;
        private VerbRegistry? _registry;

        [SetUp]
        public void Setup()
        {
            _context = FluNetContext.Create();
            _registry = _context.GetService<VerbRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
        }

        [Test]
        public void VerbRegistry_ShouldDiscoverVerbs()
        {
            // Assert
            Assert.That(_registry, Is.Not.Null);
            Assert.That(_registry!.Count, Is.GreaterThan(0), "Should discover at least one verb");
        }

        [Test]
        public void VerbRegistry_ShouldFindGetVerb()
        {
            // Act
            IVerb getVerb = _registry!.GetVerbByName("GET");

            // Assert
            Assert.That(getVerb, Is.Not.Null);
            Assert.That(getVerb.Text, Is.EqualTo("GET"));
        }

        [Test]
        public void VerbRegistry_ShouldFindVerbBySynonym()
        {
            // Act
            IVerb fetchVerb = _registry!.GetVerbByName("FETCH");

            // Assert
            Assert.That(fetchVerb, Is.Not.Null);
            Assert.That(fetchVerb.Text, Is.EqualTo("GET"), "FETCH is a synonym for GET");
        }

        [Test]
        public void VerbRegistry_ShouldBeCaseInsensitive()
        {
            // Act
            IVerb getVerbUpper = _registry!.GetVerbByName("GET");
            IVerb getVerbLower = _registry!.GetVerbByName("get");
            IVerb getVerbMixed = _registry!.GetVerbByName("Get");

            // Assert
            Assert.That(getVerbUpper.Text, Is.EqualTo("GET"));
            Assert.That(getVerbLower.Text, Is.EqualTo("GET"));
            Assert.That(getVerbMixed.Text, Is.EqualTo("GET"));
        }

        [Test]
        public void VerbRegistry_ShouldThrowForUnknownVerb()
        {
            // Act & Assert
            Assert.Throws<VerbNotFoundException>(() => _registry!.GetVerbByName("UNKNOWNVERB"));
        }

        [Test]
        public void VerbRegistry_ShouldListAllVerbNames()
        {
            // Act
            var allVerbs = _registry!.GetAllVerbNames().ToList();

            // Assert
            Assert.That(allVerbs, Is.Not.Empty);
            Assert.That(allVerbs, Does.Contain("GET"));
            Assert.That(allVerbs, Does.Contain("SAVE"));
            Assert.That(allVerbs, Does.Contain("SAY"));

            // Synonyms should also be registered
            Assert.That(allVerbs, Does.Contain("FETCH"));
        }

        [Test]
        public void VerbRegistry_ShouldDiscoverCommonVerbs()
        {
            // Act - Try to get all common verbs
            var commonVerbNames = new[] { "GET", "SAVE", "SAY", "DELETE", "DOWNLOAD", "POST", "SEND", "TRANSFORM", "LOAD" };
            var discoveries = commonVerbNames.Select(name =>
            {
                try
                {
                    var verb = _registry!.GetVerbByName(name);
                    return new { Name = name, Found = true, VerbText = verb.Text };
                }
                catch
                {
                    return new { Name = name, Found = false, VerbText = (string?)null };
                }
            }).ToList();

            // Assert
            var foundVerbs = discoveries.Where(d => d.Found).ToList();
            Assert.That(foundVerbs.Count, Is.GreaterThan(5),
                $"Should discover most common verbs. Found: {string.Join(", ", foundVerbs.Select(v => v.Name))}");
        }
    }
}
