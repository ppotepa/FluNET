using FluNET.Extensions;
using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Syntax.Verbs;
using FluNET.Tokens.Tree;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests
{
    /// <summary>
    /// Test cases for the DELETE command.
    /// These tests serve as both verification and usage examples.
    /// </summary>
    [TestFixture]
    public class DeleteCommandTests
    {
        private ServiceProvider? provider;
        private IServiceScope? scope;
        private Engine? engine;
        private string? testDirectory;

        [SetUp]
        public void Setup()
        {
            var services = new ServiceCollection();

            // Register core services
            services.AddTransient<DiscoveryService>();
            services.AddTransient<Tokens.TokenFactory>();
            services.AddTransient<TokenTreeFactory>();
            services.AddTransient<Words.WordFactory>();
            services.AddTransient<Lexicon.Lexicon>();
            services.AddTransient<SentenceValidator>();
            services.AddTransient<SentenceFactory>();
            services.AddPatternMatchers(); // Register pattern matchers (regex and string-based)
            services.AddScoped<Variables.IVariableResolver, Variables.VariableResolver>(); // Scoped so Engine and SentenceExecutor share same instance
            services.AddTransient<SentenceExecutor>();
            services.AddTransient<Execution.ExecutionPipelineFactory>();
            services.AddTransient<Engine>();

            provider = services.BuildServiceProvider();
            scope = provider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();

            // Create a temporary test directory
            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_DeleteTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            scope?.Dispose();
            provider?.Dispose();

            // Clean up test directory
            if (testDirectory != null && Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Pattern 1: Direct File Path (FROM not required)

        [Test]
        public void Delete_WithFullPath_ShouldDeleteFile()
        {
            // Arrange - Create a test file
            string testFile = Path.Combine(testDirectory!, "test.txt");
            File.WriteAllText(testFile, "Test content");
            Assert.That(File.Exists(testFile), Is.True, "Test file should be created");

            ProcessedPrompt prompt = new($"DELETE {testFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<DeleteFile>());
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Does.Contain("Deleted"));
                Assert.That(File.Exists(testFile), Is.False, "File should be deleted");
            });
        }

        [Test]
        public void Delete_WithRelativePath_ShouldDeleteFile()
        {
            // Arrange - Create a test file in current directory
            string currentDir = Directory.GetCurrentDirectory();
            string testFile = Path.Combine(currentDir, "temp_delete_test.txt");
            File.WriteAllText(testFile, "Test content");

            try
            {
                ProcessedPrompt prompt = new("DELETE temp_delete_test.txt.");

                // Act
                (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                    Assert.That(sentence, Is.Not.Null);
                    Assert.That(result as string, Does.Contain("Deleted"));
                    Assert.That(File.Exists(testFile), Is.False, "File should be deleted");
                });
            }
            finally
            {
                // Cleanup in case test fails
                if (File.Exists(testFile))
                {
                    File.Delete(testFile);
                }
            }
        }

        [Test]
        public void Delete_FileNotFound_ShouldReturnNotFoundMessage()
        {
            // Arrange
            string nonExistentFile = Path.Combine(testDirectory!, "nonexistent.txt");
            ProcessedPrompt prompt = new($"DELETE {nonExistentFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result as string, Does.Contain("File not found"));
            });
        }

        #endregion Pattern 1: Direct File Path (FROM not required)

        #region Pattern 2: Filename + Directory (FROM optional)

        [Test]
        public void Delete_WithFromDirectory_ShouldDeleteFile()
        {
            // Arrange - Create a test file
            string testFile = Path.Combine(testDirectory!, "test.txt");
            File.WriteAllText(testFile, "Test content");

            ProcessedPrompt prompt = new($"DELETE test.txt FROM {testDirectory}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<DeleteFile>());
                Assert.That(result as string, Does.Contain("Deleted"));
                Assert.That(File.Exists(testFile), Is.False, "File should be deleted");
            });
        }

        #endregion Pattern 2: Filename + Directory (FROM optional)

        #region Variable Support

        [Test]
        public void Delete_WithVariable_ShouldResolveAndDelete()
        {
            // Arrange - Create a test file
            string testFile = Path.Combine(testDirectory!, "variable_test.txt");
            File.WriteAllText(testFile, "Test content");

            engine!.RegisterVariable("filepath", testFile);
            ProcessedPrompt prompt = new("DELETE [filepath].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result as string, Does.Contain("Deleted"));
                Assert.That(File.Exists(testFile), Is.False, "File should be deleted");
            });
        }

        [Test]
        public void Delete_WithVariableFilenameAndDirectory_ShouldResolveAndDelete()
        {
            // Arrange - Create a test file
            string testFile = Path.Combine(testDirectory!, "var_file.txt");
            File.WriteAllText(testFile, "Test content");

            engine!.RegisterVariable("filename", "var_file.txt");
            engine.RegisterVariable("directory", testDirectory!);
            ProcessedPrompt prompt = new("DELETE [filename] FROM [directory].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result as string, Does.Contain("Deleted"));
                Assert.That(File.Exists(testFile), Is.False, "File should be deleted");
            });
        }

        #endregion Variable Support

        #region Reference Support

        [Test]
        public void Delete_WithReference_ShouldResolveAndDelete()
        {
            // Arrange - Create a test file
            string testFile = Path.Combine(testDirectory!, "reference_test.txt");
            File.WriteAllText(testFile, "Test content");

            ProcessedPrompt prompt = new($"DELETE {{{testFile}}}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result as string, Does.Contain("Deleted"));
                Assert.That(File.Exists(testFile), Is.False, "File should be deleted");
            });
        }

        #endregion Reference Support

        #region Multiple Files

        [Test]
        public void Delete_MultipleFiles_ShouldDeleteEachFile()
        {
            // Arrange - Create multiple test files
            string file1 = Path.Combine(testDirectory!, "file1.txt");
            string file2 = Path.Combine(testDirectory!, "file2.txt");
            File.WriteAllText(file1, "Test 1");
            File.WriteAllText(file2, "Test 2");

            // Act - Delete files one by one
            (ValidationResult validation1, _, object? result1) = engine!.Run(new ProcessedPrompt($"DELETE {file1}."));
            (ValidationResult validation2, _, object? result2) = engine.Run(new ProcessedPrompt($"DELETE {file2}."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation1.IsValid, Is.True);
                Assert.That(validation2.IsValid, Is.True);
                Assert.That(result1 as string, Does.Contain("Deleted"));
                Assert.That(result2 as string, Does.Contain("Deleted"));
                Assert.That(File.Exists(file1), Is.False, "File 1 should be deleted");
                Assert.That(File.Exists(file2), Is.False, "File 2 should be deleted");
            });
        }

        #endregion Multiple Files

        #region Edge Cases

        [Test]
        public void Delete_WithSpacesInPath_ShouldHandleCorrectly()
        {
            // Arrange - Create a file with spaces in name
            string testFile = Path.Combine(testDirectory!, "file with spaces.txt");
            File.WriteAllText(testFile, "Test content");

            ProcessedPrompt prompt = new($"DELETE {testFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result as string, Does.Contain("Deleted"));
                Assert.That(File.Exists(testFile), Is.False, "File with spaces should be deleted");
            });
        }

        [Test]
        public void Delete_WithSpecialCharacters_ShouldHandleCorrectly()
        {
            // Arrange - Create a file with special chars (avoiding invalid chars)
            string testFile = Path.Combine(testDirectory!, "file-test_123.txt");
            File.WriteAllText(testFile, "Test content");

            ProcessedPrompt prompt = new($"DELETE {testFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result as string, Does.Contain("Deleted"));
                Assert.That(File.Exists(testFile), Is.False, "File with special chars should be deleted");
            });
        }

        #endregion Edge Cases
    }
}