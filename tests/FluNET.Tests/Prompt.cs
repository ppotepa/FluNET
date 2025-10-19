using FluNET.Prompt;
using FluNET.Tokens.Tree;

namespace FluNET.Tests
{
    [TestFixture]

    public class Prompt
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestTokenCount_EmptyPrompt()
        {
            ProcessedPrompt processed = new("");
            TokenTree tree = processed.ToTokenTree();
            Assert.That(tree.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestTokenCount_SingleWord()
        {
            ProcessedPrompt processed = new("hello");
            TokenTree tree = processed.ToTokenTree();
            Assert.That(tree.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestTokenCount_MultipleWords()
        {
            ProcessedPrompt processed = new("hello world this is a test");
            TokenTree tree = processed.ToTokenTree();
            Assert.That(tree.Count, Is.EqualTo(6));
        }

        [Test]
        public void TestTokenCount_WithPunctuation()
        {
            ProcessedPrompt processed = new("Hello, world!");
            TokenTree tree = processed.ToTokenTree();
            Assert.That(tree.Count, Is.EqualTo(2)); // Assuming punctuation is not counted as separate tokens
        }

        [Test]
        public void TestTokenCount_LongerPrompt()
        {
            ProcessedPrompt processed = new("This is a longer prompt with more words to test the token count.");
            TokenTree tree = processed.ToTokenTree();
            Assert.That(tree.Count, Is.EqualTo(13));
        }

        [Test]
        public void TestToStringMatchesPrompt()
        {
            string originalPrompt = "This is a test prompt with many words to reach approximately " +
                "fifty in total count for the purpose of verifying that the ProcessedPrompt class " +
                "handles longer strings correctly without any issues or errors in tokenization or " +
                "string representation.";

            ProcessedPrompt processed = new(originalPrompt);
            Assert.That(processed.ToString(), Is.EqualTo(originalPrompt));
        }
    }
}
