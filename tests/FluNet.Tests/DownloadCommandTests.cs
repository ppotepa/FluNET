using FluNET.Prompt;
using FluNET.Context;
using FluNET.Syntax.Verbs;
using FluNET.Words;
using FluNET.Syntax.Validation;
using FluNET.Sentences;

namespace FluNET.Tests
{
    /// <summary>
    /// Comprehensive tests for DOWNLOAD command execution.
    /// Tests downloading files from HTTP/HTTPS URLs with various scenarios.
    /// </summary>
    [TestFixture]
    public class DownloadCommandTests
    {
        private FluNETContext _context = null!;
        private Engine engine = null!;
        private string testDirectory = null!;
        private string testServerUrl = null!;

        [SetUp]
        public void Setup()
        {
            _context = FluNETContext.Create();
            engine = _context.GetEngine();

            // Create test directory
            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_Download_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);

            // Start a simple HTTP server for testing
            StartTestHttpServer();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _context?.Dispose();

                // Cleanup test files
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

        private void StartTestHttpServer()
        {
            // The test web server (FluNET.TestWebServer) should be running on http://localhost:8765
            // You can start it manually with: cd src/FluNET.TestWebServer && dotnet run
            // Or the tests will verify the server is available
            testServerUrl = "http://localhost:8765/api/testfiles/";

            // Verify the test server is running
            try
            {
                using HttpClient client = new();
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = client.GetAsync(testServerUrl + "health").Result;
                if (!response.IsSuccessStatusCode)
                {
                    TestContext.WriteLine("Warning: Test web server is not responding. Please start FluNET.TestWebServer.");
                }
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Warning: Could not connect to test HTTP server: {ex.Message}");
                TestContext.WriteLine("Please ensure FluNET.TestWebServer is running on http://localhost:8765");
            }
        }

        #region DOWNLOAD Command Basic Tests

