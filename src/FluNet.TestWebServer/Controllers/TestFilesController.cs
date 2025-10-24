using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace FluNET.TestWebServer.Controllers
{
    /// <summary>
    /// Controller providing test files for DOWNLOAD command testing.
    /// Serves various file types (text, JSON, binary) to validate download functionality.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TestFilesController : ControllerBase
    {
        /// <summary>
        /// Serves a simple text file for basic download testing.
        /// </summary>
        [HttpGet("testfile.txt")]
        public IActionResult GetTextFile()
        {
            string content = "This is a test file for download testing.";
            return File(Encoding.UTF8.GetBytes(content), "text/plain", "testfile.txt");
        }

        /// <summary>
        /// Serves a JSON file with sample data.
        /// </summary>
        [HttpGet("data.json")]
        public IActionResult GetJsonFile()
        {
            var data = new
            {
                Name = "Test Data",
                Version = "1.0",
                Items = new[]
                {
                    new { Id = 1, Value = "First" },
                    new { Id = 2, Value = "Second" },
                    new { Id = 3, Value = "Third" }
                },
                Metadata = new
                {
                    CreatedAt = DateTime.UtcNow,
                    Type = "Test",
                    IsValid = true
                }
            };

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return File(Encoding.UTF8.GetBytes(json), "application/json", "data.json");
        }

        /// <summary>
        /// Serves a larger JSON file for performance testing.
        /// </summary>
        [HttpGet("largefile.json")]
        public IActionResult GetLargeJsonFile()
        {
            var items = Enumerable.Range(1, 1000).Select(i => new
            {
                Id = i,
                Name = $"Item_{i}",
                Description = $"This is test item number {i}",
                Timestamp = DateTime.UtcNow.AddSeconds(-i),
                IsActive = i % 2 == 0,
                Tags = new[] { $"tag{i % 10}", $"category{i % 5}" }
            });

            string json = JsonSerializer.Serialize(items, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return File(Encoding.UTF8.GetBytes(json), "application/json", "largefile.json");
        }

        /// <summary>
        /// Serves a binary file (PNG image header).
        /// </summary>
        [HttpGet("image.png")]
        public IActionResult GetImageFile()
        {
            // PNG file signature (8 bytes) + simple IHDR chunk
            byte[] pngData = new byte[]
            {
                // PNG signature
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                // IHDR chunk length (13 bytes)
                0x00, 0x00, 0x00, 0x0D,
                // IHDR chunk type
                0x49, 0x48, 0x44, 0x52,
                // Width (1px)
                0x00, 0x00, 0x00, 0x01,
                // Height (1px)
                0x00, 0x00, 0x00, 0x01,
                // Bit depth (8), Color type (6=RGBA), Compression (0), Filter (0), Interlace (0)
                0x08, 0x06, 0x00, 0x00, 0x00,
                // CRC for IHDR
                0x1F, 0x15, 0xC4, 0x89
            };

            return File(pngData, "image/png", "image.png");
        }

        /// <summary>
        /// Serves a plain text file with specific filename for extraction testing.
        /// </summary>
        [HttpGet("document.txt")]
        public IActionResult GetDocumentFile()
        {
            string content = "Test document content.\nLine 2\nLine 3";
            return File(Encoding.UTF8.GetBytes(content), "text/plain", "document.txt");
        }

        /// <summary>
        /// Serves a CSV file for data format testing.
        /// </summary>
        [HttpGet("data.csv")]
        public IActionResult GetCsvFile()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Id,Name,Email,Status");
            csv.AppendLine("1,John Doe,john@example.com,Active");
            csv.AppendLine("2,Jane Smith,jane@example.com,Active");
            csv.AppendLine("3,Bob Johnson,bob@example.com,Inactive");
            csv.AppendLine("4,Alice Williams,alice@example.com,Active");

            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "data.csv");
        }

        /// <summary>
        /// Serves an XML file.
        /// </summary>
        [HttpGet("config.xml")]
        public IActionResult GetXmlFile()
        {
            string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<configuration>
    <settings>
        <setting name=""timeout"" value=""30"" />
        <setting name=""retries"" value=""3"" />
        <setting name=""enabled"" value=""true"" />
    </settings>
    <endpoints>
        <endpoint url=""http://api.example.com/v1"" />
        <endpoint url=""http://api.example.com/v2"" />
    </endpoints>
</configuration>";

            return File(Encoding.UTF8.GetBytes(xml), "application/xml", "config.xml");
        }

        /// <summary>
        /// Returns 404 for testing invalid URLs.
        /// </summary>
        [HttpGet("notfound.txt")]
        public IActionResult GetNotFoundFile()
        {
            return NotFound(new { error = "File not found" });
        }

        /// <summary>
        /// Simulates a slow download for timeout testing.
        /// </summary>
        [HttpGet("slow.txt")]
        public async Task<IActionResult> GetSlowFile()
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            string content = "This file was served slowly.";
            return File(Encoding.UTF8.GetBytes(content), "text/plain", "slow.txt");
        }

        /// <summary>
        /// Serves a file with a complex path for URL parsing testing.
        /// </summary>
        [HttpGet("nested/path/file.txt")]
        public IActionResult GetNestedFile()
        {
            string content = "This file is in a nested path.";
            return File(Encoding.UTF8.GetBytes(content), "text/plain", "file.txt");
        }

        /// <summary>
        /// Health check endpoint.
        /// </summary>
        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                server = "FluNET.TestWebServer"
            });
        }
    }
}