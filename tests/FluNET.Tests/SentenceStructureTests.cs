using FluNET.Prompt;
using FluNET.Tokens.Tree;

namespace FluNET.Tests
{
    /// <summary>
    /// Tests for sentence structure, token counting, and word patterns
    /// with various verb and preposition combinations.
    /// </summary>
    [TestFixture]

    public class SentenceStructureTests
    {
        [Test]
        public void TokenTree_SimpleGetSentence_CountsCorrectly()
        {
            ProcessedPrompt processed = new("GET data FROM file.");
            TokenTree tree = processed.ToTokenTree();

            // GET + data + FROM + file = 4 tokens
            Assert.That(tree.Count, Is.EqualTo(4));
        }

        [Test]
        public void TokenTree_PostSentence_CountsCorrectly()
        {
            ProcessedPrompt processed = new("POST json TO endpoint.");
            TokenTree tree = processed.ToTokenTree();

            // POST + json + TO + endpoint = 4 tokens
            Assert.That(tree.Count, Is.EqualTo(4));
        }

        [Test]
        public void TokenTree_SaveSentence_CountsCorrectly()
        {
            ProcessedPrompt processed = new("SAVE document TO output.txt.");
            TokenTree tree = processed.ToTokenTree();

            // SAVE + document + TO + output.txt = 4 tokens
            Assert.That(tree.Count, Is.EqualTo(4));
        }

        [Test]
        public void TokenTree_DeleteSentence_CountsCorrectly()
        {
            ProcessedPrompt processed = new("DELETE file FROM directory.");
            TokenTree tree = processed.ToTokenTree();

            // DELETE + file + FROM + directory = 4 tokens
            Assert.That(tree.Count, Is.EqualTo(4));
        }

        [Test]
        public void TokenTree_LoadSentence_CountsCorrectly()
        {
            ProcessedPrompt processed = new("LOAD config FROM settings.json.");
            TokenTree tree = processed.ToTokenTree();

            // LOAD + config + FROM + settings.json = 4 tokens
            Assert.That(tree.Count, Is.EqualTo(4));
        }

        [Test]
        public void TokenTree_SendSentence_CountsCorrectly()
        {
            ProcessedPrompt processed = new("SEND message TO recipient.");
            TokenTree tree = processed.ToTokenTree();

            // SEND + message + TO + recipient = 4 tokens
            Assert.That(tree.Count, Is.EqualTo(4));
        }

        [Test]
        public void TokenTree_TransformSentence_CountsCorrectly()
        {
            ProcessedPrompt processed = new("TRANSFORM data USING algorithm.");
            TokenTree tree = processed.ToTokenTree();

            // TRANSFORM + data + USING + algorithm = 4 tokens
            Assert.That(tree.Count, Is.EqualTo(4));
        }

        [Test]
        public void TokenTree_LongerSentence_CountsCorrectly()
        {
            ProcessedPrompt processed = new("GET the user profile data FROM remote database server.");
            TokenTree tree = processed.ToTokenTree();

            // GET + the + user + profile + data + FROM + remote + database + server = 9 tokens
            Assert.That(tree.Count, Is.EqualTo(9));
        }

        [Test]
        public void TokenTree_WithCommas_CountsWords()
        {
            ProcessedPrompt processed = new("POST data, metadata, headers TO endpoint.");
            TokenTree tree = processed.ToTokenTree();

            // Depends on tokenization - commas might be separate or included
            Assert.That(tree.Count, Is.GreaterThan(4));
        }

        [Test]
        public void TokenTree_WithUrl_CountsAsOneToken()
        {
            ProcessedPrompt processed = new("POST data TO https://api.example.com/endpoint.");
            TokenTree tree = processed.ToTokenTree();

            // POST + data + TO + https://api.example.com/endpoint = 4 tokens
            Assert.That(tree.Count, Is.EqualTo(4));
        }

