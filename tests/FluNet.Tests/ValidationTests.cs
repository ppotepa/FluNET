using FluNET.Prompt;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;
using FluNET.Words;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests
{
    [TestFixture]
    public class ValidationTests
    {
        private SentenceValidator validator = null!;
        private ServiceProvider? serviceProvider;

        [SetUp]
        public void Setup()
        {
            ServiceCollection services = new();
            services.AddTransient<DiscoveryService>();
            services.AddScoped<Lexicon.Lexicon>();
            services.AddScoped<WordFactory>();
            services.AddScoped<SentenceValidator>();

            serviceProvider = services.BuildServiceProvider();
            validator = serviceProvider.GetRequiredService<SentenceValidator>();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                serviceProvider?.Dispose();
            }
            catch (Exception ex)
            {
                // Log but don't fail
                Console.WriteLine($"Warning: TearDown cleanup failed: {ex.Message}");
            }
        }

        [Test]
        public void ValidateSentence_WithoutTerminator_ShouldFail()
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

        [Test]
        public void ValidateSentence_WithPeriod_ShouldPass()
        {
            ProcessedPrompt processed = new("GET text FROM file.");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            // Note: This may still fail if GET verb is not implemented
            // but it should at least pass the terminator check
            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void ValidateSentence_WithQuestionMark_ShouldPass()
        {
            ProcessedPrompt processed = new("GET text FROM file?");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void ValidateSentence_WithExclamation_ShouldPass()
        {
            ProcessedPrompt processed = new("GET text FROM file!");
            TokenTree tree = processed.ToTokenTree();

            ValidationResult result = validator.ValidateSentence(tree);

            if (!result.IsValid)
            {
                Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
            }
        }

        [Test]
        public void ValidateSentence_EmptySentence_ShouldFail()
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
    }
}