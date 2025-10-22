using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Syntax.Verbs;
using FluNET.Tokens.Tree;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace FluNET.Tests
{
    /// <summary>
    /// Test cases for the TRANSFORM command.
    /// These tests serve as both verification and usage examples.
    /// Usage: TRANSFORM [text] USING [UTF8]
    /// </summary>
    [TestFixture]
    public class TransformCommandTests
    {
        private ServiceProvider? provider;
        private IServiceScope? scope;
        private Engine? engine;

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
        }

        [TearDown]
        public void TearDown()
        {
            scope?.Dispose();
            provider?.Dispose();
        }

        [Test]
        public void Transform_UTF8Encoding_ShouldReturnBase64()
        {
            // Arrange
            string text = "Hello, World!";
            ProcessedPrompt prompt = new($"TRANSFORM {text} USING UTF8.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<TransformEncoding>());
                Assert.That(result as string, Is.Not.Null);

                // Verify the result is valid base64 encoded text
                string expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                Assert.That(result as string, Is.EqualTo(expectedBase64));
            });
        }

        [Test]
        public void Transform_ASCIIEncoding_ShouldReturnBase64()
        {
            // Arrange
            string text = "ASCII text";
            ProcessedPrompt prompt = new($"TRANSFORM {text} USING ASCII.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result as string, Is.Not.Null);

                string expectedBase64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(text));
                Assert.That(result as string, Is.EqualTo(expectedBase64));
            });
        }

        [Test]
        public void Transform_UnicodeEncoding_ShouldReturnBase64()
        {
            // Arrange
            string text = "Unicode: 你好世界";
            ProcessedPrompt prompt = new($"TRANSFORM {text} USING Unicode.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result as string, Is.Not.Null);

                string expectedBase64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(text));
                Assert.That(result as string, Is.EqualTo(expectedBase64));
            });
        }

        [Test]
        public void Transform_UTF32Encoding_ShouldReturnBase64()
        {
            // Arrange
            string text = "UTF32 text";
            ProcessedPrompt prompt = new($"TRANSFORM {text} USING UTF32.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result as string, Is.Not.Null);

                string expectedBase64 = Convert.ToBase64String(Encoding.UTF32.GetBytes(text));
                Assert.That(result as string, Is.EqualTo(expectedBase64));
            });
        }

        [Test]
        public void Transform_WithVariable_ShouldResolveAndTransform()
        {
            // Arrange
            string text = "Variable text";
            engine!.RegisterVariable("text", text);
            engine.RegisterVariable("encoding", "UTF8");

            ProcessedPrompt prompt = new("TRANSFORM [text] USING [encoding].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result as string, Is.Not.Null);

                string expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                Assert.That(result as string, Is.EqualTo(expectedBase64));
            });
        }

        [Test]
        public void Transform_WithReference_ShouldResolveAndTransform()
        {
            // Arrange
            ProcessedPrompt prompt = new("TRANSFORM {Reference text} USING UTF8.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result as string, Is.Not.Null);
            });
        }

        [Test]
        public void Transform_EmptyString_ShouldReturnEmptyBase64()
        {
            // Arrange
            ProcessedPrompt prompt = new("TRANSFORM \"\" USING UTF8.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result as string, Is.Not.Null);

                string expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(""));
                Assert.That(result as string, Is.EqualTo(expectedBase64));
            });
        }

        [Test]
        public void Transform_SpecialCharacters_ShouldEncodeCorrectly()
        {
            // Arrange
            string text = "Special: !@#$%^&*()_+-=[]{}|;':\",./<>?";
            ProcessedPrompt prompt = new($"TRANSFORM {text} USING UTF8.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result as string, Is.Not.Null);

                string expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                Assert.That(result as string, Is.EqualTo(expectedBase64));
            });
        }

        [Test]
        public void Transform_MultipleEncodings_ShouldProduceDifferentResults()
        {
            // Arrange
            string text = "Test text";

            // Act - Transform with different encodings
            (_, _, object? utf8Result) = engine!.Run(new ProcessedPrompt($"TRANSFORM {text} USING UTF8."));
            (_, _, object? utf32Result) = engine.Run(new ProcessedPrompt($"TRANSFORM {text} USING UTF32."));
            (_, _, object? asciiResult) = engine.Run(new ProcessedPrompt($"TRANSFORM {text} USING ASCII."));

            // Assert - Different encodings should produce different base64 strings
            Assert.Multiple(() =>
            {
                Assert.That(utf8Result as string, Is.Not.Null);
                Assert.That(utf32Result as string, Is.Not.Null);
                Assert.That(asciiResult as string, Is.Not.Null);

                // UTF8 and ASCII should be the same for pure ASCII text
                Assert.That(utf8Result as string, Is.EqualTo(asciiResult as string));

                // UTF32 should be different from UTF8 due to different byte representation
                Assert.That(utf32Result as string, Is.Not.EqualTo(utf8Result as string));
            });
        }

        [Test]
        public void Transform_LongText_ShouldHandleCorrectly()
        {
            // Arrange
            string longText = new string('A', 1000); // 1000 characters
            ProcessedPrompt prompt = new($"TRANSFORM {longText} USING UTF8.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result as string, Is.Not.Null);

                string expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(longText));
                Assert.That(result as string, Is.EqualTo(expectedBase64));
            });
        }
    }
}