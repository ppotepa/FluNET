
namespace FluNET.Tokens
{
    public enum TokenType
    {
        Root,
        Regular,
        Variable,   // [variable] pattern
        Reference,  // {reference} pattern for file paths, URLs, etc.
        Terminal
    }
}