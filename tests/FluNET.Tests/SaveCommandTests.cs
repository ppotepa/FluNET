using FluNET.Context;
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
    /// Test cases for the SAVE command.
    /// These tests serve as both verification and usage examples.
    /// Usage: SAVE [text] TO [output.txt]
    /// </summary>
    [TestFixture]
    public class SaveCommandTests
    {
        private ServiceProvider? provider;
        private IServiceScope? scope;
        private Engine? engine;
        private string? testDirectory;

        [SetUp]
        public void Setup()
        {
            var services = new ServiceCollection();
            FluNETContext.ConfigureDefaultServices(services);

            provider = services.BuildServiceProvider();
            scope = provider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();

            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_SaveTests_" + Guid.NewGuid().ToString());
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
        public void Save_BasicText_ShouldCreateFile()
        {
            // Arrange
            string outputFile = Path.Combine(testDirectory!, "output.txt");
            string text = "Hello, World!";

            ProcessedPrompt prompt = new($"SAVE {text} TO {outputFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<SaveText>());
                Assert.That(result as string, Is.EqualTo(text));
                Assert.That(File.Exists(outputFile), Is.True, "File should be created");
                Assert.That(File.ReadAllText(outputFile), Is.EqualTo(text));
            });
        }

        [Test]
        public void Save_MultiLineText_ShouldPreserveFormatting()
        {
            // Arrange
            string outputFile = Path.Combine(testDirectory!, "multiline.txt");
            string text = "Line 1\nLine 2\nLine 3";

            ProcessedPrompt prompt = new($"SAVE {text} TO {outputFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(outputFile), Is.True);
                Assert.That(File.ReadAllText(outputFile), Is.EqualTo(text));
            });
        }

        [Test]
        public void Save_WithVariable_ShouldResolveAndSave()
        {
            // Arrange
            string outputFile = Path.Combine(testDirectory!, "variable.txt");
            string content = "Content from variable";

            engine!.RegisterVariable("content", content);
            engine.RegisterVariable("filepath", outputFile);

            ProcessedPrompt prompt = new("SAVE [content] TO [filepath].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(File.Exists(outputFile), Is.True);
                Assert.That(File.ReadAllText(outputFile), Is.EqualTo(content));
            });
        }

        [Test]
        public void Save_WithReference_ShouldResolveAndSave()
        {
            // Arrange
            string outputFile = Path.Combine(testDirectory!, "reference.txt");
            string text = "Reference text";

            ProcessedPrompt prompt = new($"SAVE {{{text}}} TO {outputFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(File.Exists(outputFile), Is.True);
            });
        }

        [Test]
        public void Save_OverwriteExisting_ShouldReplaceContent()
        {
            // Arrange
            string outputFile = Path.Combine(testDirectory!, "overwrite.txt");
            File.WriteAllText(outputFile, "Original content");

            string newContent = "New content";
            ProcessedPrompt prompt = new($"SAVE {newContent} TO {outputFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.ReadAllText(outputFile), Is.EqualTo(newContent), "Should overwrite original");
            });
        }

        [Test]
        public void Save_SpecialCharacters_ShouldHandleCorrectly()
        {
            // Arrange
            string outputFile = Path.Combine(testDirectory!, "special.txt");
            string text = "Special: !@#$%^&*()_+-=[]{}|;':\",./<>?";

            ProcessedPrompt prompt = new($"SAVE {text} TO {outputFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(outputFile), Is.True);
                Assert.That(File.ReadAllText(outputFile), Is.EqualTo(text));
            });
        }

        [Test]
        public void Save_EmptyString_ShouldCreateEmptyFile()
        {
            // Arrange
            string outputFile = Path.Combine(testDirectory!, "empty.txt");

            ProcessedPrompt prompt = new($"SAVE \"\" TO {outputFile}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(outputFile), Is.True);
                Assert.That(File.ReadAllText(outputFile), Is.Empty);
            });
        }
    }
}