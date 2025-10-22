using FluNET.Prompt;

namespace FluNET.IntegrationTests
{
    /// <summary>
    /// Tests for tokenization with new {reference} and [variable] syntax.
    /// Verifies that the brace-aware tokenization correctly handles various patterns.
    /// </summary>
    [TestFixture]
    public class TokenizationTests
    {
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
        public void Tokenization_NoSpaceBeforeTerminator_ShouldAttachPeriod()
        {
            // Arrange - this is WRONG syntax but should still tokenize
            ProcessedPrompt prompt = new("GET [text] FROM {file.txt}.");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(4));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{file.txt}."));
        }

        [Test]
        public void Tokenization_EmptyBraces_ShouldTokenize()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{}"));
        }

        [Test]
        public void Tokenization_EmptyBrackets_ShouldTokenize()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [] FROM {file.txt} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[1], Is.EqualTo("[]"));
        }

        [Test]
        public void Tokenization_MultipleSpacesBetweenTokens_ShouldIgnoreExtraSpaces()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET    [text]    FROM    {file.txt}    .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[0], Is.EqualTo("GET"));
            Assert.That(prompt.Tokens[1], Is.EqualTo("[text]"));
        }

        [Test]
        public void Tokenization_URLInReference_ShouldPreserve()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [data] FROM {https://example.com/api/data} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{https://example.com/api/data}"));
        }

        [Test]
        public void Tokenization_ComplexPathWithMultipleSpaces_ShouldPreserve()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {C:\\Program Files\\My App\\data file.txt} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{C:\\Program Files\\My App\\data file.txt}"));
        }

        [Test]
        public void Tokenization_NestedVariableInReference_ShouldPreserve()
        {
            // Arrange
            ProcessedPrompt prompt = new("GET [text] FROM {[basePath]/file.txt} .");

            // Assert
            Assert.That(prompt.Tokens.Length, Is.EqualTo(5));
            Assert.That(prompt.Tokens[3], Is.EqualTo("{[basePath]/file.txt}"));
        }
    }
}
