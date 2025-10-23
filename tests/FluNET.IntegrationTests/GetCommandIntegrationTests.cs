using Microsoft.Extensions.DependencyInjection;
using FluNET;
using FluNET.Context;
using FluNET.Extensions;
using FluNET.Lexicon;
using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;

namespace FluNET.IntegrationTests
{
    /// <summary>
    /// Integration tests for GET command with new {reference} and [variable] syntax.
    /// These tests are isolated from legacy tests to ensure clean test execution.
    /// </summary>
    [TestFixture]
    public class GetCommandIntegrationTests
    {
        private Engine engine = null!;
        private string testFilePath = null!;
        private string testDirectory = null!;
        private ServiceProvider? serviceProvider;
        private IServiceScope? scope;

        [SetUp]
        public void Setup()
        {
            // Setup DI container with proper scope management
            ServiceCollection services = new();
            FluNetContext.ConfigureDefaultServices(services);

            serviceProvider = services.BuildServiceProvider();
            scope = serviceProvider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();

            // Create test directory and file
            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_IntegrationTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            testFilePath = Path.Combine(testDirectory, "test.txt");
            File.WriteAllText(testFilePath, "This is a test file\nWith multiple lines\nFor testing GET command");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                // Dispose scope and service provider to clean up resources
                scope?.Dispose();
                serviceProvider?.Dispose();

                // Cleanup test files
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - OS will clean up temp files eventually
                Console.WriteLine($"Warning: TearDown cleanup failed: {ex.Message}");
            }
        }

        #region Basic GET Tests