        [Test]
        public void Download_Synonyms_Property_ShouldReturnExpectedValues()
        {
            // Arrange
            DownloadFile downloadInstance = new(
                new FileInfo(Path.Combine(testDirectory, "test.txt")),
                new Uri(testServerUrl + "testfile.txt"));

            // Act
            string[] synonyms = downloadInstance.Synonyms;

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(synonyms, Is.Not.Null);
                Assert.That(synonyms, Does.Contain("PULL"));
                Assert.That(synonyms, Does.Contain("GRAB"));
                Assert.That(synonyms, Does.Contain("OBTAIN"));
            });
        }

        [Test]
        public void Download_WithDestination_ShouldDownloadToSpecifiedPath()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "downloaded.txt");
            ProcessedPrompt prompt = new($"DOWNLOAD [file] FROM {{{testServerUrl}testfile.txt}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<FileInfo>());

                FileInfo? downloadedFile = result as FileInfo;
                Assert.That(downloadedFile, Is.Not.Null);
                Assert.That(downloadedFile!.Exists, Is.True);
                Assert.That(File.ReadAllText(downloadedFile.FullName), Does.Contain("test file for download"));
            });
        }

        [Test]
        public void Download_WithoutDestination_ShouldExtractFilenameFromUrl()
        {
            // Arrange
            // Change to test directory so the file is downloaded there
            string originalDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(testDirectory);

            try
            {
                ProcessedPrompt prompt = new($"DOWNLOAD [file] FROM {{{testServerUrl}testfile.txt}} .");

                // Act
                (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                    Assert.That(result, Is.Not.Null);
                    Assert.That(result, Is.InstanceOf<FileInfo>());

                    FileInfo? downloadedFile = result as FileInfo;
                    Assert.That(downloadedFile, Is.Not.Null);
                    Assert.That(downloadedFile!.Name, Is.EqualTo("testfile.txt"));
                    Assert.That(downloadedFile.Exists, Is.True);
                });
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }

        [Test]
        public void Pull_Synonym_ShouldWorkLikeDownload()
        {
            // Arrange - Using PULL instead of DOWNLOAD
            string destinationPath = Path.Combine(testDirectory, "pulled.txt");
            ProcessedPrompt prompt = new($"PULL [file] FROM {{{testServerUrl}testfile.txt}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<FileInfo>());

                FileInfo? downloadedFile = result as FileInfo;
                Assert.That(downloadedFile!.Exists, Is.True);
            });
        }

        [Test]
        public void Grab_Synonym_ShouldWorkLikeDownload()
        {
            // Arrange - Using GRAB instead of DOWNLOAD
            string destinationPath = Path.Combine(testDirectory, "grabbed.txt");
            ProcessedPrompt prompt = new($"GRAB [file] FROM {{{testServerUrl}testfile.txt}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public void Obtain_Synonym_ShouldWorkLikeDownload()
        {
            // Arrange - Using OBTAIN instead of DOWNLOAD
            string destinationPath = Path.Combine(testDirectory, "obtained.txt");
            ProcessedPrompt prompt = new($"OBTAIN [file] FROM {{{testServerUrl}testfile.txt}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public void Download_WithVariable_ShouldResolveAndExecute()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "var_download.txt");
            engine.RegisterVariable("url", testServerUrl + "testfile.txt");
            engine.RegisterVariable("dest", destinationPath);
            ProcessedPrompt prompt = new($"DOWNLOAD [file] FROM [url] TO [dest] .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(result, Is.Not.Null);
                Assert.That(File.Exists(destinationPath), Is.True);
            });
        }

        #endregion DOWNLOAD Command Basic Tests

        #region Validation Tests

        [Test]
        public void Download_MissingFrom_ShouldFail()
        {
            // Arrange
            ProcessedPrompt prompt = new($"DOWNLOAD [file] TO {{test.txt}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.False);
        }

        [Test]
        public void Download_MissingWhat_ShouldFail()
        {
            // Arrange
            ProcessedPrompt prompt = new($"DOWNLOAD FROM {{{testServerUrl}testfile.txt}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("DOWNLOAD verb requires a subject"));
            });
        }

        [Test]
        [Ignore("Test behavior varies depending on network configuration and DNS resolution")]
        public void Download_InvalidUrl_ShouldFailDuringExecution()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "invalid.txt");
            ProcessedPrompt prompt = new($"DOWNLOAD [file] FROM {{http://invalid-url-that-does-not-exist.com/file.txt}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Either the validation fails OR the file doesn't exist
            // (HttpClient may throw different exceptions depending on network configuration)
            Assert.That(validation.IsValid == false || !File.Exists(destinationPath), Is.True,
                "Download from invalid URL should either fail validation or not create the file");
        }

        #endregion Validation Tests

        #region DownloadFile Specific Tests

        [Test]
        public void DownloadFile_Text_Property_ShouldReturnDOWNLOAD()
        {
            // Arrange
            DownloadFile downloadInstance = new(
                new FileInfo(Path.Combine(testDirectory, "test.txt")),
                new Uri(testServerUrl + "testfile.txt"));

            // Act
            string text = downloadInstance.Text;

            // Assert
            Assert.That(text, Is.EqualTo("DOWNLOAD"));
        }

        [Test]
        public void DownloadFile_Resolve_ValidUrl_ShouldReturnUri()
        {
            // Arrange
            DownloadFile downloadInstance = new(
                new FileInfo(Path.Combine(testDirectory, "test.txt")),
                new Uri(testServerUrl + "testfile.txt"));

            // Act
            Uri? resolved = downloadInstance.Resolve("https://example.com/file.txt");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.Not.Null);
                Assert.That(resolved, Is.InstanceOf<Uri>());
                Assert.That(resolved!.Scheme, Is.EqualTo(Uri.UriSchemeHttps));
            });
        }

        [Test]
        public void DownloadFile_Resolve_InvalidUrl_ShouldReturnNull()
        {
            // Arrange
            DownloadFile downloadInstance = new(
                new FileInfo(Path.Combine(testDirectory, "test.txt")),
                new Uri(testServerUrl + "testfile.txt"));

            // Act
            Uri? resolved = downloadInstance.Resolve("not-a-valid-url");

            // Assert
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void DownloadFile_Resolve_NonHttpScheme_ShouldReturnNull()
        {
            // Arrange
            DownloadFile downloadInstance = new(
                new FileInfo(Path.Combine(testDirectory, "test.txt")),
                new Uri(testServerUrl + "testfile.txt"));

            // Act
            Uri? resolved = downloadInstance.Resolve("ftp://example.com/file.txt");

            // Assert
            Assert.That(resolved, Is.Null); // Only HTTP/HTTPS are allowed
        }

        [Test]
        public void DownloadFile_ExtractFilename_WithValidPath_ShouldExtractName()
        {
            // Arrange
            Uri uri = new Uri("https://example.com/path/to/document.pdf");
            DownloadFile downloadInstance = new(
                new FileInfo(Path.Combine(testDirectory, "test.txt")),
                uri);

            // Act
            string filename = downloadInstance.GetType()
                .GetMethod("ExtractFilename", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(downloadInstance, new object[] { uri }) as string ?? "";

            // Assert
            Assert.That(Path.GetFileName(filename), Is.EqualTo("document.pdf"));
        }

        [Test]
        public void DownloadFile_Validate_LiteralWord_ShouldReturnTrue()
        {
            // Arrange
            DownloadFile downloadInstance = new(
                new FileInfo(Path.Combine(testDirectory, "test.txt")),
                new Uri(testServerUrl + "testfile.txt"));
            LiteralWord literalWord = new("https://example.com/file.txt");

            // Act
            bool isValid = downloadInstance.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void DownloadFile_Validate_VariableWord_ShouldReturnTrue()
        {
            // Arrange
            DownloadFile downloadInstance = new(
                new FileInfo(Path.Combine(testDirectory, "test.txt")),
                new Uri(testServerUrl + "testfile.txt"));
            VariableWord variableWord = new("[url]");

            // Act
            bool isValid = downloadInstance.Validate(variableWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        #endregion DownloadFile Specific Tests

        #region Edge Cases

        [Test]
        public void Download_CaseInsensitive_ShouldWork()
        {
            // Arrange - Testing lowercase synonym
            string destinationPath = Path.Combine(testDirectory, "lowercase.txt");
            ProcessedPrompt prompt = new($"download [file] FROM {{{testServerUrl}testfile.txt}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
        }

        [Test]
        public void Download_BinaryFile_ShouldDownloadCorrectly()
        {
            // Arrange
            string destinationPath = Path.Combine(testDirectory, "image.png");
            ProcessedPrompt prompt = new($"DOWNLOAD [file] FROM {{{testServerUrl}image.png}} TO {{{destinationPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, $"Validation failed: {validation.FailureReason}");
                Assert.That(File.Exists(destinationPath), Is.True);
                byte[] content = File.ReadAllBytes(destinationPath);
                Assert.That(content.Length, Is.GreaterThan(0));
            });
        }

        #endregion Edge Cases
    }
}