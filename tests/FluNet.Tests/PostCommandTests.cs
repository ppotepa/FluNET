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
    /// Test cases for the POST command.
    /// These tests serve as both verification and usage examples.
    /// Usage: POST [json] TO [https://api.example.com/endpoint]
    /// </summary>
    [TestFixture]
    public class PostCommandTests
    {
        private ServiceProvider? provider;
        private IServiceScope? scope;
        private Engine? engine;

        [SetUp]
        public void Setup()
        {
            var services = new ServiceCollection();
            FluNetContext.ConfigureDefaultServices(services);

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
        public void Post_ValidJsonToEndpoint_ShouldConstructSentence()
        {
            // Arrange - Using httpbin.org as test endpoint (Note: actual HTTP call will happen)
            string json = "{\"name\":\"test\",\"value\":42}";
            string endpoint = "https://httpbin.org/post";

            ProcessedPrompt prompt = new($"POST {json} TO {endpoint}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(sentence!.Root, Is.InstanceOf<PostJson>());
                Assert.That(result, Is.Not.Null);
                // Response will be JSON string from httpbin.org
                Assert.That(result as string, Is.Not.Empty);
            });
        }

        [Test]
        public void Post_WithVariable_ShouldResolveAndPost()
        {
            // Arrange
            engine!.RegisterVariable("payload", "{\"test\":true}");
            engine.RegisterVariable("url", "https://httpbin.org/post");

            ProcessedPrompt prompt = new("POST [payload] TO [url].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public void Post_WithReference_ShouldResolveAndPost()
        {
            // Arrange
            string endpoint = "https://httpbin.org/post";
            ProcessedPrompt prompt = new($"POST {{{{\"ref\":\"data\"}}}} TO {endpoint}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public void Post_ComplexJson_ShouldHandleCorrectly()
        {
            // Arrange
            string json = "{\"user\":{\"name\":\"John\",\"age\":30},\"items\":[1,2,3]}";
            string endpoint = "https://httpbin.org/post";

            ProcessedPrompt prompt = new($"POST {json} TO {endpoint}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine!.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public void Post_MultipleVariables_ShouldResolveAll()
        {
            // Arrange
            engine!.RegisterVariable("data", "{\"status\":\"active\"}");
            engine.RegisterVariable("endpoint", "https://httpbin.org/post");

            ProcessedPrompt prompt = new("POST [data] TO [endpoint].");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
            });
        }
    }
}