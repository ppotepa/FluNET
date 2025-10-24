using FluNET.Context;
using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;
using Microsoft.Extensions.DependencyInjection;
using FluNET.Extensions;

namespace FluNET.Tests
{
    /// <summary>
    /// Quick test to verify both literal and reference syntax for file paths
    /// </summary>
    [TestFixture]
    public class FileSyntaxComparisonTest
    {
        private Engine? engine;
        private string? testDirectory;
        private ServiceProvider? serviceProvider;
        private IServiceScope? scope;

        [SetUp]
        public void Setup()
        {
            // Setup DI container
            ServiceCollection services = new();
            FluNETContext.ConfigureDefaultServices(services);

            serviceProvider = services.BuildServiceProvider();
            scope = serviceProvider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();

            // Create test directory and file
            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_FileSyntax_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                scope?.Dispose();
                serviceProvider?.Dispose();

                if (testDirectory != null && Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Test]
        public void FilePath_LiteralSyntax_ShouldWork()
        {
            // Arrange - Create test file
            string testFile = Path.Combine(testDirectory!, "user.json");
            File.WriteAllText(testFile, @"{""name"": ""Alice"", ""age"": 30}");

            // Act - Use literal syntax (no curly braces)
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(
                new ProcessedPrompt($"GET [{{name, age}}] FROM {testFile}."));

            // Then access the variable
            (_, _, object? nameResult) = engine.Run(new ProcessedPrompt("SAY [name]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(nameResult, Is.EqualTo("Alice"));
            });
        }

        [Test]
        public void FilePath_ReferenceSyntax_ShouldWork()
        {
            // Arrange - Create test file
            string testFile = Path.Combine(testDirectory!, "user.json");
            File.WriteAllText(testFile, @"{""name"": ""Bob"", ""age"": 25}");

            // Act - Use reference syntax (with curly braces)
            Console.WriteLine($"Test file path: {testFile}");
            Console.WriteLine($"Command: GET [{{name, age}}] FROM {{{testFile}}}.");

            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(
                new ProcessedPrompt($"GET [{{name, age}}] FROM {{{testFile}}}."));

            Console.WriteLine($"Validation: {validation.IsValid}, Reason: {validation.FailureReason}");
            Console.WriteLine($"Result: {result}");

            // Then access the variable
            (_, _, object? nameResult) = engine.Run(new ProcessedPrompt("SAY [name]."));
            Console.WriteLine($"Name result: {nameResult}");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(nameResult, Is.EqualTo("Bob"));
            });
        }

        [Test]
        public void FilePath_BothSyntaxes_ProduceSameResult()
        {
            // Arrange - Create test file
            string testFile = Path.Combine(testDirectory!, "data.json");
            File.WriteAllText(testFile, @"{""value"": 42}");

            // Act - Test both syntaxes
            (ValidationResult val1, _, _) = engine!.Run(
                new ProcessedPrompt($"GET [{{value}}] FROM {testFile}."));
            (_, _, object? result1) = engine.Run(new ProcessedPrompt("SAY [value]."));

            // Reset and test with reference syntax
            (ValidationResult val2, _, _) = engine.Run(
                new ProcessedPrompt($"GET [{{value}}] FROM {{{testFile}}}."));
            (_, _, object? result2) = engine.Run(new ProcessedPrompt("SAY [value]."));

            // Assert - Both should work and produce same result
            Assert.Multiple(() =>
            {
                Assert.That(val1.IsValid, Is.True, "Literal syntax should be valid");
                Assert.That(val2.IsValid, Is.True, "Reference syntax should be valid");
                Assert.That(result1, Is.EqualTo("42"));
                Assert.That(result2, Is.EqualTo("42"));
                Assert.That(result1, Is.EqualTo(result2), "Both syntaxes should produce identical results");
            });
        }
    }
}
