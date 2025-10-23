using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace FluNET.Tests
{
    /// <summary>
    /// Integration tests for DOWNLOAD command using the FluNET.TestWebServer.
    /// These tests verify realistic download scenarios with various file types.
    /// </summary>
    [TestFixture]
    public class DownloadIntegrationTests
    {
        private Engine engine = null!;
        private string testDirectory = null!;
        private ServiceProvider? serviceProvider;
        private IServiceScope? scope;
        private const string BaseUrl = "http://localhost:8765/api/testfiles/";

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
            services.AddScoped<IVariableResolver, VariableResolver>();
            services.AddScoped<SentenceExecutor>();

            serviceProvider = services.BuildServiceProvider();
            scope = serviceProvider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();

            // Create test directory
            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_Integration_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);

            // Verify test server is running
            VerifyTestServerIsRunning();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                scope?.Dispose();
                serviceProvider?.Dispose();

                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: TearDown cleanup failed: {ex.Message}");
            }
        }

        private void VerifyTestServerIsRunning()
        {
            try
            {
                using HttpClient client = new();
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = client.GetAsync(BaseUrl + "health").Result;
                if (!response.IsSuccessStatusCode)
                {
                    Assert.Ignore("Test web server is not running. Please start FluNET.TestWebServer.");
                }
            }
            catch
            {
                Assert.Ignore("Test web server is not running. Please start FluNET.TestWebServer on http://localhost:8765");
            }
        }

        #region JSON Download Tests

        [Test]
        public void Download_JsonFile_ShouldParseCorrectly()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "data.json");
            ProcessedPrompt prompt = new($"DOWNLOAD [data] FROM {{{BaseUrl}data.json}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(destinationPath), Is.True);

                // Verify JSON content
                string jsonContent = File.ReadAllText(destinationPath);
                JsonDocument doc = JsonDocument.Parse(jsonContent);
                Assert.That(doc.RootElement.GetProperty("Name").GetString(), Is.EqualTo("Test Data"));
                Assert.That(doc.RootElement.GetProperty("Version").GetString(), Is.EqualTo("1.0"));
                Assert.That(doc.RootElement.GetProperty("Items").GetArrayLength(), Is.EqualTo(3));
            });
        }

        [Test]
        public void Download_LargeJsonFile_ShouldHandleCorrectly()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "largefile.json");
            ProcessedPrompt prompt = new($"DOWNLOAD [largefile] FROM {{{BaseUrl}largefile.json}} TO {{{destinationPath}}} .");

            // Act
            var startTime = DateTime.UtcNow;
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(destinationPath), Is.True);

                FileInfo fileInfo = new(destinationPath);
                Assert.That(fileInfo.Length, Is.GreaterThan(100000), "Large file should be over 100KB");

                // Verify it contains expected number of items
                string jsonContent = File.ReadAllText(destinationPath);
                JsonDocument doc = JsonDocument.Parse(jsonContent);
                Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(1000));

                TestContext.WriteLine($"Downloaded {fileInfo.Length:N0} bytes in {duration.TotalMilliseconds:F0}ms");
            });
        }

        #endregion JSON Download Tests

        #region CSV and XML Download Tests

        [Test]
        public void Download_CsvFile_ShouldPreserveFormat()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "data.csv");
            ProcessedPrompt prompt = new($"PULL [csv] FROM {{{BaseUrl}data.csv}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(destinationPath), Is.True);

                string[] lines = File.ReadAllLines(destinationPath);
                Assert.That(lines.Length, Is.GreaterThanOrEqualTo(5), "Should have header + data rows");
                Assert.That(lines[0], Does.Contain("Id,Name,Email,Status"));
                Assert.That(lines[1], Does.Contain("John Doe"));
            });
        }

        [Test]
        public void Download_XmlFile_ShouldBeWellFormed()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "config.xml");
            ProcessedPrompt prompt = new($"GRAB [config] FROM {{{BaseUrl}config.xml}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(destinationPath), Is.True);

                string xmlContent = File.ReadAllText(destinationPath);
                Assert.That(xmlContent, Does.Contain("<?xml version"));
                Assert.That(xmlContent, Does.Contain("<configuration>"));
                Assert.That(xmlContent, Does.Contain("<settings>"));
            });
        }

        #endregion CSV and XML Download Tests

        #region Binary File Tests

        [Test]
        public void Download_PngImage_ShouldPreserveBinaryData()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "downloaded.png");
            ProcessedPrompt prompt = new($"DOWNLOAD [image] FROM {{{BaseUrl}image.png}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(destinationPath), Is.True);

                byte[] data = File.ReadAllBytes(destinationPath);
                // Verify PNG signature
                Assert.That(data[0], Is.EqualTo(0x89), "PNG signature byte 1");
                Assert.That(data[1], Is.EqualTo(0x50), "PNG signature byte 2 (P)");
                Assert.That(data[2], Is.EqualTo(0x4E), "PNG signature byte 3 (N)");
                Assert.That(data[3], Is.EqualTo(0x47), "PNG signature byte 4 (G)");
            });
        }

        #endregion Binary File Tests

        #region Filename Extraction Tests

        [Test]
        public void Download_NestedPath_ShouldExtractFilename()
        {
            // Arrange
            string originalDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(testDirectory);

            try
            {
                ProcessedPrompt prompt = new($"OBTAIN [file] FROM {{{BaseUrl}nested/path/file.txt}} .");

                // Act
                (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(validation.IsValid, Is.True);

                    string expectedPath = Path.Combine(testDirectory, "file.txt");
                    Assert.That(File.Exists(expectedPath), Is.True);

                    string content = File.ReadAllText(expectedPath);
                    Assert.That(content, Does.Contain("nested path"));
                });
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }

        [Test]
        public void Download_WithoutExtension_FromDocument_ShouldExtractCorrectly()
        {
            // Arrange
            string originalDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(testDirectory);

            try
            {
                ProcessedPrompt prompt = new($"DOWNLOAD [doc] FROM {{{BaseUrl}document.txt}} .");

                // Act
                (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(validation.IsValid, Is.True);

                    string expectedPath = Path.Combine(testDirectory, "document.txt");
                    Assert.That(File.Exists(expectedPath), Is.True);

                    string content = File.ReadAllText(expectedPath);
                    Assert.That(content, Does.Contain("Test document content"));
                });
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }

        #endregion Filename Extraction Tests

        #region Variable Resolution Tests

        [Test]
        public void Download_WithVariableUrl_ShouldResolveAndDownload()
        {
            // Arrange
            engine.RegisterVariable("apiUrl", BaseUrl + "data.json");
            string destinationPath = Path.Combine(testDirectory, "fromvar.json");
            ProcessedPrompt prompt = new($"DOWNLOAD [data] FROM [apiUrl] TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(destinationPath), Is.True);

                FileInfo fileInfo = new(destinationPath);
                Assert.That(fileInfo.Length, Is.GreaterThan(0));
            });
        }

        [Test]
        public void Download_WithVariableDestination_ShouldResolveCorrectly()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "output.txt");
            engine.RegisterVariable("outputPath", destinationPath);
            ProcessedPrompt prompt = new($"DOWNLOAD [file] FROM {{{BaseUrl}testfile.txt}} TO [outputPath] .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(destinationPath), Is.True);

                string content = File.ReadAllText(destinationPath);
                Assert.That(content, Does.Contain("test file for download"));
            });
        }

        #endregion Variable Resolution Tests

        #region Performance and Edge Cases

        [Test]
        public void Download_MultipleFilesSequentially_ShouldSucceed()
        {
            // Arrange & Act & Assert
            var files = new[]
            {
                ("testfile.txt", "test1.txt"),
                ("data.json", "test2.json"),
                ("data.csv", "test3.csv")
            };

            foreach (var (source, dest) in files)
            {
                string destinationPath = Path.Combine(testDirectory, dest);
                ProcessedPrompt prompt = new($"DOWNLOAD [file] FROM {{{BaseUrl}{source}}} TO {{{destinationPath}}} .");

                (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

                Assert.That(validation.IsValid, Is.True, $"Failed to download {source}");
                Assert.That(File.Exists(destinationPath), Is.True, $"{dest} was not created");
            }
        }

        [Test]
        [Ignore("Performance test - enable manually")]
        public void Download_SlowEndpoint_ShouldHandleGracefully()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "slow.txt");
            ProcessedPrompt prompt = new($"DOWNLOAD [file] FROM {{{BaseUrl}slow.txt}} TO {{{destinationPath}}} .");

            // Act
            var startTime = DateTime.UtcNow;
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(destinationPath), Is.True);
                Assert.That(duration.TotalSeconds, Is.GreaterThanOrEqualTo(2), "Should take at least 2 seconds");

                TestContext.WriteLine($"Slow download completed in {duration.TotalSeconds:F2} seconds");
            });
        }

        #endregion Performance and Edge Cases
    }
}