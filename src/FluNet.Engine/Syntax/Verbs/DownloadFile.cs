using FluNET.Syntax.Core;
using FluNET.Words;
using System.Diagnostics;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of the DOWNLOAD verb for downloading files from HTTP/HTTPS URLs.
    /// Usage: DOWNLOAD [file] FROM {https://example.com/file.zip} TO {destination.zip}
    /// If TO is omitted, the filename is extracted from the URL.
    /// </summary>
    public class DownloadFile : Download<FileInfo, Uri, FileInfo>
    {
        private readonly string? _destinationPath;

        /// <summary>
        /// Parameterless constructor for WordFactory discovery.
        /// </summary>
        public DownloadFile() : base(new FileInfo("temp"), new Uri("http://temp"), null)
        {
        }

        /// <summary>
        /// Initializes a new instance of DownloadFile.
        /// </summary>
        /// <param name="what">The file being downloaded (result)</param>
        /// <param name="from">The URI from which to download</param>
        /// <param name="to">The destination file (optional)</param>
        public DownloadFile(FileInfo what, Uri from, FileInfo? to = null) : base(what, from, to)
        {
            _destinationPath = to?.FullName;
        }

        /// <summary>
        /// Gets the action function that downloads a file from a URI.
        /// </summary>
        public override Func<Uri, FileInfo> Act
        {
            get
            {
                return (uri) =>
                {
                    try
                    {
                        // Determine destination path
                        string destinationPath = _destinationPath ?? ExtractFilename(uri);

                        // Ensure the directory exists
                        string? directory = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        Debug.WriteLine($"Downloading from {uri} to {destinationPath}");

                        // Download the file
                        using (HttpClient client = new())
                        {
                            client.Timeout = TimeSpan.FromMinutes(5); // 5 minute timeout
                            byte[] fileData = client.GetByteArrayAsync(uri).Result;
                            File.WriteAllBytes(destinationPath, fileData);

                            Debug.WriteLine($"Successfully downloaded {fileData.Length} bytes");
                        }

                        return new FileInfo(destinationPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Download failed: {ex.Message}");
                        throw new InvalidOperationException($"Failed to download file from {uri}: {ex.Message}", ex);
                    }
                };
            }
        }

        /// <summary>
        /// Validates that the word represents a valid URI or file path.
        /// </summary>
        public override bool Validate(IWord word)
        {
            // Accept literals (URLs), variables, and references
            if (word is LiteralWord literalWord)
            {
                // Check if it looks like a URL or file path
                return !string.IsNullOrWhiteSpace(literalWord.Value.TrimEnd('.', '?', '!'));
            }

            if (word is VariableWord)
            {
                return true;
            }

            return word is ReferenceWord referenceWord && !string.IsNullOrWhiteSpace(referenceWord.Reference);
        }

        /// <summary>
        /// Resolves a string value to Uri for HTTP/HTTPS downloads.
        /// </summary>
        /// <param name="value">The URL string</param>
        /// <returns>Uri instance, or null if invalid</returns>
        public override Uri? Resolve(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                // Try to create an absolute URI
                if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
                {
                    // Only accept HTTP/HTTPS schemes
                    if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    {
                        return uri;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves a string value to FileInfo for the destination.
        /// </summary>
        /// <param name="value">The destination file path</param>
        /// <returns>FileInfo instance, or null if invalid</returns>
        public override FileInfo? ResolveTo(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                return new FileInfo(value);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts the filename from the URI if no destination is specified.
        /// </summary>
        /// <param name="source">The source URI</param>
        /// <returns>The extracted filename</returns>
        protected override string ExtractFilename(Uri source)
        {
            try
            {
                // Get the last segment of the URI path
                string filename = Path.GetFileName(source.LocalPath);

                // If no filename found, use a default
                if (string.IsNullOrWhiteSpace(filename) || filename == "/" || filename == "\\")
                {
                    filename = "downloaded_file";
                }

                // If no extension, try to add one based on content type or use .bin
                if (!Path.HasExtension(filename))
                {
                    filename += ".bin";
                }

                // Ensure we're saving to the current directory
                return Path.Combine(Directory.GetCurrentDirectory(), filename);
            }
            catch
            {
                // Fallback to a default filename
                return Path.Combine(Directory.GetCurrentDirectory(), "downloaded_file.bin");
            }
        }

        /// <summary>
        /// Resolves a ReferenceWord to Uri for downloads.
        /// </summary>
        public Uri? Resolve(ReferenceWord reference)
        {
            return reference.ResolveAs<Uri>();
        }

        /// <summary>
        /// Resolves a ReferenceWord to FileInfo for destination.
        /// </summary>
        public FileInfo? ResolveToFile(ReferenceWord reference)
        {
            return reference.ResolveAs<FileInfo>();
        }
    }
}