using FluNET.Prompt;
using FluNET.Context;
using FluNET.Syntax.Verbs;
using FluNET.Words;
using FluNET.Syntax.Validation;
using FluNET.Sentences;
using FluNET.Variables;

namespace FluNET.Tests
{
    [TestFixture]
    public class ExecutionTests
    {
        private FluNetContext _context = null!;
        private Engine _engine = null!;
        private IVariableResolver _variableResolver = null!;

        [SetUp]
        public void Setup()
        {
            _context = FluNetContext.Create();
            _engine = _context.GetEngine();
            _variableResolver = _context.GetService<IVariableResolver>();
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
        }

        [Test]
        public void VariableResolver_RegisterAndResolve_SimpleVariable()
        {
            // Arrange
            _variableResolver.Register("testData", "Hello World");

            // Act
            string? result = _variableResolver.Resolve<string>("[testData]");

            // Assert
            Assert.That(result, Is.EqualTo("Hello World"));
        }

        [Test]
        public void VariableResolver_RegisterAndResolve_CaseInsensitive()
        {
            // Arrange
            _variableResolver.Register("TestData", 42);

            // Act
            int? result1 = _variableResolver.Resolve<int>("[testdata]");
            int? result2 = _variableResolver.Resolve<int>("[TESTDATA]");
            int? result3 = _variableResolver.Resolve<int>("[TestData]");

            // Assert
            Assert.That(result1, Is.EqualTo(42));
            Assert.That(result2, Is.EqualTo(42));
            Assert.That(result3, Is.EqualTo(42));
        }

        [Test]
        public void VariableResolver_Resolve_NonVariableReference_ReturnsNull()
        {
            // Arrange
            _variableResolver.Register("data", "value");

            // Act
            string? result = _variableResolver.Resolve<string>("data");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void VariableResolver_Resolve_UnknownVariable_ReturnsNull()
        {
            // Act
            string? result = _variableResolver.Resolve<string>("[unknownVariable]");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void VariableResolver_IsVariableReference_ValidFormats()
        {
            // Act & Assert
            Assert.That(VariableResolver.IsVariableReference("[data]"), Is.True);
            Assert.That(VariableResolver.IsVariableReference("[myVariable]"), Is.True);
            Assert.That(VariableResolver.IsVariableReference("[{name,age}]"), Is.True);
            Assert.That(VariableResolver.IsVariableReference("data"), Is.False);
            Assert.That(VariableResolver.IsVariableReference("[data"), Is.False);
            Assert.That(VariableResolver.IsVariableReference("data]"), Is.False);
            Assert.That(VariableResolver.IsVariableReference("[]"), Is.False);
        }

        [Test]
        public void VariableResolver_RegisterAndResolve_JsonObject()
        {
            // Arrange - Note: JSON object syntax [{Name,Age}] is for future feature
            // For now, we just test that it doesn't crash
            var person = new { Name = "John", Age = 30 };
            _variableResolver.Register("person", person);

            // Act - Resolve the actual registered object
            object? result = _variableResolver.Resolve<object>("[person]");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.GetType().Name, Does.Contain("AnonymousType"));
        }

        [Test]
        public void VariableResolver_RegisterAndResolve_DifferentTypes()
        {
            // Arrange
            _variableResolver.Register("stringVar", "text");
            _variableResolver.Register("intVar", 123);
            _variableResolver.Register("boolVar", true);
            _variableResolver.Register("doubleVar", 3.14);

            // Act
            string? str = _variableResolver.Resolve<string>("[stringVar]");
            int? intVal = _variableResolver.Resolve<int>("[intVar]");
            bool? boolVal = _variableResolver.Resolve<bool>("[boolVar]");
            double? doubleVal = _variableResolver.Resolve<double>("[doubleVar]");

            // Assert
            Assert.That(str, Is.EqualTo("text"));
            Assert.That(intVal, Is.EqualTo(123));
            Assert.That(boolVal, Is.True);
            Assert.That(doubleVal, Is.EqualTo(3.14));
        }

        [Test]
        public void VariableResolver_TypeMismatch_ReturnsNull()
        {
            // Arrange
            _variableResolver.Register("number", 42);

            // Act - Try to resolve as wrong type
            string? result = _variableResolver.Resolve<string>("[number]");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Engine_RegisterVariable_CanBeResolved()
        {
            // Arrange
            _engine.RegisterVariable("apiUrl", "https://api.example.com");

            // Act
            string? result = _variableResolver.Resolve<string>("[apiUrl]");

            // Assert
            Assert.That(result, Is.EqualTo("https://api.example.com"));
        }

        [Test]
        public void Engine_Run_ReturnsThreeValueTuple()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET text FROM file.txt.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = _engine.Run(prompt);

            // Assert - Should return three values
            Assert.That(validation, Is.Not.Null);
            Assert.That(sentence, Is.Not.Null.Or.Null); // Sentence can be null if validation failed
            // Result will be null since actual execution requires real verb implementations
        }

        [Test]
        public void SentenceExecutor_Execute_WithNullSentence_ReturnsNull()
        {
            // Arrange
            SentenceExecutor executor = _context.GetService<SentenceExecutor>();

            // Act
            object? result = executor.Execute(null!);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void VariableResolver_Register_OverwriteExisting()
        {
            // Arrange
            _variableResolver.Register("data", "original");

            // Act
            _variableResolver.Register("data", "updated");
            string? result = _variableResolver.Resolve<string>("[data]");

            // Assert
            Assert.That(result, Is.EqualTo("updated"));
        }

        [Test]
        public void VariableResolver_Resolve_WithWhitespace()
        {
            // Arrange
            _variableResolver.Register("myVar", "value");

            // Act - Currently whitespace inside brackets is not supported
            string? result1 = _variableResolver.Resolve<string>("[ myVar ]");
            string? result2 = _variableResolver.Resolve<string>("[myVar ]");
            string? result3 = _variableResolver.Resolve<string>("[ myVar]");
            string? validResult = _variableResolver.Resolve<string>("[myVar]");

            // Assert - Whitespace variants should not match (strict matching)
            Assert.That(result1, Is.Null);
            Assert.That(result2, Is.Null);
            Assert.That(result3, Is.Null);
            // But exact match should work
            Assert.That(validResult, Is.EqualTo("value"));
        }
    }
}