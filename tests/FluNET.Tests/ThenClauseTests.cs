using FluNET;
using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests
{
    /// <summary>
    /// Tests for THEN clause sentence chaining functionality.
    /// THEN allows multiple commands to be executed in sequence with shared variable context.
    /// Example: DOWNLOAD [file] FROM url TO {file.txt} THEN SAY [file].
    /// </summary>
    [TestFixture]
    public class ThenClauseTests
    {
        private ServiceProvider? serviceProvider;
        private IServiceScope? scope;
        private Engine? engine;
        private string? testDirectory;

        [SetUp]
        public void Setup()
        {
            // Create test directory
            testDirectory = Path.Combine(Path.GetTempPath(), $"FluNET_ThenClause_{Guid.NewGuid()}");
            Directory.CreateDirectory(testDirectory);

            // Configure dependency injection
            ServiceCollection services = new();
            services.AddTransient<DiscoveryService>();
            services.AddScoped<Engine>();
            services.AddScoped<TokenTreeFactory>();
            services.AddScoped<TokenFactory>();
            services.AddScoped<Lexicon.Lexicon>();
            services.AddScoped<WordFactory>();
            services.AddScoped<SentenceValidator>();
            services.AddScoped<SentenceFactory>();
            services.AddScoped<IVariableResolver, VariableResolver>();
            services.AddScoped<SentenceExecutor>();

            serviceProvider = services.BuildServiceProvider();
            scope = serviceProvider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();
        }

        [TearDown]
        public void TearDown()
        {
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

            scope?.Dispose();
            serviceProvider?.Dispose();
        }

        #region Basic THEN Clause Tests

        [Test]
        public void ThenClause_GetThenSay_ShouldExecuteBothCommands()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "test.txt");
            File.WriteAllText(testFile, "Hello World");

            // Act - Chain GET and SAY with THEN
            (ValidationResult validation, _, object? result) =
                engine!.Run(new ProcessedPrompt($"GET [content] FROM {testFile} THEN SAY [content]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Does.Contain("Hello World"));
            });
        }

        [Test]
        public void ThenClause_SaveThenLoad_ShouldChainCommands()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "output.txt");

            // Act - Chain SAVE and LOAD with THEN
            (ValidationResult validation, _, object? result) =
                engine!.Run(new ProcessedPrompt($"SAVE \"Test Content\" TO {testFile} THEN GET [result] FROM {testFile}."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(testFile), Is.True);
                Assert.That(result, Is.Not.Null);
                // Result from GET will be string[]
                Assert.That(result, Is.InstanceOf<string[]>());
                string[] lines = (string[])result!;
                Assert.That(lines, Has.Length.GreaterThan(0));
                Assert.That(string.Join("", lines), Does.Contain("Test Content"));
            });
        }

        [Test]
        public void ThenClause_MultipleChains_ShouldExecuteInSequence()
        {
            // Arrange
            string file1 = Path.Combine(testDirectory!, "file1.txt");
            string file2 = Path.Combine(testDirectory!, "file2.txt");
            File.WriteAllText(file1, "First");

            // Act - Chain three commands: GET THEN SAVE THEN SAY
            (ValidationResult validation, _, object? result) =
                engine!.Run(new ProcessedPrompt($"GET [data] FROM {file1} THEN SAVE [data] TO {file2} THEN SAY [data]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(file2), Is.True);
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public void ThenClause_VariableSharing_ShouldMaintainContext()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "shared.txt");
            File.WriteAllText(testFile, "Shared Data");

            // Act - First command stores in variable, second uses it
            (ValidationResult validation, _, object? result) =
                engine!.Run(new ProcessedPrompt($"GET [shared] FROM {testFile} THEN SAY [shared]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Does.Contain("Shared Data"));
            });
        }

        #endregion

        #region Error Handling

        [Test]
        public void ThenClause_FirstCommandFails_ShouldReturnError()
        {
            // Arrange - Non-existent file
            string nonExistentFile = Path.Combine(testDirectory!, "nonexistent.txt");

            // Act
            (ValidationResult validation, _, object? result) = 
                engine!.Run(new ProcessedPrompt($"GET [data] FROM {nonExistentFile} THEN SAY [data]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("Variable [data] not found"));
            });
        }

        [Test]
        public void ThenClause_SecondCommandFails_ShouldReturnError()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "test.txt");
            File.WriteAllText(testFile, "Data");

            // Act - Second command references non-existent variable
            (ValidationResult validation, _, object? result) = 
                engine!.Run(new ProcessedPrompt($"GET [data] FROM {testFile} THEN SAY [nonexistent]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("Variable [nonexistent] not found"));
            });
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ThenClause_EmptyAfterThen_ShouldFail()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "test.txt");
            File.WriteAllText(testFile, "Data");

            // Act - THEN without following command
            (ValidationResult validation, _, _) =
                engine!.Run(new ProcessedPrompt($"GET [data] FROM {testFile} THEN ."));

            // Assert
            Assert.That(validation.IsValid, Is.False);
        }

        [Test]
        public void ThenClause_OnlyThen_ShouldFail()
        {
            // Act
            (ValidationResult validation, _, _) =
                engine!.Run(new ProcessedPrompt("THEN."));

            // Assert
            Assert.That(validation.IsValid, Is.False);
        }

        #endregion
    }
}
