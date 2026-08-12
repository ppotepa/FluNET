using FluNET.Syntax.Core;
using FluNET.Words;
using FluNET.Capabilities;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of POST verb for sending JSON data to an HTTP endpoint.
    /// Usage: POST [json] TO [https://api.example.com/endpoint]
    /// </summary>
    public class PostJson : Post<string, Uri>, IAsyncVerb
    {
        private readonly IHttpTransport _http;
        /// <summary>
        /// Parameterless constructor for WordFactory discovery.
        /// </summary>
        public PostJson() : this(string.Empty, new Uri("http://temp"), DefaultCapabilities.Http)
        {
        }

        /// <summary>
        /// Initializes a new instance of PostJson.
        /// </summary>
        /// <param name="what">The JSON string to post</param>
        /// <param name="to">The URI endpoint to post to</param>
        public PostJson(string what, Uri to) : this(what, to, DefaultCapabilities.Http)
        {
        }

        public PostJson(string what, Uri to, IHttpTransport http) : base(what, to)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
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
                    return _http.PostJsonAsync(uri, What).GetAwaiter().GetResult();
                };
            }
        }

        public async ValueTask<object?> InvokeAsync(CancellationToken cancellationToken = default) =>
            await _http.PostJsonAsync(To, What, cancellationToken).ConfigureAwait(false);

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
