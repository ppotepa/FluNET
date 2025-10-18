namespace FluNET.Tests
{   
    using FluNET;
    using FluNET.Prompt;
    using FluNET.TokenTree;

    public class Prompt
    {
        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public void TestTokenCount_EmptyPrompt()
        {
            var processed = new ProcessedPrompt("");
            var tree = processed.ToTokenTree();
            Assert.That(tree.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestTokenCount_SingleWord()
        {
            var processed = new ProcessedPrompt("hello");
            var tree = processed.ToTokenTree();
            Assert.That(tree.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestTokenCount_MultipleWords()
        {
            var processed = new ProcessedPrompt("hello world this is a test");
            var tree = processed.ToTokenTree();
            Assert.That(tree.Count, Is.EqualTo(6));
        }

        [Test]
        public void TestTokenCount_WithPunctuation()
        {
            var processed = new ProcessedPrompt("Hello, world!");
            var tree = processed.ToTokenTree();
            Assert.That(tree.Count, Is.EqualTo(2)); // Assuming punctuation is not counted as separate tokens
        }

        [Test]
        public void TestTokenCount_LongerPrompt()
        {
            var processed = new ProcessedPrompt("This is a longer prompt with more words to test the token count.");
            var tree = processed.ToTokenTree();
            Assert.That(tree.Count, Is.EqualTo(13));
        }

        [Test]
        public void TestToStringMatchesPrompt()
        {
            string originalPrompt = "This is a test prompt with many words to reach approximately " +
                "fifty in total count for the purpose of verifying that the ProcessedPrompt class " +
                "handles longer strings correctly without any issues or errors in tokenization or " +
                "string representation.";

            var processed = new ProcessedPrompt(originalPrompt);
            Assert.That(processed.ToString(), Is.EqualTo(originalPrompt));
        }
    }
}