        [Test]
        public void Get_FromExistingFile_ShouldReturnFileContents()
        {
            // Arrange
            ProcessedPrompt prompt = new($"GET [text] FROM {{{testFilePath}}} .");

            // Act
            var (validation, sentence, result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, "Sentence should be valid");
                Assert.That(sentence, Is.Not.Null, "Sentence should be created");
                Assert.That(result, Is.Not.Null, "Result should not be null");
                Assert.That(result, Is.InstanceOf<string[]>(), "Result should be string array");

                string[]? lines = result as string[];
                Assert.That(lines, Is.Not.Null);
                Assert.That(lines!.Length, Is.GreaterThan(0), "File should have content");
                Assert.That(string.Join("", lines), Does.Contain("This is a test file"));
            });
        }

        [Test]
        public void Get_FromNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            var nonExistentPath = Path.Combine(testDirectory, "does_not_exist.txt");
            ProcessedPrompt prompt = new($"GET [text] FROM {{{nonExistentPath}}} .");

            // Act
            var (validation, sentence, result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, "Sentence structure is valid");
                Assert.That(sentence, Is.Not.Null, "Sentence should be created");
                Assert.That(result, Is.Null, "Execution should return null for non-existent file");
            });
        }

        [Test]
        public void Get_WithVariable_ShouldResolveAndExecute()
        {
            // Arrange
            engine.RegisterVariable("filePath", testFilePath);
            ProcessedPrompt prompt = new($"GET [text] FROM [filePath] .");

            // Act
            var (validation, sentence, result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string[]>());
            });
        }

        [Test]
        public void Get_WithRelativePath_ShouldWork()
        {
            // Arrange
            var currentDir = Directory.GetCurrentDirectory();
            var relativePath = "test_relative.txt";
            var fullPath = Path.Combine(currentDir, relativePath);
            File.WriteAllText(fullPath, "Relative path test");

            try
            {
                ProcessedPrompt prompt = new($"GET [text] FROM {{{relativePath}}} .");

                // Act
                var (validation, sentence, result) = engine.Run(prompt);

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(validation.IsValid, Is.True);
                    Assert.That(sentence, Is.Not.Null);
                });
            }
            finally
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
        }

        #endregion Basic GET Tests

        #region Integration Tests

        [Test]
        public void Get_EndToEnd_MultipleExecutions_ShouldWork()
        {
            // Arrange
            var file1 = Path.Combine(testDirectory, "file1.txt");
            var file2 = Path.Combine(testDirectory, "file2.txt");
            File.WriteAllText(file1, "File 1 content");
            File.WriteAllText(file2, "File 2 content");

            // Act & Assert - First execution
            var result1 = engine.Run(new ProcessedPrompt($"GET [data] FROM {{{file1}}} ."));
            Assert.Multiple(() =>
            {
                Assert.That(result1.ValidationResult.IsValid, Is.True);
                Assert.That(result1.Result, Is.Not.Null);
            });

            // Act & Assert - Second execution
            var result2 = engine.Run(new ProcessedPrompt($"GET [data] FROM {{{file2}}} ."));
            Assert.Multiple(() =>
            {
                Assert.That(result2.ValidationResult.IsValid, Is.True);
                Assert.That(result2.Result, Is.Not.Null);
            });
        }

        [Test]
        public void Get_WithEmptyFile_ShouldReturnEmptyArray()
        {
            // Arrange
            var emptyFile = Path.Combine(testDirectory, "empty.txt");
            File.WriteAllText(emptyFile, "");
            ProcessedPrompt prompt = new($"GET [text] FROM {{{emptyFile}}} .");

            // Act
            var (validation, sentence, result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string[]>());
                string[]? lines = result as string[];
                Assert.That(lines!.Length, Is.EqualTo(1)); // Empty file has one empty line
            });
        }

        [Test]
        public void Get_WithLargeFile_ShouldHandleCorrectly()
        {
            // Arrange
            var largeFile = Path.Combine(testDirectory, "large.txt");
            var lines = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                lines.Add($"Line {i}: Test data for large file processing");
            }
            File.WriteAllLines(largeFile, lines);
            ProcessedPrompt prompt = new($"GET [data] FROM {{{largeFile}}} .");

            // Act
            var (validation, sentence, result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string[]>());
                string[]? resultLines = result as string[];
                Assert.That(resultLines!.Length, Is.GreaterThanOrEqualTo(1000));
            });
        }

        [Test]
        public void Get_WithSpecialCharactersInPath_ShouldWork()
        {
            // Arrange
            var specialFile = Path.Combine(testDirectory, "file_with_underscores.txt");
            File.WriteAllText(specialFile, "Special characters test");
            ProcessedPrompt prompt = new($"GET [text] FROM {{{specialFile}}} .");

            // Act
            var (validation, sentence, result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public void Get_WithPathContainingSpaces_ShouldWork()
        {
            // Arrange
            var fileWithSpaces = Path.Combine(testDirectory, "file with spaces.txt");
            File.WriteAllText(fileWithSpaces, "Spaces in path test");
            ProcessedPrompt prompt = new($"GET [text] FROM {{{fileWithSpaces}}} .");

            // Act
            var (validation, sentence, result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, "Should handle paths with spaces");
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string[]>());
                string[]? lines = result as string[];
                Assert.That(string.Join("", lines!), Does.Contain("Spaces in path test"));
            });
        }

        [Test]
        public void Get_WithNestedBracesInReference_ShouldWork()
        {
            // Arrange - Using variable inside reference {{{variableName}}}
            engine.RegisterVariable("filepath", testFilePath);
            ProcessedPrompt prompt = new($"GET [text] FROM {{{{{testFilePath}}}}} .");

            // Act
            var (validation, sentence, result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True, "Should handle nested braces");
        }

        #endregion Integration Tests

        #region Edge Cases

        [Test]
        public void Get_WithTrailingPeriodInFilePath_ShouldHandleCorrectly()
        {
            // Arrange
            var testFile = Path.Combine(testDirectory, "test_period.txt");
            File.WriteAllText(testFile, "Period test");
            ProcessedPrompt prompt = new($"GET [text] FROM {{{testFile}}} .");

            // Act
            var (validation, sentence, result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True, "Should handle terminator correctly");
        }

        #endregion Edge Cases
    }
}