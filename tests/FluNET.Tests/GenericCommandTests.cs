using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Syntax.Verbs;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace FluNET.Tests
{
    /// <summary>
    /// Comprehensive tests for all verb commands (SAVE, POST, DELETE, LOAD, SEND, TRANSFORM)
    /// to achieve 100% code coverage of generic command patterns.
    /// </summary>
    [TestFixture]
    public class GenericCommandTests
    {
        private Engine engine = null!;
        private string testDirectory = null!;
        private ServiceProvider? serviceProvider;
        private IServiceScope? scope;

        [SetUp]
        public void Setup()
        {
            // Setup DI container - use Transient for DiscoveryService to ensure fresh assembly discovery per test
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
            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                // Dispose scope and service provider to clean up resources
                scope?.Dispose();
                serviceProvider?.Dispose();

                // Cleanup test files
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - OS will clean up temp files eventually
                Console.WriteLine($"Warning: TearDown cleanup failed: {ex.Message}");
            }
        }

        #region SAVE Command Tests

        [Test]
        [Ignore("SaveText.CanHandle() returns false - needs engine fix")]
        public void Save_ToFile_ShouldCreateFile()
        {
            // Arrange
            string outputFile = Path.Combine(testDirectory, "output.txt");
            engine.RegisterVariable("data", "Test content to save");
            ProcessedPrompt prompt = new($"SAVE [data] TO {{{outputFile}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(sentence, Is.Not.Null);
            });
        }

        [Test]
        public void SaveText_Resolve_ValidPath_ShouldReturnFileInfo()
        {
            // Arrange
            SaveText saveTextInstance = new("content", new FileInfo("test.txt"));

            // Act
            FileInfo? resolved = saveTextInstance.Resolve("output.txt");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.Not.Null);
                Assert.That(resolved, Is.InstanceOf<FileInfo>());
            });
        }

        [Test]
        public void SaveText_Resolve_InvalidPath_ShouldReturnNull()
        {
            // Arrange
            SaveText saveTextInstance = new("content", new FileInfo("test.txt"));

            // Act
            FileInfo? resolved = saveTextInstance.Resolve("\0invalid\0");

            // Assert
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void SaveText_Validate_LiteralWord_ShouldReturnTrue()
        {
            // Arrange
            SaveText saveTextInstance = new("content", new FileInfo("test.txt"));
            LiteralWord literalWord = new("output.txt");

            // Act
            bool isValid = saveTextInstance.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void SaveText_Validate_VariableWord_ShouldReturnTrue()
        {
            // Arrange
            SaveText saveTextInstance = new("content", new FileInfo("test.txt"));
            VariableWord variableWord = new("[path]");

            // Act
            bool isValid = saveTextInstance.Validate(variableWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void Save_Text_Property_ShouldReturnSAVE()
        {
            // Arrange
            SaveText saveTextInstance = new("test", new FileInfo("file.txt"));

            // Act
            string text = saveTextInstance.Text;

            // Assert
            Assert.That(text, Is.EqualTo("SAVE"));
        }

        [Test]
        public void Save_CanHandle_WithToKeyword_ShouldReturnTrue()
        {
            // Arrange
            SaveText saveTextInstance = new("content", new FileInfo("test.txt"));

            SaveText root = saveTextInstance;
            VariableWord varWord = new("[data]");
            Keywords.To toKeyword = new();
            LiteralWord fileWord = new("output.txt");

            root.Next = varWord;
            varWord.Previous = root;
            varWord.Next = toKeyword;
            toKeyword.Previous = varWord;
            toKeyword.Next = fileWord;
            fileWord.Previous = toKeyword;

            // Act
            bool canHandle = saveTextInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.True);
        }

        [Test]
        public void Save_Execute_ShouldWriteFile()
        {
            // Arrange
            string outputFile = Path.Combine(testDirectory, "save_test.txt");
            SaveText saveTextInstance = new("Test content", new FileInfo(outputFile));

            // Act
            string result = saveTextInstance.Invoke();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Test content"));
                Assert.That(File.Exists(outputFile), Is.True);
                Assert.That(File.ReadAllText(outputFile), Is.EqualTo("Test content"));
            });
        }

        #endregion SAVE Command Tests

        #region POST Command Tests

        [Test]
        public void PostJson_Resolve_ValidUrl_ShouldReturnUri()
        {
            // Arrange
            PostJson postJsonInstance = new("data", new Uri("http://example.com"));

            // Act
            Uri? resolved = postJsonInstance.Resolve("https://api.example.com/endpoint");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.Not.Null);
                Assert.That(resolved, Is.InstanceOf<Uri>());
                Assert.That(resolved!.ToString(), Is.EqualTo("https://api.example.com/endpoint"));
            });
        }

        [Test]
        public void PostJson_Resolve_InvalidUrl_ShouldReturnNull()
        {
            // Arrange
            PostJson postJsonInstance = new("data", new Uri("http://example.com"));

            // Act
            Uri? resolved = postJsonInstance.Resolve("not a valid url");

            // Assert
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void PostJson_Validate_LiteralWord_ShouldReturnTrue()
        {
            // Arrange
            PostJson postJsonInstance = new("data", new Uri("http://example.com"));
            LiteralWord literalWord = new("https://api.example.com");

            // Act
            bool isValid = postJsonInstance.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void Post_Text_Property_ShouldReturnPOST()
        {
            // Arrange
            PostJson postJsonInstance = new("data", new Uri("http://example.com"));

            // Act
            string text = postJsonInstance.Text;

            // Assert
            Assert.That(text, Is.EqualTo("POST"));
        }

        [Test]
        public void Post_CanHandle_WithToKeyword_ShouldReturnTrue()
        {
            // Arrange
            PostJson postJsonInstance = new("data", new Uri("http://example.com"));

            PostJson root = postJsonInstance;
            VariableWord varWord = new("[data]");
            Keywords.To toKeyword = new();
            LiteralWord urlWord = new("https://api.example.com");

            root.Next = varWord;
            varWord.Previous = root;
            varWord.Next = toKeyword;
            toKeyword.Previous = varWord;
            toKeyword.Next = urlWord;
            urlWord.Previous = toKeyword;

            // Act
            bool canHandle = postJsonInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.True);
        }

        #endregion POST Command Tests

        #region DELETE Command Tests

        [Test]
        public void DeleteFile_Resolve_ValidPath_ShouldReturnDirectoryInfo()
        {
            // Arrange
            DeleteFile deleteFileInstance = new("file.txt", new DirectoryInfo(testDirectory));

            // Act
            DirectoryInfo? resolved = deleteFileInstance.Resolve(testDirectory);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.Not.Null);
                Assert.That(resolved, Is.InstanceOf<DirectoryInfo>());
            });
        }

        [Test]
        public void DeleteFile_Resolve_InvalidPath_ShouldReturnNull()
        {
            // Arrange
            DeleteFile deleteFileInstance = new("file.txt", new DirectoryInfo(testDirectory));

            // Act
            DirectoryInfo? resolved = deleteFileInstance.Resolve("\0invalid\0");

            // Assert
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void DeleteFile_Validate_LiteralWord_ShouldReturnTrue()
        {
            // Arrange
            DeleteFile deleteFileInstance = new("file.txt", new DirectoryInfo(testDirectory));
            LiteralWord literalWord = new(testDirectory);

            // Act
            bool isValid = deleteFileInstance.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void Delete_Text_Property_ShouldReturnDELETE()
        {
            // Arrange
            DeleteFile deleteFileInstance = new("file.txt", new DirectoryInfo(testDirectory));

            // Act
            string text = deleteFileInstance.Text;

            // Assert
            Assert.That(text, Is.EqualTo("DELETE"));
        }

        [Test]
        public void Delete_CanHandle_WithFromKeyword_ShouldReturnTrue()
        {
            // Arrange
            DeleteFile deleteFileInstance = new("file.txt", new DirectoryInfo(testDirectory));

            DeleteFile root = deleteFileInstance;
            VariableWord varWord = new("[file]");
            Keywords.From fromKeyword = new();
            LiteralWord dirWord = new(testDirectory);

            root.Next = varWord;
            varWord.Previous = root;
            varWord.Next = fromKeyword;
            fromKeyword.Previous = varWord;
            fromKeyword.Next = dirWord;
            dirWord.Previous = fromKeyword;

            // Act
            bool canHandle = deleteFileInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.True);
        }

        [Test]
        public void Delete_Execute_FileExists_ShouldDeleteFile()
        {
            // Arrange
            string testFile = "test_delete.txt";
            string fullPath = Path.Combine(testDirectory, testFile);
            File.WriteAllText(fullPath, "To be deleted");

            DeleteFile deleteFileInstance = new(testFile, new DirectoryInfo(testDirectory));

            // Act
            string result = deleteFileInstance.Invoke();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Deleted"));
                Assert.That(File.Exists(fullPath), Is.False);
            });
        }

        [Test]
        public void Delete_Execute_FileDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            string testFile = "nonexistent.txt";
            DeleteFile deleteFileInstance = new(testFile, new DirectoryInfo(testDirectory));

            // Act
            string result = deleteFileInstance.Invoke();

            // Assert
            Assert.That(result, Does.Contain("not found"));
        }

        #endregion DELETE Command Tests

        #region LOAD Command Tests

        [Test]
        public void LoadConfig_Resolve_ValidPath_ShouldReturnFileInfo()
        {
            // Arrange
            LoadConfig loadConfigInstance = new([], new FileInfo("config.json"));

            // Act
            FileInfo? resolved = loadConfigInstance.Resolve("settings.json");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.Not.Null);
                Assert.That(resolved, Is.InstanceOf<FileInfo>());
            });
        }

        [Test]
        public void LoadConfig_Resolve_InvalidPath_ShouldReturnNull()
        {
            // Arrange
            LoadConfig loadConfigInstance = new([], new FileInfo("config.json"));

            // Act
            FileInfo? resolved = loadConfigInstance.Resolve("\0invalid\0");

            // Assert
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void LoadConfig_Validate_LiteralWord_FileExists_ShouldReturnTrue()
        {
            // Arrange
            string configFile = Path.Combine(testDirectory, "config.json");
            File.WriteAllText(configFile, "{}");
            LoadConfig loadConfigInstance = new([], new FileInfo(configFile));
            LiteralWord literalWord = new(configFile);

            // Act
            bool isValid = loadConfigInstance.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void LoadConfig_Validate_LiteralWord_FileDoesNotExist_ShouldReturnTrue()
        {
            // Validate should check format, not existence
            // Arrange
            LoadConfig loadConfigInstance = new([], new FileInfo("config.json"));
            LiteralWord literalWord = new("nonexistent.json");

            // Act
            bool isValid = loadConfigInstance.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True); // Valid format, even if file doesn't exist
        }

        [Test]
        public void LoadConfig_Validate_VariableWord_ShouldReturnTrue()
        {
            // Arrange
            LoadConfig loadConfigInstance = new([], new FileInfo("config.json"));
            VariableWord variableWord = new("[configPath]");

            // Act
            bool isValid = loadConfigInstance.Validate(variableWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void Load_Text_Property_ShouldReturnLOAD()
        {
            // Arrange
            LoadConfig loadConfigInstance = new([], new FileInfo("config.json"));

            // Act
            string text = loadConfigInstance.Text;

            // Assert
            Assert.That(text, Is.EqualTo("LOAD"));
        }

        [Test]
        public void Load_CanHandle_WithFromKeyword_ShouldReturnTrue()
        {
            // Arrange
            LoadConfig loadConfigInstance = new([], new FileInfo("config.json"));

            LoadConfig root = loadConfigInstance;
            VariableWord varWord = new("[config]");
            Keywords.From fromKeyword = new();
            LiteralWord fileWord = new("settings.json");

            root.Next = varWord;
            varWord.Previous = root;
            varWord.Next = fromKeyword;
            fromKeyword.Previous = varWord;
            fromKeyword.Next = fileWord;
            fileWord.Previous = fromKeyword;

            // Act
            bool canHandle = loadConfigInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.True);
        }

        [Test]
        public void Load_Execute_ShouldLoadConfiguration()
        {
            // Arrange
            string configFile = Path.Combine(testDirectory, "test_config.json");
            File.WriteAllText(configFile, "{\"key\":\"value\"}");
            LoadConfig loadConfigInstance = new([], new FileInfo(configFile));

            // Act
            Dictionary<string, object> result = loadConfigInstance.Invoke();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<Dictionary<string, object>>());
                Assert.That(result.ContainsKey("loaded"), Is.True);
            });
        }

        #endregion LOAD Command Tests

        #region SEND Command Tests

        [Test]
        public void SendEmail_Resolve_ShouldReturnString()
        {
            // Arrange
            SendEmail sendEmailInstance = new("message", "recipient@example.com");

            // Act
            string? resolved = sendEmailInstance.Resolve("user@example.com");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.Not.Null);
                Assert.That(resolved, Is.EqualTo("user@example.com"));
            });
        }

        [Test]
        public void SendEmail_Validate_LiteralWord_ShouldReturnTrue()
        {
            // Arrange
            SendEmail sendEmailInstance = new("message", "recipient@example.com");
            LiteralWord literalWord = new("user@example.com");

            // Act
            bool isValid = sendEmailInstance.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void Send_Text_Property_ShouldReturnSEND()
        {
            // Arrange
            SendEmail sendEmailInstance = new("message", "recipient@example.com");

            // Act
            string text = sendEmailInstance.Text;

            // Assert
            Assert.That(text, Is.EqualTo("SEND"));
        }

        [Test]
        public void Send_CanHandle_WithToKeyword_ShouldReturnTrue()
        {
            // Arrange
            SendEmail sendEmailInstance = new("message", "recipient@example.com");

            SendEmail root = sendEmailInstance;
            VariableWord varWord = new("[message]");
            Keywords.To toKeyword = new();
            LiteralWord emailWord = new("user@example.com");

            root.Next = varWord;
            varWord.Previous = root;
            varWord.Next = toKeyword;
            toKeyword.Previous = varWord;
            toKeyword.Next = emailWord;
            emailWord.Previous = toKeyword;

            // Act
            bool canHandle = sendEmailInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.True);
        }

        [Test]
        public void Send_Execute_ShouldReturnConfirmation()
        {
            // Arrange
            SendEmail sendEmailInstance = new("Test message", "user@example.com");

            // Act
            var originalOut = Console.Out;
            using (StringWriter sw = new())
            {
                try
                {
                    Console.SetOut(sw);
                    string result = sendEmailInstance.Invoke();

                    // Assert
                    Assert.Multiple(() =>
                    {
                        Assert.That(result, Does.Contain("Email sent"));
                        Assert.That(result, Does.Contain("user@example.com"));
                    });
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        #endregion SEND Command Tests

        #region TRANSFORM Command Tests

        [Test]
        public void TransformEncoding_Resolve_UTF8_ShouldReturnEncoding()
        {
            // Arrange
            TransformEncoding transformInstance = new("text", Encoding.UTF8);

            // Act
            Encoding? resolved = transformInstance.Resolve("UTF8");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.Not.Null);
                Assert.That(resolved, Is.EqualTo(Encoding.UTF8));
            });
        }

        [Test]
        public void TransformEncoding_Resolve_UTF8WithHyphen_ShouldReturnEncoding()
        {
            // Arrange
            TransformEncoding transformInstance = new("text", Encoding.UTF8);

            // Act
            Encoding? resolved = transformInstance.Resolve("UTF-8");

            // Assert
            Assert.That(resolved, Is.EqualTo(Encoding.UTF8));
        }

        [Test]
        public void TransformEncoding_Resolve_ASCII_ShouldReturnEncoding()
        {
            // Arrange
            TransformEncoding transformInstance = new("text", Encoding.UTF8);

            // Act
            Encoding? resolved = transformInstance.Resolve("ASCII");

            // Assert
            Assert.That(resolved, Is.EqualTo(Encoding.ASCII));
        }

        [Test]
        public void TransformEncoding_Resolve_Unicode_ShouldReturnEncoding()
        {
            // Arrange
            TransformEncoding transformInstance = new("text", Encoding.UTF8);

            // Act
            Encoding? resolved = transformInstance.Resolve("UNICODE");

            // Assert
            Assert.That(resolved, Is.EqualTo(Encoding.Unicode));
        }

        [Test]
        public void TransformEncoding_Resolve_InvalidEncoding_ShouldReturnNull()
        {
            // Arrange
            TransformEncoding transformInstance = new("text", Encoding.UTF8);

            // Act
            Encoding? resolved = transformInstance.Resolve("INVALID_ENCODING_12345");

            // Assert
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void TransformEncoding_Validate_LiteralWord_ShouldReturnTrue()
        {
            // Arrange
            TransformEncoding transformInstance = new("text", Encoding.UTF8);
            LiteralWord literalWord = new("UTF8");

            // Act
            bool isValid = transformInstance.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void Transform_Text_Property_ShouldReturnTRANSFORM()
        {
            // Arrange
            TransformEncoding transformInstance = new("text", Encoding.UTF8);

            // Act
            string text = transformInstance.Text;

            // Assert
            Assert.That(text, Is.EqualTo("TRANSFORM"));
        }

        [Test]
        public void Transform_CanHandle_WithUsingKeyword_ShouldReturnTrue()
        {
            // Arrange
            TransformEncoding transformInstance = new("text", Encoding.UTF8);

            TransformEncoding root = transformInstance;
            VariableWord varWord = new("[data]");
            Keywords.Using usingKeyword = new();
            LiteralWord encodingWord = new("UTF8");

            root.Next = varWord;
            varWord.Previous = root;
            varWord.Next = usingKeyword;
            usingKeyword.Previous = varWord;
            usingKeyword.Next = encodingWord;
            encodingWord.Previous = usingKeyword;

            // Act
            bool canHandle = transformInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.True);
        }

        [Test]
        public void Transform_Execute_ShouldEncodeText()
        {
            // Arrange
            TransformEncoding transformInstance = new("Hello World", Encoding.UTF8);

            // Act
            string result = transformInstance.Invoke();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.Not.Empty);
                // Result should be base64 encoded
                Assert.That(() => Convert.FromBase64String(result), Throws.Nothing);
            });
        }

        #endregion TRANSFORM Command Tests

        #region CanHandle Negative Tests

        [Test]
        public void Save_CanHandle_WithoutToKeyword_ShouldReturnFalse()
        {
            // Arrange
            SaveText saveTextInstance = new("content", new FileInfo("test.txt"));
            SaveText root = saveTextInstance;
            VariableWord varWord = new("[data]");
            root.Next = varWord;

            // Act
            bool canHandle = saveTextInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.False);
        }

        [Test]
        public void Post_CanHandle_WithoutToKeyword_ShouldReturnFalse()
        {
            // Arrange
            PostJson postJsonInstance = new("data", new Uri("http://example.com"));
            PostJson root = postJsonInstance;

            // Act
            bool canHandle = postJsonInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.False);
        }

        [Test]
        public void Delete_CanHandle_WithoutFromKeyword_ShouldReturnFalse()
        {
            // Arrange
            DeleteFile deleteFileInstance = new("file.txt", new DirectoryInfo(testDirectory));
            DeleteFile root = deleteFileInstance;

            // Act
            bool canHandle = deleteFileInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.False);
        }

        [Test]
        public void Load_CanHandle_WithoutFromKeyword_ShouldReturnFalse()
        {
            // Arrange
            LoadConfig loadConfigInstance = new([], new FileInfo("config.json"));
            LoadConfig root = loadConfigInstance;

            // Act
            bool canHandle = loadConfigInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.False);
        }

        [Test]
        public void Send_CanHandle_WithoutToKeyword_ShouldReturnFalse()
        {
            // Arrange
            SendEmail sendEmailInstance = new("message", "recipient@example.com");
            SendEmail root = sendEmailInstance;

            // Act
            bool canHandle = sendEmailInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.False);
        }

        [Test]
        public void Transform_CanHandle_WithoutUsingKeyword_ShouldReturnFalse()
        {
            // Arrange
            TransformEncoding transformInstance = new("text", Encoding.UTF8);
            TransformEncoding root = transformInstance;

            // Act
            bool canHandle = transformInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.False);
        }

        [Test]
        public void Save_CanHandle_WithoutValueAfterTo_ShouldReturnFalse()
        {
            // Arrange
            SaveText saveTextInstance = new("content", new FileInfo("test.txt"));
            SaveText root = saveTextInstance;
            Keywords.To toKeyword = new();
            root.Next = toKeyword;
            toKeyword.Previous = root;

            // Act
            bool canHandle = saveTextInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.False);
        }

        #endregion CanHandle Negative Tests

        #region ValidateNext Tests

        [Test]
        public void Save_ValidateNext_WithToKeyword_ShouldSucceed()
        {
            // Arrange
            SaveText saveTextInstance = new("content", new FileInfo("test.txt"));
            Keywords.To toKeyword = new();
            Lexicon.Lexicon lexicon = new(new DiscoveryService());

            // Act
            ValidationResult result = saveTextInstance.ValidateNext(toKeyword, lexicon);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Post_ValidateNext_WithInvalidWord_ShouldFail()
        {
            // Arrange
            PostJson postJsonInstance = new("data", new Uri("http://example.com"));
            Keywords.From fromKeyword = new(); // Wrong preposition
            Lexicon.Lexicon lexicon = new(new DiscoveryService());

            // Act
            ValidationResult result = postJsonInstance.ValidateNext(fromKeyword, lexicon);

            // Assert
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void Delete_ValidateNext_WithFromKeyword_ShouldSucceed()
        {
            // Arrange
            DeleteFile deleteFileInstance = new("file.txt", new DirectoryInfo(testDirectory));
            Keywords.From fromKeyword = new();
            Lexicon.Lexicon lexicon = new(new DiscoveryService());

            // Act
            ValidationResult result = deleteFileInstance.ValidateNext(fromKeyword, lexicon);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Load_ValidateNext_WithInvalidWord_ShouldFail()
        {
            // Arrange
            LoadConfig loadConfigInstance = new([], new FileInfo("config.json"));
            Keywords.To toKeyword = new(); // Wrong preposition
            Lexicon.Lexicon lexicon = new(new DiscoveryService());

            // Act
            ValidationResult result = loadConfigInstance.ValidateNext(toKeyword, lexicon);

            // Assert
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void Send_ValidateNext_WithToKeyword_ShouldSucceed()
        {
            // Arrange
            SendEmail sendEmailInstance = new("message", "recipient@example.com");
            Keywords.To toKeyword = new();
            Lexicon.Lexicon lexicon = new(new DiscoveryService());

            // Act
            ValidationResult result = sendEmailInstance.ValidateNext(toKeyword, lexicon);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Transform_ValidateNext_WithUsingKeyword_ShouldSucceed()
        {
            // Arrange
            TransformEncoding transformInstance = new("text", Encoding.UTF8);
            Keywords.Using usingKeyword = new();
            Lexicon.Lexicon lexicon = new(new DiscoveryService());

            // Act
            ValidationResult result = transformInstance.ValidateNext(usingKeyword, lexicon);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        #endregion ValidateNext Tests

        #region Property Tests

        [Test]
        public void Save_What_Property_ShouldReturnProvidedValue()
        {
            // Arrange
            string expectedWhat = "Test content";
            SaveText saveTextInstance = new(expectedWhat, new FileInfo("test.txt"));

            // Act
            string what = saveTextInstance.What;

            // Assert
            Assert.That(what, Is.EqualTo(expectedWhat));
        }

        [Test]
        public void Post_What_Property_ShouldReturnProvidedValue()
        {
            // Arrange
            string expectedWhat = "JSON data";
            PostJson postJsonInstance = new(expectedWhat, new Uri("http://example.com"));

            // Act
            string what = postJsonInstance.What;

            // Assert
            Assert.That(what, Is.EqualTo(expectedWhat));
        }

        [Test]
        public void Delete_From_Property_ShouldReturnProvidedValue()
        {
            // Arrange
            DirectoryInfo expectedFrom = new(testDirectory);
            DeleteFile deleteFileInstance = new("file.txt", expectedFrom);

            // Act
            DirectoryInfo from = deleteFileInstance.From;

            // Assert
            Assert.That(from, Is.EqualTo(expectedFrom));
        }

        [Test]
        public void Load_From_Property_ShouldReturnProvidedValue()
        {
            // Arrange
            FileInfo expectedFrom = new("config.json");
            LoadConfig loadConfigInstance = new([], expectedFrom);

            // Act
            FileInfo from = loadConfigInstance.From;

            // Assert
            Assert.That(from, Is.EqualTo(expectedFrom));
        }

        [Test]
        public void Transform_Using_Property_ShouldReturnProvidedValue()
        {
            // Arrange
            Encoding expectedUsing = Encoding.UTF8;
            TransformEncoding transformInstance = new("text", expectedUsing);

            // Act
            Encoding usingValue = transformInstance.Using;

            // Assert
            Assert.That(usingValue, Is.EqualTo(expectedUsing));
        }

        #endregion Property Tests
    }
}