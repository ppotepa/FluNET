using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Syntax.Verbs;
using FluNET.Tokens.Tree;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests
{
    /// <summary>
    /// Test cases for the LOAD command.
    /// These tests serve as both verification and usage examples.
    /// Usage: LOAD [config] FROM [settings.json]
    /// </summary>
    [TestFixture]
    public class LoadCommandTests
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
            services.AddTransient<TokenTreeFactory>();
            services.AddTransient<Words.WordFactory>();
            services.AddTransient<Lexicon.Lexicon>();
            services.AddTransient<SentenceValidator>();
            services.AddTransient<SentenceFactory>();
            services.AddScoped<Variables.IVariableResolver, Variables.VariableResolver>();
            services.AddTransient<SentenceExecutor>();
            services.AddTransient<Engine>();

            provider = services.BuildServiceProvider();
            scope = provider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();

            // Create temporary test directory with a config file
            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_LoadTests_" + Guid.NewGuid().ToString());
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
        public void Load_BasicConfigFile_ShouldLoadConfiguration()
        {
            // Arrange
            string configFile = Path.Combine(testDirectory!, "config.json");
            File.WriteAllText(configFile, "{\"setting1\":\"value1\",\"setting2\":42}");

            ProcessedPrompt prompt = new($"LOAD config FROM {configFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<LoadConfig>());
                Assert.That(result, Is.InstanceOf<Dictionary<string, object>>());

                var config = result as Dictionary<string, object>;
                Assert.That(config, Is.Not.Null);
                Assert.That(config!["loaded"], Is.EqualTo(true));
                Assert.That(config["source"], Is.EqualTo("config.json"));
            });
        }

        [Test]
        public void Load_WithVariable_ShouldResolveAndLoad()
        {
            // Arrange
            string configFile = Path.Combine(testDirectory!, "settings.json");
            File.WriteAllText(configFile, "{\"key\":\"value\"}");

            engine!.RegisterVariable("configpath", configFile);
            ProcessedPrompt prompt = new("LOAD config FROM [configpath].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<Dictionary<string, object>>());
            });
        }

        [Test]
        public void Load_WithReference_ShouldResolveAndLoad()
        {
            // Arrange - LOAD config from reference path (using braces for file path)
            string configFile = Path.Combine(testDirectory!, "app.json");
            File.WriteAllText(configFile, "{\"data\":\"test\"}");

            // Use reference syntax {path} to specify file location
            ProcessedPrompt prompt = new($"LOAD config FROM {configFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<Dictionary<string, object>>());
            });
        }

        [Test]
        public void Load_MultipleVariables_ShouldResolveAll()
        {
            // Arrange
            string configFile = Path.Combine(testDirectory!, "config.json");
            File.WriteAllText(configFile, "{\"test\":true}");

            engine!.RegisterVariable("configname", "config");
            engine.RegisterVariable("filepath", configFile);
            ProcessedPrompt prompt = new("LOAD [configname] FROM [filepath].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result, Is.InstanceOf<Dictionary<string, object>>());
            });
        }
    }
}