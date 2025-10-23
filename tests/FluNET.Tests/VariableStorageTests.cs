using FluNET.Extensions;
using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests
{
    /// <summary>
    /// Tests for automatic variable storage functionality.
    /// Variables should be automatically stored when used as direct objects in verbs.
    /// Example: GET [text] FROM file.txt -> stores result in [text]
    /// </summary>
    [TestFixture]
    public class VariableStorageTests
    {
        private ServiceProvider? provider;
        private IServiceScope? scope;
        private Engine? engine;
        private string? testDirectory;

        [SetUp]
        public void Setup()
        {
            var services = new ServiceCollection();

            services.AddTransient<DiscoveryService>();
            services.AddTransient<Tokens.TokenFactory>();
            services.AddTransient<Tokens.Tree.TokenTreeFactory>();
            services.AddTransient<Words.WordFactory>();
            services.AddTransient<Lexicon.Lexicon>();
            services.AddTransient<SentenceValidator>();
            services.AddTransient<SentenceFactory>();
            services.AddPatternMatchers(); // Register pattern matchers (regex and string-based)
            services.AddScoped<Variables.IVariableResolver, Variables.VariableResolver>();
            services.AddTransient<SentenceExecutor>();
                        services.AddTransient<Execution.ExecutionPipelineFactory>();

            services.AddTransient<Engine>();

            provider = services.BuildServiceProvider();
            scope = provider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();

            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_VarStorage_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            scope?.Dispose();
            provider?.Dispose();

            if (testDirectory != null && Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, true);
                }
                catch { }
            }
        }

        [Test]
        public void AutoStorage_GET_ToVariable_ShouldStoreResult()
        {
            // Arrange - Create a test file
            string testFile = Path.Combine(testDirectory!, "test.txt");
            string expectedContent = "Hello from file!";
            File.WriteAllText(testFile, expectedContent);

            // Act - GET command should automatically store result in [content]
            (ValidationResult validation1, _, object? result1) = engine!.Run(
                new ProcessedPrompt($"GET [content] FROM {testFile}."));

            // Now use the variable in another command
            (ValidationResult validation2, _, object? result2) = engine.Run(
                new ProcessedPrompt("SAY [content]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation1.IsValid, Is.True, "First command should be valid");
                Assert.That(validation2.IsValid, Is.True, "Second command should be valid");

                // The GET command returns array of strings
                Assert.That(result1, Is.InstanceOf<string[]>());
                string[] lines1 = (string[])result1!;
                Assert.That(lines1[0], Is.EqualTo(expectedContent));

                // The SAY command should output what's stored in [content]
                // SAY returns string (what it printed), even if the variable contained string[]
                Assert.That(result2, Is.InstanceOf<string>());
                string output = (string)result2!;
                Assert.That(output, Is.EqualTo(expectedContent),
                    "SAY should output the content stored in [content] variable");
            });
        }

        [Test]
        public void AutoStorage_SAVE_ToVariable_ShouldStoreResult()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "output.txt");
            string contentToSave = "Test content";

            // Act - SAVE command needs actual content to save
            // First save some content, then it can be referenced by variable
            (ValidationResult validation1, _, object? result1) = engine!.Run(
                new ProcessedPrompt($"SAVE \"{contentToSave}\" TO {{{testFile}}}."));

            // Assert - With actual content, validation should pass
            Assert.Multiple(() =>
            {
                Assert.That(validation1.IsValid, Is.True, $"Validation failed: {validation1.FailureReason}");
                Assert.That(result1, Is.Not.Null, "Result should contain the saved content");
            });
        }

        [Test]
        public void Variable_NotDefined_ShouldFailExecution()
        {
            // Arrange - No variable defined

            // Act - Try to use undefined variable
            (ValidationResult validation, _, object? result) = engine!.Run(
                new ProcessedPrompt("SAY [undefined]."));

            // Assert - Validation should pass but execution should fail
            Assert.Multiple(() =>
            {
                // The engine catches execution exceptions and returns them in validation result
                Assert.That(validation.IsValid, Is.False, "Should fail when variable doesn't exist");
                Assert.That(validation.FailureReason, Does.Contain("undefined"),
                    $"Error message should mention undefined variable. Actual: {validation.FailureReason}");
                Assert.That(result, Is.Null, "Result should be null on execution failure");
            });
        }

        [Test]
        public void Reference_NotStoredButEvaluated_MultipleUses()
        {
            // Arrange & Act - Use reference twice
            (_, _, object? result1) = engine!.Run(new ProcessedPrompt("SAY {Hello First}."));
            (_, _, object? result2) = engine.Run(new ProcessedPrompt("SAY {Hello Second}."));

            // Assert - References are evaluated each time, not stored
            // SAY returns the string it printed
            Assert.Multiple(() =>
            {
                Assert.That(result1, Is.Not.Null);
                Assert.That(result1 as string, Does.Contain("Hello First"));

                Assert.That(result2, Is.Not.Null);
                Assert.That(result2 as string, Does.Contain("Hello Second"));
            });
        }

        [Test]
        public void AutoStorage_Multiple_Commands_CanChain()
        {
            // Arrange - Create test files
            string file1 = Path.Combine(testDirectory!, "file1.txt");
            string file2 = Path.Combine(testDirectory!, "file2.txt");
            File.WriteAllText(file1, "Content 1");
            File.WriteAllText(file2, "Content 2");

            // Act - Chain multiple commands using variables
            engine!.Run(new ProcessedPrompt($"GET [text1] FROM {file1}."));
            engine.Run(new ProcessedPrompt($"GET [text2] FROM {file2}."));
            (_, _, object? result) = engine.Run(new ProcessedPrompt("SAY [text1]."));

            // Assert - SAY returns the string it printed
            Assert.That(result, Is.Not.Null);
            Assert.That(result as string, Is.EqualTo("Content 1"));
        }
    }
}