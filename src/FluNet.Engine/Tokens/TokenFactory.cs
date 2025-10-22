namespace FluNET.Tokens
{
    public class TokenFactory
    {
        public TokenFactory()
        {
        }

        internal Token CreateToken(RawToken rawToken)
        {
            string value = rawToken.Value;

            // Strip trailing period for pattern matching (will be preserved in value)
            string valueWithoutPeriod = value.TrimEnd('.');
            bool hasPeriod = valueWithoutPeriod.Length < value.Length;

            // Check for variable pattern [variable] or [variable].
            if (valueWithoutPeriod.StartsWith('[') && valueWithoutPeriod.EndsWith(']'))
            {
                return new Token(value, TokenType.Variable);
            }

            // Check for reference pattern {reference} or {reference}.
            if (valueWithoutPeriod.StartsWith('{') && valueWithoutPeriod.EndsWith('}'))
            {
                return new Token(value, TokenType.Reference);
            }

            // Default to regular token
            return new Token(value, TokenType.Regular);
        }
    }
}