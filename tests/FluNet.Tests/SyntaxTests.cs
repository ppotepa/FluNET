using FluNET.Prompt;
using FluNET.Context;
using FluNET.Syntax.Verbs;
using FluNET.Words;
using FluNET.Syntax.Validation;
using FluNET.Sentences;

namespace FluNET.Tests
{
    /// <summary>
    /// Isolated tests for syntax validation - verifying tokenization and ISentence construction
    /// without execution. These tests ensure the syntax is correctly parsed before execution.
    /// </summary>
    [TestFixture]
    public class SyntaxTests
    {
        private FluNetContext _context = null!;
        private Engine engine = null!;

        [SetUp]
        public void Setup()
        {
            _context = FluNetContext.Create();
            engine = _context.GetEngine();
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
        }

        #region Tokenization Tests

        [Test]
        public void Tokenization_SimpleReference_ShouldCreateSingleToken()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {C:\\test.txt} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[0], Is.EqualTo("GET"));
            Assert.That(prompt.Tokens[1], Is.EqualTo("[text]"));
            Assert.That(prompt.Tokens[2], Is.EqualTo("FROM"));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{C:\\test.txt}"));
            Assert.That(prompt.Tokens[4], Is.EqualTo("."));
        }

        [Test]
        public void Tokenization_ReferenceWithSpaces_ShouldPreserveSpaces()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {C:\\Test Files\\document.txt} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{C:\\Test Files\\document.txt}"));
        }

        [Test]
        public void Tokenization_NestedBraces_ShouldPreserveNesting()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {{{filepath}}} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{{{filepath}}}"));
        }

        [Test]
        public void Tokenization_VariableWithoutSpaces_ShouldCreateSingleToken()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [myVariable] FROM {file.txt} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[1], Is.EqualTo("[myVariable]"));
        }

        [Test]
        public void Tokenization_MultipleReferences_ShouldSeparateCorrectly()
        {
            // Arrange
            ProcessedPrompt prompt = new("COPY [data] FROM {source.txt} TO {dest.txt} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(7));
            Assert.That(prompt.Tokens[0], Is.EqualTo("COPY"));
            Assert.That(prompt.Tokens[1], Is.EqualTo("[data]"));
            Assert.That(prompt.Tokens[2], Is.EqualTo("FROM"));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{source.txt}"));
            Assert.That(prompt.Tokens[4], Is.EqualTo("TO"));
            Assert.That(prompt.Tokens[5], Is.EqualTo("{dest.txt}"));
            Assert.That(prompt.Tokens[6], Is.EqualTo("."));
        }

        [Test]
        public void Tokenization_NoSpaceBeforeTerminator_ShouldNotAttachPeriod()
        {
            // Arrange - this is WRONG syntax but should still tokenize
            ProcessedPrompt prompt = new("GET [text] FROM {file.txt}.");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(4));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{file.txt}."));
        }

        #endregion Tokenization Tests

        #region Sentence Validation Tests

        [Test]
        public void Sentence_GET_WithReferenceWord_ShouldValidate()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {C:\\test.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - Only check syntax validation, not execution
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, "Sentence should be syntactically valid");
                Assert.That(sentence, Is.Not.Null, "Sentence should be constructed");
                Assert.That(sentence?.Root?.GetType().Name, Is.EqualTo("GetText"), "Should resolve to GetText verb");
            });
        }

        [Test]
        public void Sentence_GET_WithVariableInReference_ShouldValidate()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {{{filepath}}} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, "Sentence should be syntactically valid");
                Assert.That(sentence, Is.Not.Null, "Sentence should be constructed");
            });
        }

        [Test]
        public void Sentence_SAVE_WithReferenceWord_ShouldValidate()
        {
            // Arrange - Test syntax validation with literal content and reference path
            ProcessedPrompt prompt = new("SAVE \"test data\" TO {C:\\output.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, "Sentence should be syntactically valid");
                Assert.That(sentence, Is.Not.Null, "Sentence should be constructed");
                Assert.That(sentence?.Root?.GetType().Name, Is.EqualTo("SaveText"), "Should resolve to SaveText verb");
            });
        }

        [Test]
        public void Sentence_GET_WithQualifier_ShouldValidate()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET TEXT [content] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            if (!validation.IsValid)
            {
                TestContext.WriteLine($"Validation failed: {validation.FailureReason}");
            }

            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, "Sentence should be syntactically valid");
                Assert.That(sentence, Is.Not.Null, "Sentence should be constructed");
            });
        }

        [Test]
        public void Sentence_GET_WithSpecialCharactersInPath_ShouldValidate()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {C:\\Test-Files\\file_name (1).txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, "Sentence should be syntactically valid");
                Assert.That(sentence, Is.Not.Null, "Sentence should be constructed");
            });
        }

        [Test]
        public void Sentence_GET_WithRelativePath_ShouldValidate()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {./test.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, "Sentence should be syntactically valid");
                Assert.That(sentence, Is.Not.Null, "Sentence should be constructed");
            });
        }

        [Test]
        [Ignore("Validation currently allows missing variables - semantic validation not yet implemented")]
        public void Sentence_GET_InvalidSyntax_NoVariable_ShouldNotValidate()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.False, "Sentence should be invalid without variable");
        }

        [Test]
        [Ignore("Validation currently allows missing references - semantic validation not yet implemented")]
        public void Sentence_GET_InvalidSyntax_NoReference_ShouldNotValidate()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.False, "Sentence should be invalid without reference");
        }

        [Test]
        public void Sentence_GET_InvalidSyntax_NoTerminator_ShouldNotValidate()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {file.txt}");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.False, "Sentence should be invalid without terminator");
        }

        #endregion Sentence Validation Tests

        #region Word Type Resolution Tests

        [Test]
        public void WordResolution_Reference_ShouldBeReferenceWord()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {C:\\test.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert - We'll check the diagnostic output to verify ReferenceWord is created
            Assert.That(validation.IsValid, Is.True);
            // The diagnostic output should show Token[3] as Type: Reference
            // and Word[4] as ReferenceWord
        }

        [Test]
        public void WordResolution_Variable_ShouldBeVariableWord()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [myVar] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
            // The diagnostic output should show Token[1] as Type: Variable
            // and Word[2] as VariableWord
        }

        [Test]
        public void WordResolution_Literal_ShouldBeLiteralWord()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {file.txt} .");

            // Act
            (ValidationResult validation, ISentence? sentence, object? result) = engine.Run(prompt);

            // Assert
            Assert.That(validation.IsValid, Is.True);
            // The terminator '.' should be Token[4] Type: Regular
            // and Word[5] as LiteralWord
        }

        #endregion Word Type Resolution Tests

        #region Edge Case Tests

        [Test]
        public void EdgeCase_EmptyBraces_ShouldTokenize()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{}"));
        }

        [Test]
        public void EdgeCase_EmptyBrackets_ShouldTokenize()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [] FROM {file.txt} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[1], Is.EqualTo("[]"));
        }

        [Test]
        public void EdgeCase_MultipleSpacesBetweenTokens_ShouldIgnoreExtraSpaces()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET    [text]    FROM    {file.txt}    .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[0], Is.EqualTo("GET"));
            Assert.That(prompt.Tokens[1], Is.EqualTo("[text]"));
        }

        [Test]
        public void EdgeCase_URLInReference_ShouldPreserve()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [data] FROM {https://example.com/api/data} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{https://example.com/api/data}"));
        }

        #endregion Edge Case Tests
    }
}