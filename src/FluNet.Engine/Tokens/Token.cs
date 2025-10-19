namespace FluNET.Tokens
{
    public record Token(string Value, TokenType Type)
    {
        public string Value { get; internal set; } = Value;
        public TokenType Type { get; internal set; } = Type;
        public Token? Next { get; set; }
        public Token? Previous { get; set; }

        public static Token CreateRoot()
        {
            return new Token("ROOT", TokenType.Root);
        }

        public static Token CreateTerminal()
        {
            return new Token("TERMINAL", TokenType.Terminal);
        }
    }
}