        [Test]
        public void TokenTree_MultiplePrepositions_CountsCorrectly()
        {
            ProcessedPrompt processed = new("GET data FROM source TO destination.");
            TokenTree tree = processed.ToTokenTree();

            // GET + data + FROM + source + TO + destination = 6 tokens
            Assert.That(tree.Count, Is.EqualTo(6));
        }

        [Test]
        public void TokenTree_NavigationChain_IsCorrect()
        {
            ProcessedPrompt processed = new("SAVE file TO disk.");
            TokenTree tree = processed.ToTokenTree();

            List<Tokens.Token> tokens = tree.GetTokens().ToList();

            Assert.Multiple(() =>
            {
                // Check we can navigate through the chain
                Assert.That(tokens.Count, Is.GreaterThan(0));

                // The actual tokens (not ROOT/TERMINAL) should be in the chain
                Assert.That(tokens, Is.Not.Empty);

                // Verify the content tokens are present
                List<string> tokenValues = tokens.Select(t => t.Value).ToList();
                Assert.That(tokenValues, Does.Contain("SAVE"));
                Assert.That(tokenValues, Does.Contain("file"));
                Assert.That(tokenValues, Does.Contain("TO"));
            });
        }

        [Test]
        public void TokenTree_ToString_ReconstructsSentence()
        {
            string original = "GET data FROM source.";
            ProcessedPrompt processed = new(original);
            TokenTree tree = processed.ToTokenTree();

            string reconstructed = tree.ToString();

            // Should include ROOT and TERMINAL in output
            Assert.That(reconstructed, Does.Contain("GET"));
            Assert.That(reconstructed, Does.Contain("data"));
            Assert.That(reconstructed, Does.Contain("FROM"));
        }

        [Test]
        public void ProcessedPrompt_ToString_PreservesOriginal()
        {
            string original = "POST json TO https://api.example.com/endpoint.";
            ProcessedPrompt processed = new(original);

            Assert.That(processed.ToString(), Is.EqualTo(original));
        }

        [Test]
        public void TokenTree_EmptyPrompt_HasZeroCount()
        {
            ProcessedPrompt processed = new("");
            TokenTree tree = processed.ToTokenTree();

            Assert.That(tree.Count, Is.EqualTo(0));
        }

        [Test]
        public void TokenTree_WhitespaceOnly_HasZeroCount()
        {
            ProcessedPrompt processed = new("   ");
            TokenTree tree = processed.ToTokenTree();

            Assert.That(tree.Count, Is.EqualTo(0));
        }

        [Test]
        public void TokenTree_DifferentTerminators_AllHandled()
        {
            string[] sentences = new[]
            {
                "GET data FROM source.",
                "POST data TO endpoint?",
                "DELETE file FROM disk!"
            };

            foreach (string? sentence in sentences)
            {
                ProcessedPrompt processed = new(sentence);
                TokenTree tree = processed.ToTokenTree();

                Assert.That(tree.Count, Is.GreaterThan(0),
                    $"Sentence '{sentence}' should have tokens");
            }
        }

        [Test]
        public void TokenTree_SpecialCharacters_InValues_Handled()
        {
            ProcessedPrompt processed = new("SAVE data TO C:\\Users\\file.txt.");
            TokenTree tree = processed.ToTokenTree();

            Assert.That(tree.Count, Is.GreaterThan(0));
        }

        [Test]
        public void TokenTree_EmailAddress_CountedCorrectly()
        {
            ProcessedPrompt processed = new("SEND message TO user@example.com.");
            TokenTree tree = processed.ToTokenTree();

            // SEND + message + TO + user@example.com = 4 tokens
            Assert.That(tree.Count, Is.EqualTo(4));
        }

        [Test]
        public void TokenTree_NumbersAndSymbols_CountedCorrectly()
        {
            ProcessedPrompt processed = new("POST value123 TO endpoint456.");
            TokenTree tree = processed.ToTokenTree();

            // POST + value123 + TO + endpoint456 = 4 tokens
            Assert.That(tree.Count, Is.EqualTo(4));
        }
    }
}
