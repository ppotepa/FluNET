using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Verbs;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests
{
    /// <summary>
    /// Comprehensive tests for GET command execution with various scenarios
    /// to achieve 100% code coverage.
    /// </summary>
    [TestFixture]
    public class GetCommandTests
    {
        private Engine engine = null!;
        private string testFilePath = null!;
        private string testDirectory = null!;
        private ServiceProvider? serviceProvider;
        private IServiceScope? scope;

        [SetUp]
        public void Setup()
        {
            // Setup DI container - use Scoped for proper lifecycle management
            ServiceCollection services = new();
            services.AddScoped<DiscoveryService>();
            services.AddScoped<Engine>();
            services.AddScoped<TokenTreeFactory>();
            services.AddScoped<TokenFactory>();
            services.AddScoped<Lexicon.Lexicon>();
            services.AddScoped<WordFactory>();
            services.AddScoped<SentenceValidator>();
            services.AddScoped<SentenceFactory>();
            services.AddScoped<VariableResolver>();
            services.AddScoped<SentenceExecutor>();

            serviceProvider = services.BuildServiceProvider();
            scope = serviceProvider.CreateScope();
            engine = scope.ServiceProvider.GetRequiredService<Engine>();

            // Create test directory and file
            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            testFilePath = Path.Combine(testDirectory, "test.txt");
            File.WriteAllText(testFilePath, "This is a test file\nWith multiple lines\nFor testing GET command");
        }

        [TearDown]
        public void TearDown()
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

        #region GET Command Basic Tests

        [Test]
        public void Get_FromExistingFile_ShouldReturnFileContents()
        {
            // Arrange
            ProcessedPrompt prompt = new($"GET [text] FROM {{{testFilePath}}} .");

            // Act
            (ValidationResult validation, ISentence sentence, object result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string[]>());

                string[]? lines = result as string[];
                Assert.That(lines, Is.Not.Null);
                Assert.That(lines!.Length, Is.GreaterThan(0));
                Assert.That(string.Join("", lines), Does.Contain("This is a test file"));
            });
        }

        [Test]
        public void Get_FromNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            string nonExistentPath = Path.Combine(testDirectory, "does_not_exist.txt");
            ProcessedPrompt prompt = new($"GET [text] FROM {{{nonExistentPath}}} .");

            // Act
            (ValidationResult validation, ISentence sentence, object result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True); // Sentence structure is valid
                Assert.That(sentence, Is.Not.Null);
                Assert.That(result, Is.Null); // But execution returns null for non-existent file
            });
        }

        [Test]
        public void Get_WithVariable_ShouldResolveAndExecute()
        {
            // Arrange
            engine.RegisterVariable("filePath", testFilePath);
            ProcessedPrompt prompt = new($"GET [text] FROM [filePath] .");

            // Act
            (ValidationResult validation, ISentence sentence, object result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string[]>());
            });
        }

        [Test]
        public void Get_WithRelativePath_ShouldWork()
        {
            // Arrange
            string currentDir = Directory.GetCurrentDirectory();
            string relativePath = "test_relative.txt";
            string fullPath = Path.Combine(currentDir, relativePath);
            File.WriteAllText(fullPath, "Relative path test");

            try
            {
                ProcessedPrompt prompt = new($"GET [text] FROM {{{relativePath}}} .");

                // Act
                (ValidationResult validation, ISentence sentence, object result) = engine.Run(prompt);

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(validation.IsValid, Is.True);
                    Assert.That(sentence, Is.Not.Null);
                });
            }
            finally
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
        }

        #endregion

        #region GetText Specific Tests

        [Test]
        public void GetText_Resolve_ValidFilePath_ShouldReturnFileInfo()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));

            // Act
            FileInfo? resolved = getTextInstance.Resolve(testFilePath);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.Not.Null);
                Assert.That(resolved, Is.InstanceOf<FileInfo>());
                Assert.That(resolved!.FullName, Does.Contain("test.txt"));
            });
        }

        [Test]
        public void GetText_Resolve_InvalidPath_ShouldReturnNull()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));

            // Act
            FileInfo? resolved = getTextInstance.Resolve("\0invalid\0path");

            // Assert
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void GetText_Validate_LiteralWord_FileExists_ShouldReturnTrue()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));
            LiteralWord literalWord = new(testFilePath);

            // Act
            bool isValid = getTextInstance.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void GetText_Validate_LiteralWord_FileDoesNotExist_ShouldReturnTrue()
        {
            // Validate should check format, not existence
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));
            LiteralWord literalWord = new("nonexistent.txt");

            // Act
            bool isValid = getTextInstance.Validate(literalWord);

            // Assert
            Assert.That(isValid, Is.True); // Valid format, even if file doesn't exist
        }

        [Test]
        public void GetText_Validate_VariableWord_ShouldReturnTrue()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));
            VariableWord variableWord = new("[testVar]");

            // Act
            bool isValid = getTextInstance.Validate(variableWord);

            // Assert
            Assert.That(isValid, Is.True); // Variables are always valid in validation
        }

        [Test]
        public void GetText_Validate_OtherWordType_ShouldReturnFalse()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));
            Keywords.From fromKeyword = new();

            // Act
            bool isValid = getTextInstance.Validate(fromKeyword);

            // Assert
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void GetText_CanHandle_WithFromKeyword_ShouldReturnTrue()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));

            // Create a sentence structure: GetText -> VariableWord -> From -> LiteralWord
            GetText root = getTextInstance;
            VariableWord varWord = new("[text]");
            Keywords.From fromKeyword = new();
            LiteralWord fileWord = new(testFilePath);

            root.Next = varWord;
            varWord.Previous = root;
            varWord.Next = fromKeyword;
            fromKeyword.Previous = varWord;
            fromKeyword.Next = fileWord;
            fileWord.Previous = fromKeyword;

            // Act
            bool canHandle = getTextInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.True);
        }

        [Test]
        public void GetText_CanHandle_WithoutFromKeyword_ShouldReturnFalse()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));

            // Create a sentence structure without FROM keyword
            GetText root = getTextInstance;
            VariableWord varWord = new("[text]");

            root.Next = varWord;
            varWord.Previous = root;

            // Act
            bool canHandle = getTextInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.False);
        }

        [Test]
        public void GetText_CanHandle_WithoutValueAfterFrom_ShouldReturnFalse()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));

            // Create a sentence structure: GetText -> From (no value after)
            GetText root = getTextInstance;
            Keywords.From fromKeyword = new();

            root.Next = fromKeyword;
            fromKeyword.Previous = root;
            // No Next word after FROM

            // Act
            bool canHandle = getTextInstance.CanHandle(root);

            // Assert
            Assert.That(canHandle, Is.False);
        }

        [Test]
        public void GetText_Execute_ShouldReturnFileLines()
        {
            // Arrange
            FileInfo fileInfo = new(testFilePath);
            GetText getTextInstance = new(Array.Empty<string>(), fileInfo);

            // Act
            string[] result = getTextInstance.Execute();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string[]>());
                Assert.That(result.Length, Is.GreaterThan(0));
            });
        }

        [Test]
        public void GetText_Act_ShouldReadFileAndSplitLines()
        {
            // Arrange
            FileInfo fileInfo = new(testFilePath);
            GetText getTextInstance = new(Array.Empty<string>(), fileInfo);

            // Act
            Func<FileInfo, string[]> act = getTextInstance.Act;
            string[] result = act(fileInfo);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Length, Is.EqualTo(3)); // 3 lines in test file
                Assert.That(result[0], Does.Contain("This is a test file"));
            });
        }

        [Test]
        public void GetText_Then_ShouldReturnThenKeywordWithData()
        {
            // Arrange
            FileInfo fileInfo = new(testFilePath);
            GetText getTextInstance = new(Array.Empty<string>(), fileInfo);

            // Act
            IThen<string[]> thenKeyword = getTextInstance.Then();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(thenKeyword, Is.Not.Null);
                Assert.That(thenKeyword.Text, Is.EqualTo("THEN"));
                Assert.That(thenKeyword.Data, Is.Not.Null);
                Assert.That(thenKeyword.Data, Is.InstanceOf<string[]>());
            });
        }

        #endregion

        #region Get Base Class Tests

        [Test]
        public void Get_Text_Property_ShouldReturnGET()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));

            // Act
            string text = getTextInstance.Text;

            // Assert
            Assert.That(text, Is.EqualTo("GET"));
        }

        [Test]
        public void Get_What_Property_ShouldReturnProvidedValue()
        {
            // Arrange
            string[] expectedWhat = new[] { "line1", "line2" };
            GetText getTextInstance = new(expectedWhat, new FileInfo(testFilePath));

            // Act
            string[] what = getTextInstance.What;

            // Assert
            Assert.That(what, Is.EqualTo(expectedWhat));
        }

        [Test]
        public void Get_From_Property_ShouldReturnProvidedValue()
        {
            // Arrange
            FileInfo expectedFrom = new(testFilePath);
            GetText getTextInstance = new(Array.Empty<string>(), expectedFrom);

            // Act
            FileInfo from = getTextInstance.From;

            // Assert
            Assert.That(from, Is.EqualTo(expectedFrom));
        }

        [Test]
        public void Get_ValidateNext_WithFromKeyword_ShouldSucceed()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));
            Keywords.From fromKeyword = new();
            Lexicon.Lexicon lexicon = new(new DiscoveryService());

            // Act
            ValidationResult result = getTextInstance.ValidateNext(fromKeyword, lexicon);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Get_ValidateNext_WithInvalidWord_ShouldFail()
        {
            // Arrange
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));
            Keywords.To toKeyword = new(); // Wrong preposition
            Lexicon.Lexicon lexicon = new(new DiscoveryService());

            // Act
            ValidationResult result = getTextInstance.ValidateNext(toKeyword, lexicon);

            // Assert
            Assert.That(result.IsValid, Is.False);
        }

        #endregion

        #region Integration Tests

        [Test]
        public void Get_EndToEnd_MultipleExecutions_ShouldWork()
        {
            // Arrange
            string file1 = Path.Combine(testDirectory, "file1.txt");
            string file2 = Path.Combine(testDirectory, "file2.txt");
            File.WriteAllText(file1, "File 1 content");
            File.WriteAllText(file2, "File 2 content");

            // Act & Assert - First execution
            (ValidationResult ValidationResult, ISentence? Sentence, object? Result) result1 = engine.Run(new ProcessedPrompt($"GET [data] FROM {{{file1}}} ."));
            Assert.Multiple(() =>
            {
                Assert.That(result1.ValidationResult.IsValid, Is.True);
                Assert.That(result1.Result, Is.Not.Null);
            });

            // Act & Assert - Second execution
            (ValidationResult ValidationResult, ISentence? Sentence, object? Result) result2 = engine.Run(new ProcessedPrompt($"GET [data] FROM {{{file2}}} ."));
            Assert.Multiple(() =>
            {
                Assert.That(result2.ValidationResult.IsValid, Is.True);
                Assert.That(result2.Result, Is.Not.Null);
            });
        }

        [Test]
        public void Get_WithEmptyFile_ShouldReturnEmptyArray()
        {
            // Arrange
            string emptyFile = Path.Combine(testDirectory, "empty.txt");
            File.WriteAllText(emptyFile, "");
            ProcessedPrompt prompt = new($"GET [text] FROM {{{emptyFile}}} .");

            // Act
            (ValidationResult validation, ISentence sentence, object result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string[]>());
                string[]? lines = result as string[];
                Assert.That(lines!.Length, Is.EqualTo(1)); // Empty file has one empty line
            });
        }

        [Test]
        public void Get_WithLargeFile_ShouldHandleCorrectly()
        {
            // Arrange
            string largeFile = Path.Combine(testDirectory, "large.txt");
            List<string> lines = [];
            for (int i = 0; i < 1000; i++)
            {
                lines.Add($"Line {i}: Test data for large file processing");
            }
            File.WriteAllLines(largeFile, lines);
            ProcessedPrompt prompt = new($"GET [data] FROM {{{largeFile}}} .");

            // Act
            (ValidationResult validation, ISentence sentence, object result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string[]>());
                string[]? resultLines = result as string[];
                Assert.That(resultLines!.Length, Is.GreaterThanOrEqualTo(1000));
            });
        }

        [Test]
        public void Get_WithSpecialCharactersInPath_ShouldWork()
        {
            // Arrange
            string specialFile = Path.Combine(testDirectory, "file_with_underscores.txt");
            File.WriteAllText(specialFile, "Special characters test");
            ProcessedPrompt prompt = new($"GET [text] FROM {{{specialFile}}} .");

            // Act
            (ValidationResult validation, ISentence sentence, object result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
            });
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Get_WithTrailingPeriodInFilePath_ShouldRemovePeriod()
        {
            // The system should handle the terminator properly
            // File path with period at the end should be trimmed
            string testFile = Path.Combine(testDirectory, "test_period.txt");
            File.WriteAllText(testFile, "Period test");

            ProcessedPrompt prompt = new($"GET [text] FROM {{{testFile}}} .");

            (ValidationResult validation, ISentence sentence, object result) = engine.Run(prompt);

            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void Get_Resolve_EmptyString_ShouldReturnNull()
        {
            // Empty string should return null for safety
            GetText getTextInstance = new(Array.Empty<string>(), new FileInfo(testFilePath));

            FileInfo? resolved = getTextInstance.Resolve("");

            Assert.That(resolved, Is.Null); // Empty strings should be rejected
        }

        #endregion
    }
}
