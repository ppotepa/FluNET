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
                    using (var client = new HttpClient())
                    {
                        var content = new StringContent(What, System.Text.Encoding.UTF8, "application/json");
                        var response = client.PostAsync(uri, content).Result;
                        return response.Content.ReadAsStringAsync().Result;
                    }
                };
            }
        }
    }
}
