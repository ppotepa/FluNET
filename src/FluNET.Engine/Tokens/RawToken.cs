namespace FluNET.Tokens
{
    internal class RawToken
    {
        public string Value { get; private set; }

        private RawToken(string value)
        {
            Value = value;
        }

        internal static RawToken Create(string source)
        {
            return new RawToken(source);
        }
    }
}