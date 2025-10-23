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
    /// Tests for automatic object property destructuring.
    /// Example: GET [{name, surname, age}] FROM {file} extracts properties into individual variables
    /// </summary>
    [TestFixture]
    public class ObjectDestructuringTests
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
            services.AddTransient<DiscoveryService>();
            services.AddScoped<Engine>();
            services.AddScoped<TokenTreeFactory>();
            services.AddScoped<TokenFactory>();
            services.AddScoped<Lexicon.Lexicon>();
            services.AddScoped<WordFactory>();
            services.AddScoped<SentenceValidator>();
            services.AddScoped<SentenceFactory>();
            services.AddPatternMatchers(); // Register pattern matchers (regex and string-based)
            services.AddScoped<IVariableResolver, VariableResolver>();
            services.AddScoped<SentenceExecutor>();
            services.AddTransient<Execution.ExecutionPipelineFactory>();

            serviceProvider = services.BuildServiceProvider();
            scope = serviceProvider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();

            // Create test directory
            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_ObjectDestructuring_" + Guid.NewGuid().ToString("N"));
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
        public void Destructure_JsonFile_SimpleProperties_ShouldExtractIndividualVariables()
        {
            // Arrange - Create JSON file with user data
            string userFile = Path.Combine(testDirectory!, "user.json");
            File.WriteAllText(userFile, @"{
    ""name"": ""John"",
    ""surname"": ""Doe"",
    ""age"": 30
}");

            // Act - Use destructuring syntax to extract properties
            (ValidationResult validation, _, _) = engine!.Run(
                new ProcessedPrompt($"GET [{{name, surname, age}}] FROM {userFile}."));

            // Then access individual variables via SAY (converts to string)
            (ValidationResult val1, _, object? result1) = engine.Run(new ProcessedPrompt("SAY [name]."));
            (ValidationResult val2, _, object? result2) = engine.Run(new ProcessedPrompt("SAY [surname]."));
            (ValidationResult val3, _, object? result3) = engine.Run(new ProcessedPrompt("SAY [age]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(val1.IsValid, Is.True);
                Assert.That(val2.IsValid, Is.True);
                Assert.That(val3.IsValid, Is.True);

                // SAY command converts values to strings for display
                Assert.That(result1, Is.EqualTo("John"));
                Assert.That(result2, Is.EqualTo("Doe"));
                Assert.That(result3, Is.EqualTo("30")); // Number converted to string by SAY
            });
        }

        [Test]
        public void Destructure_WithThenClause_ShouldAccessPropertiesInChain()
        {
            // Arrange - Create JSON file
            string personFile = Path.Combine(testDirectory!, "person.json");
            File.WriteAllText(personFile, @"{
    ""firstName"": ""Alice"",
    ""lastName"": ""Smith"",
    ""email"": ""alice@example.com""
}");

            // Capture console output
            var originalOut = Console.Out;
            using var stringWriter = new StringWriter();
            try
            {
                Console.SetOut(stringWriter);

                // Act - Use THEN clause to chain operations
                (ValidationResult validation, _, _) = engine!.Run(
                    new ProcessedPrompt($"GET [{{firstName, lastName, email}}] FROM {personFile} THEN SAY [firstName] THEN SAY [lastName] THEN SAY [email]."));

                string output = stringWriter.ToString();

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                    Assert.That(output, Does.Contain("Alice"));
                    Assert.That(output, Does.Contain("Smith"));
                    Assert.That(output, Does.Contain("alice@example.com"));
                });
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Test]
        public void Destructure_PartialProperties_ShouldExtractOnlySpecified()
        {
            // Arrange - JSON with many properties but only extract some
            string configFile = Path.Combine(testDirectory!, "config.json");
            File.WriteAllText(configFile, @"{
    ""name"": ""MyApp"",
    ""version"": ""1.0.0"",
    ""author"": ""Developer"",
    ""license"": ""MIT"",
    ""description"": ""A test application""
}");

            // Act - Only extract name and version
            (ValidationResult validation, _, _) = engine!.Run(
                new ProcessedPrompt($"GET [{{name, version}}] FROM {configFile}."));

            (ValidationResult val1, _, object? result1) = engine.Run(new ProcessedPrompt("SAY [name]."));
            (ValidationResult val2, _, object? result2) = engine.Run(new ProcessedPrompt("SAY [version]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result1, Is.EqualTo("MyApp"));
                Assert.That(result2, Is.EqualTo("1.0.0"));
            });
        }

        [Test]
        public void Destructure_FileWithoutExtension_ShouldAutoDetectJson()
        {
            // Arrange - File without extension containing JSON
            string dataFile = Path.Combine(testDirectory!, "userdata");
            File.WriteAllText(dataFile, @"{
    ""username"": ""bob123"",
    ""role"": ""admin"",
    ""active"": true
}");

            // Act - Load file without extension
            (ValidationResult validation, _, _) = engine!.Run(
                new ProcessedPrompt($"GET [{{username, role}}] FROM {dataFile}."));

            (ValidationResult val1, _, object? result1) = engine.Run(new ProcessedPrompt("SAY [username]."));
            (ValidationResult val2, _, object? result2) = engine.Run(new ProcessedPrompt("SAY [role]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result1, Is.EqualTo("bob123"));
                Assert.That(result2, Is.EqualTo("admin"));
            });
        }

        [Test]
        public void Destructure_NestedObject_ShouldExtractTopLevelProperties()
        {
            // Arrange - JSON with nested structure
            string profileFile = Path.Combine(testDirectory!, "profile.json");
            File.WriteAllText(profileFile, @"{
    ""id"": 123,
    ""name"": ""Charlie"",
    ""address"": {
        ""street"": ""123 Main St"",
        ""city"": ""Springfield""
    }
}");

            // Act - Extract top-level properties (including nested object)
            (ValidationResult validation, _, _) = engine!.Run(
                new ProcessedPrompt($"GET [{{id, name}}] FROM {profileFile}."));

            (ValidationResult val1, _, object? result1) = engine.Run(new ProcessedPrompt("SAY [id]."));
            (ValidationResult val2, _, object? result2) = engine.Run(new ProcessedPrompt("SAY [name]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result1, Is.EqualTo("123")); // SAY converts to string
                Assert.That(result2, Is.EqualTo("Charlie"));
            });
        }

        [Test]
        public void Destructure_MixedTypes_ShouldPreserveTypes()
        {
            // Arrange - JSON with different value types
            string dataFile = Path.Combine(testDirectory!, "mixed.json");
            File.WriteAllText(dataFile, @"{
    ""title"": ""Test"",
    ""count"": 42,
    ""enabled"": true,
    ""score"": 98.5
}");

            // Act
            (ValidationResult validation, _, _) = engine!.Run(
                new ProcessedPrompt($"GET [{{title, count, enabled, score}}] FROM {dataFile}."));

            (_, _, object? result1) = engine.Run(new ProcessedPrompt("SAY [title]."));
            (_, _, object? result2) = engine.Run(new ProcessedPrompt("SAY [count]."));
            (_, _, object? result3) = engine.Run(new ProcessedPrompt("SAY [enabled]."));
            (_, _, object? result4) = engine.Run(new ProcessedPrompt("SAY [score]."));

            // Assert - SAY command converts all values to strings for display
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result1, Is.EqualTo("Test"));
                Assert.That(result2, Is.EqualTo("42"));
                Assert.That(result3, Is.EqualTo("True"));
                Assert.That(result4, Is.EqualTo("98.5"));
            });
        }

        [Test]
        public void Destructure_SaveExtractedProperty_ShouldWork()
        {
            // Arrange
            string sourceFile = Path.Combine(testDirectory!, "source.json");
            File.WriteAllText(sourceFile, @"{
    ""message"": ""Hello World"",
    ""timestamp"": ""2024-01-01""
}");

            string outputFile = Path.Combine(testDirectory!, "output.txt");

            // Act - Extract and save a property
            (ValidationResult validation, _, _) = engine!.Run(
                new ProcessedPrompt($"GET [{{message, timestamp}}] FROM {sourceFile} THEN SAVE [message] TO {outputFile}."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(outputFile), Is.True);
                string savedContent = File.ReadAllText(outputFile);
                Assert.That(savedContent, Is.EqualTo("Hello World"));
            });
        }

        [Test]
        public void Destructure_CaseInsensitive_ShouldMatchProperties()
        {
            // Arrange - JSON with camelCase
            string file = Path.Combine(testDirectory!, "data.json");
            File.WriteAllText(file, @"{
    ""userName"": ""testuser"",
    ""emailAddress"": ""test@example.com""
}");

            // Act - Use exact casing to match JSON property names
            (ValidationResult validation, _, _) = engine!.Run(
                new ProcessedPrompt($"GET [{{userName, emailAddress}}] FROM {file}."));

            // Variables are stored case-insensitively, can retrieve with any casing
            (_, _, object? result1) = engine.Run(new ProcessedPrompt("SAY [username]."));
            (_, _, object? result2) = engine.Run(new ProcessedPrompt("SAY [emailaddress]."));

            // Assert - Variables stored case-insensitively
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result1, Is.EqualTo("testuser"));
                Assert.That(result2, Is.EqualTo("test@example.com"));
            });
        }

        [Test]
        public void Destructure_NonExistentProperty_ShouldOnlyExtractExisting()
        {
            // Arrange
            string file = Path.Combine(testDirectory!, "partial.json");
            File.WriteAllText(file, @"{
    ""name"": ""Test"",
    ""value"": 100
}");

            // Act - Request property that doesn't exist
            (ValidationResult validation, _, _) = engine!.Run(
                new ProcessedPrompt($"GET [{{name, value, missing}}] FROM {file}."));

            (_, _, object? result1) = engine.Run(new ProcessedPrompt("SAY [name]."));
            (_, _, object? result2) = engine.Run(new ProcessedPrompt("SAY [value]."));

            // Assert - Should extract only existing properties
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result1, Is.EqualTo("Test"));
                Assert.That(result2, Is.EqualTo("100")); // SAY converts to string
            });
        }
    }
}
