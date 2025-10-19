
using FluNET.Keywords;

namespace FluNET.Syntax
{
    /// <summary>
    /// Attribute to mark which keywords are required for a particular verb or language construct.
    /// Used for compile-time or runtime verification of language rules.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class RequiredKeywordsAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance with the specified required keywords.
        /// </summary>
        /// <param name="keywords">Array of keywords that are required</param>
        public RequiredKeywordsAttribute(IKeyword[] keywords)
        {
            this.Keywords = keywords;
        }

        /// <summary>
        /// Gets the array of required keywords.
        /// </summary>
        internal IKeyword[] Keywords { get; }
    }
}