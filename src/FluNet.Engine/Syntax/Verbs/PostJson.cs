using FluNET.Syntax.Core;
using FluNET.Words;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of POST verb for sending JSON data to an HTTP endpoint.
    /// Usage: POST [json] TO [https://api.example.com/endpoint]
    /// </summary>
    public class PostJson : Post<string, Uri>
    {
        /// <summary>
        /// Initializes a new instance of PostJson.
        /// </summary>
        /// <param name="what">The JSON string to post</param>
        /// <param name="to">The URI endpoint to post to</param>
        public PostJson(string what, Uri to) : base(what, to)
        {
        }

        /// <summary>
        /// Gets the action function that posts JSON data to a URI endpoint.
        /// </summary>
        public override Func<Uri, string> Act
        {
            get
            {
                return (uri) =>
                {
                    using (HttpClient client = new())
                    {
                        StringContent content = new(What, System.Text.Encoding.UTF8, "application/json");
                        HttpResponseMessage response = client.PostAsync(uri, content).Result;
                        return response.Content.ReadAsStringAsync().Result;
                    }
                };
            }
        }

        /// <summary>
        /// Validates that the word represents a valid URI endpoint.
        /// </summary>
        public override bool Validate(IWord word)
        {
            // For HTTP POST, accept any URI or string that looks like a URL
            return word is LiteralWord or VariableWord or ReferenceWord;
        }

        /// <summary>
        /// Resolves a string value to Uri for HTTP endpoints.
        /// </summary>
        public override Uri? Resolve(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null;
        }

        /// <summary>
        /// Resolves a ReferenceWord to Uri.
        /// </summary>
        public Uri? Resolve(ReferenceWord reference)
        {
            return reference.ResolveAs<Uri>();
        }
    }
}