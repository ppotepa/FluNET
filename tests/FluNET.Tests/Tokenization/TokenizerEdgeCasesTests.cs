using FluNET.Prompt;

namespace FluNET.Tests.Tokenization
{
    [TestFixture]
    public class TokenizerEdgeCasesTests
    {
        [Test]
        public void TokenizeWithQuotedString_PreservesOneLiteralToken()
        {
            var prompt = new ProcessedPrompt("SAY \"hello world\" TO user");

            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("\"hello world\""));
        }

        [Test]
        public void TokenizeWithSingleQuotes_PreservesOneLiteralToken()
        {
            var prompt = new ProcessedPrompt("SAY 'hello world' TO user");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("'hello world'"));
        }

        [Test]
        public void TokenizeWithQuotesInsideBraces_PreservesEverything()
        {
            var prompt = new ProcessedPrompt("GET {\"file name\"} FROM path");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("{\"file name\"}"));
        }

        [Test]
        public void TokenizeWithBracesInsideQuotes_PreservesQuotedToken()
        {
            var prompt = new ProcessedPrompt("SAY \"{value}\" TO user");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("\"{value}\""));
        }

        [Test]
        public void TokenizeWithBackslash_PreservesBackslash()
        {
            var prompt = new ProcessedPrompt(@"GET file\path.txt FROM source");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo(@"file\path.txt"));
        }

        [Test]
        public void TokenizeWithUnicodeCharacters_PreservesUnicode()
        {
            var prompt = new ProcessedPrompt("SAY {Hello 世界} TO user");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("{Hello 世界}"));
        }

        [Test]
        public void TokenizeWithEmoji_PreservesEmoji()
        {
            var prompt = new ProcessedPrompt("SAY {Hello 👋} TO user");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("{Hello 👋}"));
        }

        [Test]
        public void TokenizeWithTrailingPeriod_SeparatesTerminator()
        {
            var prompt = new ProcessedPrompt("GET text FROM file.txt.");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(5));
            Assert.That(prompt.Tokens[3], Is.EqualTo("file.txt"));
            Assert.That(prompt.Tokens[4], Is.EqualTo("."));
        }

        [Test]
        public void TokenizeWithMultiplePeriods_PreservesAll()
        {
            var prompt = new ProcessedPrompt("GET text FROM file...txt");
            
            Assert.That(prompt.Tokens[3], Is.EqualTo("file...txt"));
        }

        [Test]
        public void TokenizeWithComma_KeepsCommaWithToken()
        {
            var prompt = new ProcessedPrompt("GET item1,item2,item3 FROM list");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("item1,item2,item3"));
        }

        [Test]
        public void TokenizeWithSemicolon_KeepsSemicolonWithToken()
        {
            var prompt = new ProcessedPrompt("EXECUTE cmd1;cmd2;cmd3");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(2));
            Assert.That(prompt.Tokens[1], Is.EqualTo("cmd1;cmd2;cmd3"));
        }

        [Test]
        public void TokenizeWithEqualsSign_PreservesEquals()
        {
            var prompt = new ProcessedPrompt("SET variable=value");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(2));
            Assert.That(prompt.Tokens[1], Is.EqualTo("variable=value"));
        }

        [Test]
        public void TokenizeWithAtSymbol_PreservesAtSymbol()
        {
            var prompt = new ProcessedPrompt("SEND email TO user@example.com");
            
            Assert.That(prompt.Tokens[3], Is.EqualTo("user@example.com"));
        }

        [Test]
        public void TokenizeWithHashSymbol_PreservesHashSymbol()
        {
            var prompt = new ProcessedPrompt("GET item #123 FROM list");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(5));
            Assert.That(prompt.Tokens[2], Is.EqualTo("#123"));
        }

        [Test]
        public void TokenizeWithDollarSign_PreservesDollarSign()
        {
            var prompt = new ProcessedPrompt("GET $variable FROM context");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("$variable"));
        }

        [Test]
        public void TokenizeWithPercentSign_PreservesPercentSign()
        {
            var prompt = new ProcessedPrompt("SET value TO 100%");
            
            Assert.That(prompt.Tokens[3], Is.EqualTo("100%"));
        }

        [Test]
        public void TokenizeWithAmpersand_PreservesAmpersand()
        {
            var prompt = new ProcessedPrompt("GET item1&item2 FROM list");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("item1&item2"));
        }

        [Test]
        public void TokenizeWithPlusSign_PreservesPlusSign()
        {
            var prompt = new ProcessedPrompt("CALCULATE 1+2+3");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("1+2+3"));
        }

        [Test]
        public void TokenizeWithHyphen_PreservesHyphen()
        {
            var prompt = new ProcessedPrompt("GET file-name.txt FROM source");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("file-name.txt"));
        }

        [Test]
        public void TokenizeWithUnderscore_PreservesUnderscore()
        {
            var prompt = new ProcessedPrompt("GET my_file_name.txt FROM source");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("my_file_name.txt"));
        }

        [Test]
        public void TokenizeWithParentheses_PreservesParentheses()
        {
            var prompt = new ProcessedPrompt("CALL function(arg1,arg2)");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("function(arg1,arg2)"));
        }

        [Test]
        public void TokenizeWithAngleBrackets_PreservesAngleBrackets()
        {
            var prompt = new ProcessedPrompt("GET <value> FROM xml");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[1], Is.EqualTo("<value>"));
        }

        [Test]
        public void TokenizeWithPipe_PreservesPipe()
        {
            var prompt = new ProcessedPrompt("FILTER data|process|output");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("data|process|output"));
        }

        [Test]
        public void TokenizeWithCarriageReturn_PreservesInToken()
        {
            var prompt = new ProcessedPrompt("GET {line1\r\nline2} FROM file");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("{line1\r\nline2}"));
        }

        [Test]
        public void TokenizeWithFormFeed_PreservesInToken()
        {
            var prompt = new ProcessedPrompt("GET {page1\fpage2} FROM doc");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("{page1\fpage2}"));
        }

        [Test]
        public void TokenizeRealWorldExample_HttpUrl()
        {
            var prompt = new ProcessedPrompt("DOWNLOAD FROM https://example.com/api/data?key=123&format=json");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(3));
            Assert.That(prompt.Tokens[2], Is.EqualTo("https://example.com/api/data?key=123&format=json"));
        }

        [Test]
        public void TokenizeRealWorldExample_FilePath()
        {
            var prompt = new ProcessedPrompt(@"SAVE data TO C:\Users\Documents\output.txt");
            
            Assert.That(prompt.Tokens, Has.Length.EqualTo(4));
            Assert.That(prompt.Tokens[3], Is.EqualTo(@"C:\Users\Documents\output.txt"));
        }

        [Test]
        public void TokenizeRealWorldExample_JsonLikeStructure()
        {
            var prompt = new ProcessedPrompt("POST {\"name\":\"John\",\"age\":30} TO api");
            
            Assert.That(prompt.Tokens[1], Is.EqualTo("{\"name\":\"John\",\"age\":30}"));
        }
    }
}
