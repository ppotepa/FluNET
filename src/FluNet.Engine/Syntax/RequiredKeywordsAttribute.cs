
using FluNET.Keywords;

namespace FluNET.Syntax
{
    public class RequiredKeywordsAttribute : Attribute
    {
        public RequiredKeywordsAttribute(IKeyword[] keywords)
        {
            this.Keywords = keywords;
        }

        internal IKeyword[] Keywords { get; }
    }
}