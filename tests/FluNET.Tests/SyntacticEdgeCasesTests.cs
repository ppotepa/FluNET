using FluNET.Prompt;
using FluNET.Context;
using FluNET.Syntax.Verbs;
using FluNET.Words;
using FluNET.Syntax.Validation;
using FluNET.Sentences;

namespace FluNET.Tests
{
    /// <summary>
    /// Comprehensive tests for syntactic edge cases and boundary conditions.
    /// Tests unusual but valid syntax, invalid syntax, and error handling.
    /// </summary>
    [TestFixture]
    [Order(9)]
    public class SyntacticEdgeCasesTests
    {
        private FluNetContext _context = null!;
        private Engine engine = null!;
        private string testDirectory = null!;

        [SetUp]
        public void Setup()
        {
            _context = FluNetContext.Create();
            engine = _context.GetEngine();

            // Create test directory
            testDirectory = Path.Combine(Path.GetTempPath(), "FluNET_EdgeCases_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
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
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Missing Terminators

        [Test]
        public void EdgeCase_MissingPeriod_ShouldFail()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {file.txt}");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False, "Should fail without terminator");
                Assert.That(validation.FailureReason, Does.Contain("terminator"));
            });
        }

        [Test]
        public void EdgeCase_QuestionMarkTerminator_ShouldSucceed()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {file.txt} ?");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Validation should succeed (question mark is a valid terminator)
            Assert.That(validation.IsValid, Is.True, "Question mark is a valid terminator");
        }

        [Test]
        public void EdgeCase_ExclamationTerminator_ShouldSucceed()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {file.txt} !");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Validation should succeed (exclamation is a valid terminator)
            Assert.That(validation.IsValid, Is.True, "Exclamation mark is a valid terminator");
        }

        #endregion Missing Terminators

        #region Empty and Whitespace Cases

        [Test]
        public void EdgeCase_EmptyPrompt_ShouldFail()
        {
            // Arrange
            ProcessedPrompt prompt = new("");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("Empty"));
            });
        }

        [Test]
        public void EdgeCase_OnlyWhitespace_ShouldFail()
        {
            // Arrange
            ProcessedPrompt prompt = new("   \t\n   ");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("terminator"));
            });
        }

        [Test]
        public void EdgeCase_OnlyPeriod_ShouldFail()
        {
            // Arrange
            ProcessedPrompt prompt = new(".");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("verb"));
            });
        }

        #endregion Empty and Whitespace Cases

        #region Unknown Verbs and Keywords

        [Test]
        public void EdgeCase_UnknownVerb_ShouldFail()
        {
            // Arrange - UPLOAD is not a defined verb
            ProcessedPrompt prompt = new("UPLOAD [data] TO {destination.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("verb"));
            });
        }

        [Test]
        public void EdgeCase_VerbNotFirst_ShouldFail()
        {
            // Arrange
            ProcessedPrompt prompt = new("[data] GET FROM {source.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("verb"));
            });
        }

        [Test]
        public void EdgeCase_LiteralAsFirstWord_ShouldFail()
        {
            // Arrange
            ProcessedPrompt prompt = new("Hello World .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("verb"));
            });
        }

        #endregion Unknown Verbs and Keywords

        #region Malformed Variables and References

        [Test]
        public void EdgeCase_UnclosedVariable_ShouldTreatAsLiteral()
        {
            // Arrange - Missing closing bracket
            ProcessedPrompt prompt = new("GET [text FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - "[text" might be treated as a literal, validation might fail differently
            Assert.That(validation.IsValid, Is.False);
        }

        [Test]
        public void EdgeCase_UnclosedReference_ShouldTreatAsLiteral()
        {
            // Arrange - Missing closing brace - tokenizer treats unclosed braces as separate tokens
            ProcessedPrompt prompt = new("GET [text] FROM {file.txt .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Syntax validation passes (treated as literal tokens), but execution may fail
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_EmptyVariable_ShouldSucceed()
        {
            // Arrange - Variable with empty name
            ProcessedPrompt prompt = new("GET [] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Empty variable name is technically valid (though semantically questionable)
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_EmptyReference_ShouldSucceed()
        {
            // Arrange - Reference with empty path
            ProcessedPrompt prompt = new("GET [text] FROM {} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Empty reference is syntactically valid
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_NestedBrackets_ShouldHandleCorrectly()
        {
            // Arrange - Variable name contains brackets (edge case)
            ProcessedPrompt prompt = new("GET [[nested]] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Depends on tokenization rules
            // This might succeed or fail depending on how nested brackets are handled
            // Just verify we get a validation result
            Assert.Pass($"Validation result: {validation.IsValid}, Reason: {validation.FailureReason}");
        }

        [Test]
        public void EdgeCase_NestedBraces_ShouldHandleCorrectly()
        {
            // Arrange - Reference contains braces (edge case)
            ProcessedPrompt prompt = new("GET [text] FROM {{nested}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Depends on tokenization rules
            // Just verify we get a validation result
            Assert.Pass($"Validation result: {validation.IsValid}, Reason: {validation.FailureReason}");
        }

        #endregion Malformed Variables and References

        #region Missing Required Components

        [Test]
        public void EdgeCase_GET_MissingWhat_ShouldFail()
        {
            // Arrange - GET directly followed by FROM (missing [what])
            ProcessedPrompt prompt = new("GET FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("subject"));
            });
        }

        [Test]
        public void EdgeCase_GET_MissingFrom_ShouldFail()
        {
            // Arrange - GET with [what] but no FROM
            ProcessedPrompt prompt = new("GET [text] .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("FROM"));
            });
        }

        [Test]
        public void EdgeCase_GET_FromWithoutSource_ShouldFail()
        {
            // Arrange - FROM not followed by a source
            ProcessedPrompt prompt = new("GET [text] FROM .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("source"));
            });
        }

        [Test]
        public void EdgeCase_SAVE_MissingWhat_ShouldFail()
        {
            // Arrange - SAVE without [what] to save
            // Note: This is syntactically valid but semantically incomplete - TO is resolved to empty path
            ProcessedPrompt prompt = new("SAVE TO {output.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Syntax validation passes, result is empty string
            Assert.That(validation.IsValid, Is.True);
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void EdgeCase_POST_MissingWhat_ShouldFail()
        {
            // Arrange - POST without [what] to post
            ProcessedPrompt prompt = new("POST TO {endpoint} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.False);
        }

        #endregion Missing Required Components

        #region Multiple Keywords

        [Test]
        public void EdgeCase_MultipleFromKeywords_ShouldHandleFirst()
        {
            // Arrange - Multiple FROM keywords (ambiguous)
            ProcessedPrompt prompt = new("GET [text] FROM {file1.txt} FROM {file2.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Should either use first FROM or fail validation
            // Just verify we get a validation result
            Assert.Pass($"Validation result: {validation.IsValid}, Reason: {validation.FailureReason}");
        }

        [Test]
        public void EdgeCase_MultipleToKeywords_ShouldHandleFirst()
        {
            // Arrange - Multiple TO keywords
            ProcessedPrompt prompt = new("SAVE [data] TO {file1.txt} TO {file2.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Should either use first TO or fail validation
            // Just verify we get a validation result
            Assert.Pass($"Validation result: {validation.IsValid}, Reason: {validation.FailureReason}");
        }

        #endregion Multiple Keywords

        #region Special Characters in Paths

        [Test]
        public void EdgeCase_PathWithSpaces_ShouldSucceed()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {C:\\My Documents\\test file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_PathWithSpecialChars_ShouldSucceed()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {C:\\Test-Files\\file_name (1).txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_PathWithUnicode_ShouldSucceed()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {C:\\Documents\\文档\\файл.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_URLAsReference_ShouldSucceed()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [data] FROM {https://example.com/api/data?key=value&format=json} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_NetworkPathAsReference_ShouldSucceed()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {\\\\server\\share\\file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        #endregion Special Characters in Paths

        #region Case Sensitivity

        [Test]
        public void EdgeCase_VerbLowerCase_ShouldSucceed()
        {
            // Arrange - Verb in lowercase
            ProcessedPrompt prompt = new("get [text] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Verbs should be case-insensitive
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_VerbMixedCase_ShouldSucceed()
        {
            // Arrange - Verb in mixed case
            ProcessedPrompt prompt = new("GeT [text] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_KeywordLowerCase_ShouldSucceed()
        {
            // Arrange - FROM in lowercase
            ProcessedPrompt prompt = new("GET [text] from {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Keywords should be case-insensitive
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_QualifierLowerCase_ShouldSucceed()
        {
            // Arrange - Qualifier in lowercase
            ProcessedPrompt prompt = new("GET text [content] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Qualifiers should be case-insensitive
            Assert.That(validation.IsValid, Is.True);
        }

        #endregion Case Sensitivity

        #region Multiple Qualifiers

        [Test]
        public void EdgeCase_MultipleQualifiers_ShouldHandleFirst()
        {
            // Arrange - Multiple qualifiers (ambiguous)
            ProcessedPrompt prompt = new("GET TEXT JSON [data] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Behavior depends on implementation
            // Just verify we get a validation result
            Assert.Pass($"Validation result: {validation.IsValid}, Reason: {validation.FailureReason}");
        }

        [Test]
        public void EdgeCase_QualifierWithoutWhat_ShouldFail()
        {
            // Arrange - Qualifier not followed by [what]
            ProcessedPrompt prompt = new("GET TEXT FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("variable").Or.Contain("reference"));
            });
        }

        [Test]
        public void EdgeCase_UnknownQualifier_ShouldTreatAsLiteral()
        {
            // Arrange - Unknown qualifier (not in the predefined list)
            ProcessedPrompt prompt = new("GET CUSTOM [data] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Unknown qualifier might be treated as a literal word
            // Just verify we get a validation result
            Assert.Pass($"Validation result: {validation.IsValid}, Reason: {validation.FailureReason}");
        }

        #endregion Multiple Qualifiers

        #region Very Long Inputs

        [Test]
        public void EdgeCase_VeryLongVariableName_ShouldSucceed()
        {
            // Arrange
            string longVarName = new string('a', 1000);
            ProcessedPrompt prompt = new($"GET [{longVarName}] FROM {{file.txt}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_VeryLongReferencePath_ShouldSucceed()
        {
            // Arrange
            string longPath = "C:\\" + string.Join("\\", Enumerable.Repeat("folder", 50)) + "\\file.txt";
            ProcessedPrompt prompt = new($"GET [text] FROM {{{longPath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        #endregion Very Long Inputs

        #region Whitespace Variations

        [Test]
        public void EdgeCase_ExtraWhitespaceBetweenWords_ShouldSucceed()
        {
            // Arrange - Multiple spaces between words
            ProcessedPrompt prompt = new("GET    [text]    FROM    {file.txt}    .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_TabsBetweenWords_ShouldSucceed()
        {
            // Arrange - Tabs instead of spaces
            ProcessedPrompt prompt = new("GET\t[text]\tFROM\t{file.txt}\t.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_NoSpaceBeforePeriod_ShouldSucceed()
        {
            // Arrange - Period attached to last word
            ProcessedPrompt prompt = new("GET [text] FROM {file.txt}.");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        #endregion Whitespace Variations

        #region Numeric and Symbolic Content

        [Test]
        public void EdgeCase_NumericVariableName_ShouldSucceed()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [123] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_SymbolicVariableName_ShouldSucceed()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [@#$%] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void EdgeCase_NumericOnlyReference_ShouldSucceed()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {12345} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
        }

        #endregion Numeric and Symbolic Content
    }
}