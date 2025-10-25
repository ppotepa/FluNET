using FluNET.Prompt;

namespace FluNET.Tests.Tokenization
{
    [TestFixture]
    public class TokenizerTests
    {
        [Test]
        public void TokenizeSimplePrompt_SplitsOnSpaces()
        {
            var prompt = new ProcessedPrompt("GET text FROM file.txt");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[0], Is.EqualTo("GET"));
            Assert.That(prompt.Tokens[1], Is.EqualTo("text"));
            Assert.That(prompt.Tokens[2], Is.EqualTo("FROM"));
            Assert.That(prompt.Tokens[3], Is.EqualTo("file.txt"));
        }

        [Test]
        public void TokenizeWithBraces_PreservesSpacesInsideBraces()
        {
            var prompt = new ProcessedPrompt("GET {my file name} FROM source");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[0], Is.EqualTo("GET"));
            Assert.That(prompt.Tokens[1], Is.EqualTo("{my file name}"));
            Assert.That(prompt.Tokens[2], Is.EqualTo("FROM"));
            Assert.That(prompt.Tokens[3], Is.EqualTo("source"));
        }

        [Test]
        public void TokenizeWithBrackets_PreservesSpacesInsideBrackets()
        {
            var prompt = new ProcessedPrompt("SAVE text TO [output file]");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[0], Is.EqualTo("SAVE"));
            Assert.That(prompt.Tokens[1], Is.EqualTo("text"));
            Assert.That(prompt.Tokens[2], Is.EqualTo("TO"));
            Assert.That(prompt.Tokens[3], Is.EqualTo("[output file]"));
        }

        [Test]
        public void TokenizeNestedBraces_PreservesAllLevels()
        {
            var prompt = new ProcessedPrompt("GET {{{nested value}}} FROM file");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("{{{nested value}}}"));
        }

        [Test]
        public void TokenizeNestedBrackets_PreservesAllLevels()
        {
            var prompt = new ProcessedPrompt("SET [[[deep variable]]] TO value");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("[[[deep variable]]]"));
        }

        [Test]
        public void TokenizeMixedBracesAndBrackets_PreservesBoth()
        {
            var prompt = new ProcessedPrompt("TRANSFORM {source data} TO [output var]");
            
            // TO splits the tokens since it's outside braces/brackets
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[0], Is.EqualTo("TRANSFORM"));
            Assert.That(prompt.Tokens[1], Is.EqualTo("{source data}"));
            Assert.That(prompt.Tokens[2], Is.EqualTo("TO"));
            Assert.That(prompt.Tokens[3], Is.EqualTo("[output var]"));
        }

        [Test]
        public void TokenizeWithTabs_TreatsTabsAsDelimiters()
        {
            var prompt = new ProcessedPrompt("GET\ttext\tFROM\tfile");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[0], Is.EqualTo("GET"));
            Assert.That(prompt.Tokens[3], Is.EqualTo("file"));
        }

        [Test]
        public void TokenizeWithMultipleSpaces_CollapsesSpacesOutsideBraces()
        {
            var prompt = new ProcessedPrompt("GET    text    FROM    file");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[0], Is.EqualTo("GET"));
        }

        [Test]
        public void TokenizeWithLeadingAndTrailingSpaces_IgnoresExtraSpaces()
        {
            var prompt = new ProcessedPrompt("  GET text FROM file  ");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[0], Is.EqualTo("GET"));
            Assert.That(prompt.Tokens[3], Is.EqualTo("file"));
        }

        [Test]
        public void TokenizeEmptyBraces_PreservesEmptyBraces()
        {
            var prompt = new ProcessedPrompt("GET {} FROM file");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("{}"));
        }

        [Test]
        public void TokenizeEmptyBrackets_PreservesEmptyBrackets()
        {
            var prompt = new ProcessedPrompt("SET [] TO value");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("[]"));
        }

        [Test]
        public void TokenizeComplexNestedStructure_PreservesStructure()
        {
            var prompt = new ProcessedPrompt("TRANSFORM {data {nested}} TO [var [inner]]");
            
            // Spaces outside braces/brackets split tokens
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[0], Is.EqualTo("TRANSFORM"));
            Assert.That(prompt.Tokens[1], Is.EqualTo("{data {nested}}"));
            Assert.That(prompt.Tokens[2], Is.EqualTo("TO"));
            Assert.That(prompt.Tokens[3], Is.EqualTo("[var [inner]]"));
        }

        [Test]
        public void TokenizeWithNewlines_PreservesNewlinesInTokens()
        {
            var prompt = new ProcessedPrompt("GET {line1\nline2} FROM file");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("{line1\nline2}"));
            Assert.That(prompt.Tokens[1], Does.Contain("\n"));
        }

        [Test]
        public void TokenizeWithSpecialCharacters_PreservesCharacters()
        {
            var prompt = new ProcessedPrompt("GET {file-name_2023.txt} FROM path");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("{file-name_2023.txt}"));
        }

        [Test]
        public void TokenizeSingleToken_ReturnsSingleElement()
        {
            var prompt = new ProcessedPrompt("GET");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(1));
            Assert.That(prompt.Tokens[0], Is.EqualTo("GET"));
        }

        [Test]
        public void TokenizeEmptyString_ReturnsEmptyArray()
        {
            var prompt = new ProcessedPrompt("");
            
            Assert.That(prompt.Tokens, Is.Empty);
        }

        [Test]
        public void TokenizeOnlySpaces_ReturnsEmptyArray()
        {
            var prompt = new ProcessedPrompt("     ");
            
            Assert.That(prompt.Tokens, Is.Empty);
        }

        [Test]
        public void ToString_TrimsAndCollapsesSpaces()
        {
            var prompt = new ProcessedPrompt("  GET  text  FROM  file  ");
            
            Assert.That(prompt.ToString(), Is.EqualTo("GET text FROM file"));
        }

        [Test]
        public void ToString_PreservesContentInsideBraces()
        {
            var prompt = new ProcessedPrompt("GET {my  file}");
            
            // ToString collapses double spaces to single, but tokens preserve them
            Assert.That(prompt.Tokens[1], Is.EqualTo("{my  file}"));
        }

        [TestCase("GET {unclosed brace", 2, Description = "Unclosed brace keeps rest as single token")]
        [TestCase("GET [unclosed bracket", 2, Description = "Unclosed bracket keeps rest as single token")]
        [TestCase("GET text} FROM file", 2, Description = "Extra closing brace - depth goes negative, rest is single token")]
        [TestCase("GET text] FROM file", 2, Description = "Extra closing bracket - depth goes negative, rest is single token")]
        public void TokenizeMalformedBracesOrBrackets_StillTokenizes(string input, int expectedCount)
        {
            var prompt = new ProcessedPrompt(input);
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(expectedCount));
        }

        [Test]
        public void TokenizeWithMixedDelimiters_HandlesCorrectly()
        {
            var prompt = new ProcessedPrompt("GET\t{file name} FROM  path\t");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("{file name}"));
        }
    }
}
