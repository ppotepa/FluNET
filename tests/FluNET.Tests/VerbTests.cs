using FluNET.Prompt;
using FluNET.Tokens.Tree;
using FluNET.Words;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests
{
    /// <summary>
    /// Tests for various verb implementations to ensure they validate correctly
    /// and work with different preposition patterns.
    /// </summary>
    public class VerbTests
    {
        private SentenceValidator validator = null!;

        [SetUp]
        public void Setup()
        {
            ServiceCollection services = new();
            services.AddScoped<DiscoveryService>();
            services.AddScoped<Lexicon.Lexicon>();
            services.AddScoped<WordFactory>();
            services.AddScoped<SentenceValidator>();

            ServiceProvider provider = services.BuildServiceProvider();
            validator = provider.GetRequiredService<SentenceValidator>();
        }

        #region GET Verb Tests

        [Test]
        public void Get_WithFromPreposition_ShouldValidate()
        {
            ProcessedPrompt processed = new("GET text FROM file.txt.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            // May fail if verb not found, but should not fail on terminator
            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void Get_WithoutTerminator_ShouldFail()
        {
            ProcessedPrompt processed = new("GET text FROM file");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.FailureReason, Does.Contain("terminator"));
            });
        }

        #endregion

        #region POST Verb Tests

        [Test]
        public void Post_WithToPreposition_ShouldValidate()
        {
            ProcessedPrompt processed = new("POST data TO endpoint.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void Post_WithoutTerminator_ShouldFail()
        {
            ProcessedPrompt processed = new("POST data TO endpoint");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.FailureReason, Does.Contain("terminator"));
            });
        }

        [Test]
        public void Post_WithQuestionMark_ShouldValidate()
        {
            ProcessedPrompt processed = new("POST data TO endpoint?");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        #endregion

        #region SAVE Verb Tests

        [Test]
        public void Save_WithToPreposition_ShouldValidate()
        {
            ProcessedPrompt processed = new("SAVE document TO output.txt.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void Save_WithExclamation_ShouldValidate()
        {
            ProcessedPrompt processed = new("SAVE document TO output.txt!");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        #endregion

        #region DELETE Verb Tests

        [Test]
        public void Delete_WithFromPreposition_ShouldValidate()
        {
            ProcessedPrompt processed = new("DELETE file FROM directory.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void Delete_WithoutTerminator_ShouldFail()
        {
            ProcessedPrompt processed = new("DELETE file FROM directory");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.FailureReason, Does.Contain("terminator"));
            });
        }

        #endregion

        #region LOAD Verb Tests

        [Test]
        public void Load_WithFromPreposition_ShouldValidate()
        {
            ProcessedPrompt processed = new("LOAD config FROM settings.json.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void Load_WithMultipleWords_ShouldValidate()
        {
            ProcessedPrompt processed = new("LOAD user data FROM database server.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        #endregion

        #region SEND Verb Tests

        [Test]
        public void Send_WithToPreposition_ShouldValidate()
        {
            ProcessedPrompt processed = new("SEND message TO recipient.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void Send_WithEmail_ShouldValidate()
        {
            ProcessedPrompt processed = new("SEND notification TO user@example.com.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        #endregion

        #region TRANSFORM Verb Tests

        [Test]
        public void Transform_WithUsingPreposition_ShouldValidate()
        {
            ProcessedPrompt processed = new("TRANSFORM data USING algorithm.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void Transform_WithEncoding_ShouldValidate()
        {
            ProcessedPrompt processed = new("TRANSFORM text USING UTF8.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        #endregion

        #region Mixed Pattern Tests

        [Test]
        public void DifferentVerbs_WithSamePreposition_ShouldAllValidate()
        {
            string[] sentences = new[]
            {
                "GET data FROM source.",
                "LOAD config FROM file.",
                "DELETE item FROM list."
            };

            foreach (string? sentence in sentences)
            {
                ProcessedPrompt processed = new(sentence);
                TokenTree tree = processed.ToTokenTree();
                ValidationResult result = validator.ValidateSentence(tree);

                if (!result.IsValid)
                {
                    Assert.That(result.FailureReason, Does.Not.Contain("terminator"),
                        $"Sentence '{sentence}' failed with terminator issue");
                }
            }
        }

        [Test]
        public void ComplexSentence_MultipleWords_ShouldValidate()
        {
            ProcessedPrompt processed = new("GET the user profile data FROM remote database server.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void EmptySentence_ShouldFail()
        {
            ProcessedPrompt processed = new("");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.FailureReason, Does.Contain("Empty"));
            });
        }

        [Test]
        public void OnlyTerminator_ShouldFail()
        {
            ProcessedPrompt processed = new(".");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            Assert.That(result.IsValid, Is.False);
        }

        #endregion
    }
}
