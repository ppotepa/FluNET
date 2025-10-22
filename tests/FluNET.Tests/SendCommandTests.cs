using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Syntax.Verbs;
using FluNET.Tokens.Tree;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests
{
    /// <summary>
    /// Test cases for the SEND command.
    /// These tests serve as both verification and usage examples.
    /// Usage: SEND [message] TO [recipient@example.com]
    /// Note: SendEmail is a simulated implementation that outputs to Debug console
    /// </summary>
    [TestFixture]
    public class SendCommandTests
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
        public void Send_BasicEmail_ShouldReturnConfirmation()
        {
            // Arrange
            string message = "Hello from FluNET!";
            string recipient = "user@example.com";

            ProcessedPrompt prompt = new($"SEND {message} TO {recipient}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<SendEmail>());
                Assert.That(result as string, Does.Contain("Email sent to"));
                Assert.That(result as string, Does.Contain(recipient));
            });
        }

        [Test]
        public void Send_WithVariable_ShouldResolveAndSend()
        {
            // Arrange
            engine!.RegisterVariable("message", "Test message");
            engine.RegisterVariable("recipient", "test@example.com");

            ProcessedPrompt prompt = new("SEND [message] TO [recipient].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result as string, Does.Contain("Email sent to"));
                Assert.That(result as string, Does.Contain("test@example.com"));
            });
        }

        [Test]
        public void Send_WithReference_ShouldResolveAndSend()
        {
            // Arrange
            ProcessedPrompt prompt = new("SEND {Important message} TO admin@example.com.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result as string, Does.Contain("Email sent to"));
            });
        }

        [Test]
        public void Send_LongMessage_ShouldHandleCorrectly()
        {
            // Arrange
            string longMessage = "This is a very long email message that contains multiple sentences. " +
                               "It simulates a real email that might be sent through the system. " +
                               "The system should handle this correctly.";
            string recipient = "recipient@example.com";

            ProcessedPrompt prompt = new($"SEND {longMessage} TO {recipient}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result as string, Does.Contain("Email sent to"));
                Assert.That(result as string, Does.Contain(recipient));
            });
        }

        [Test]
        public void Send_MultipleRecipients_ShouldSendToEachRecipient()
        {
            // Arrange - Send to multiple recipients (one at a time)
            string message = "Broadcast message";
            string[] recipients = { "user1@example.com", "user2@example.com", "user3@example.com" };

            // Act & Assert - Send to each recipient
            foreach (string recipient in recipients)
            {
                ProcessedPrompt prompt = new($"SEND {message} TO {recipient}.");
                (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

                Assert.Multiple(() =>
                {
                    Assert.That(validation.IsValid, Is.True, $"Failed for recipient: {recipient}");
                    Assert.That(result as string, Does.Contain(recipient));
                });
            }
        }

        [Test]
        public void Send_SpecialCharactersInMessage_ShouldHandleCorrectly()
        {
            // Arrange
            string message = "Special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?";
            string recipient = "test@example.com";

            ProcessedPrompt prompt = new($"SEND {message} TO {recipient}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result as string, Does.Contain("Email sent to"));
            });
        }

        [Test]
        public void Send_EmptyMessage_ShouldStillSend()
        {
            // Arrange
            ProcessedPrompt prompt = new("SEND \"\" TO user@example.com.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result as string, Does.Contain("Email sent to"));
            });
        }

        [Test]
        public void Send_MultipleVariables_ShouldResolveAll()
        {
            // Arrange
            engine!.RegisterVariable("greeting", "Hello there!");
            engine.RegisterVariable("email", "contact@example.com");

            ProcessedPrompt prompt = new("SEND [greeting] TO [email].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result as string, Does.Contain("Email sent to"));
                Assert.That(result as string, Does.Contain("contact@example.com"));
            });
        }
    }
}