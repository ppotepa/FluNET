using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Syntax.Verbs;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests
{
    [TestFixture]
    public class SayCommandTests
    {
        private IServiceScope? scope;
        private Engine? engine;

        [SetUp]
        public void Setup()
        {
            // Setup DI container - use Transient for DiscoveryService to ensure fresh assembly discovery per test
            IServiceCollection services = new ServiceCollection();
            services.AddTransient<DiscoveryService>();
            services.AddTransient<Tokens.TokenFactory>();
            services.AddTransient<Tokens.Tree.TokenTreeFactory>();
            services.AddTransient<Words.WordFactory>();
            services.AddTransient<Lexicon.Lexicon>();
            services.AddTransient<SentenceValidator>();
            services.AddTransient<SentenceFactory>();
            // VariableResolver must be Scoped so Engine and SentenceExecutor share the same instance
            services.AddScoped<Variables.IVariableResolver, Variables.VariableResolver>();
            services.AddTransient<SentenceExecutor>();
            services.AddTransient<Engine>();

            ServiceProvider provider = services.BuildServiceProvider();
            scope = provider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();
        }

        [TearDown]
        public void TearDown()
        {
            scope?.Dispose();
        }

        [Test]
        public void Say_SimpleLiteral_ShouldOutput()
        {
            // Arrange
            ProcessedPrompt prompt = new("SAY Hello World.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<SayText>());
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string>());
                Assert.That(result as string, Is.EqualTo("Hello World"));
            });
        }

        [Test]
        public void Say_EmptyMessage_ShouldBeValid()
        {
            // Arrange
            ProcessedPrompt prompt = new("SAY .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
                // Empty message should result in empty string
                Assert.That(result as string, Is.EqualTo(string.Empty).Or.EqualTo(""));
            });
        }

        [Test]
        public void Say_WithVariable_ShouldResolveAndOutput()
        {
            // Arrange
            engine!.RegisterVariable("greeting", "Hello from variable!");
            ProcessedPrompt prompt = new("SAY [greeting].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Is.EqualTo("Hello from variable!"));
            });
        }

        [Test]
        public void Say_WithReference_ShouldResolveAndOutput()
        {
            // Arrange
            ProcessedPrompt prompt = new("SAY {some reference value}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Is.EqualTo("some reference value"));
            });
        }

        [Test]
        public void Say_MultipleWords_ShouldOutputFullMessage()
        {
            // Arrange
            ProcessedPrompt prompt = new("SAY The quick brown fox jumps over the lazy dog.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Does.Contain("quick brown fox"));
            });
        }

        [Test]
        public void Echo_Synonym_ShouldWork()
        {
            // Arrange
            ProcessedPrompt prompt = new("ECHO Testing ECHO synonym.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<SayText>());
                Assert.That(result, Is.Not.Null);
                // ECHO in message text becomes SAY (since ECHO is a synonym of SAY)
                Assert.That(result as string, Does.Contain("Testing SAY synonym"));
            });
        }

        [Test]
        public void Print_Synonym_ShouldWork()
        {
            // Arrange
            ProcessedPrompt prompt = new("PRINT Testing PRINT synonym.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<SayText>());
                Assert.That(result, Is.Not.Null);
                // PRINT in message text becomes SAY (since PRINT is a synonym of SAY)
                Assert.That(result as string, Does.Contain("Testing SAY synonym"));
            });
        }

        [Test]
        public void Output_Synonym_ShouldWork()
        {
            // Arrange
            ProcessedPrompt prompt = new("OUTPUT Testing OUTPUT synonym.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<SayText>());
                Assert.That(result, Is.Not.Null);
                // OUTPUT in message text becomes SAY (since OUTPUT is a synonym of SAY)
                Assert.That(result as string, Does.Contain("Testing SAY synonym"));
            });
        }

        [Test]
        public void Write_Synonym_ShouldWork()
        {
            // Arrange
            ProcessedPrompt prompt = new("WRITE Testing WRITE synonym.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<SayText>());
                Assert.That(result, Is.Not.Null);
                // WRITE in message text becomes SAY (since WRITE is a synonym of SAY)
                Assert.That(result as string, Does.Contain("Testing SAY synonym"));
            });
        }

        [Test]
        public void Say_WithSpecialCharacters_ShouldOutput()
        {
            // Arrange
            ProcessedPrompt prompt = new("SAY Special chars: @#$%^&*().");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Does.Contain("@#$%^&*()"));
            });
        }

        [Test]
        public void Say_WithNumbers_ShouldOutput()
        {
            // Arrange
            ProcessedPrompt prompt = new("SAY The answer is 42.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Does.Contain("42"));
            });
        }

        [Test]
        public void Say_CaseSensitiveMessage_ShouldPreserveCase()
        {
            // Arrange
            ProcessedPrompt prompt = new("SAY MiXeD CaSe TeXt.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Does.Contain("MiXeD CaSe TeXt"));
            });
        }

        [Test]
        public void SayText_Validate_LiteralWord_ShouldReturnTrue()
        {
            // Arrange
            var sayText = new SayText("test");
            var literalWord = new Words.LiteralWord("Hello");

            // Act
            bool isValid = sayText.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void SayText_Validate_VariableWord_ShouldReturnTrue()
        {
            // Arrange
            var sayText = new SayText("test");
            var variableWord = new Words.VariableWord("[message]");

            // Act
            bool isValid = sayText.Validate(variableWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void SayText_Validate_ReferenceWord_ShouldReturnTrue()
        {
            // Arrange
            var sayText = new SayText("test");
            var referenceWord = new Words.ReferenceWord("{value}");

            // Act
            bool isValid = sayText.Validate(referenceWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void SayText_Act_ShouldReturnMessage()
        {
            // Arrange
            var sayText = new SayText("Test message");

            // Capture console output
            var originalOut = Console.Out;
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            try
            {
                // Act
                string result = sayText.Act("Test message");

                // Assert
                Assert.That(result, Is.EqualTo("Test message"));
                Assert.That(stringWriter.ToString().Trim(), Is.EqualTo("Test message"));
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Test]
        public void SayText_Synonyms_ShouldIncludeAllVariants()
        {
            // Arrange
            var sayText = new SayText("test");

            // Act
            var synonyms = sayText.Synonyms;

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(synonyms, Does.Contain("ECHO"));
                Assert.That(synonyms, Does.Contain("PRINT"));
                Assert.That(synonyms, Does.Contain("OUTPUT"));
                Assert.That(synonyms, Does.Contain("WRITE"));
                Assert.That(synonyms.Length, Is.EqualTo(4));
            });
        }
    }
}
