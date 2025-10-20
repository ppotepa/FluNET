# FluNET Test Web Server

A lightweight ASP.NET Core Web API server for testing the FluNET DOWNLOAD command functionality.

## Purpose

This test server provides various file types and scenarios to validate:
- Text file downloads
- JSON data downloads
- Binary file downloads (images)
- CSV and XML format downloads
- Filename extraction from URLs
- Error handling (404s)
- Performance testing (large files, slow responses)
- Nested path handling

## Running the Server

### From Command Line
```powershell
cd src/FluNET.TestWebServer
dotnet run
```

The server will start on `http://localhost:8765`

### From Tests
The server is automatically started and stopped by test fixtures.

## Available Endpoints

### File Downloads
- `GET /api/testfiles/testfile.txt` - Simple text file
- `GET /api/testfiles/data.json` - JSON data file
- `GET /api/testfiles/largefile.json` - Large JSON file (1000 items)
- `GET /api/testfiles/image.png` - Binary PNG image
- `GET /api/testfiles/document.txt` - Plain text document
- `GET /api/testfiles/data.csv` - CSV data file
- `GET /api/testfiles/config.xml` - XML configuration file

### Test Scenarios
- `GET /api/testfiles/notfound.txt` - Returns 404 for error testing
- `GET /api/testfiles/slow.txt` - Delayed response for timeout testing
- `GET /api/testfiles/nested/path/file.txt` - Nested path for URL parsing

### Monitoring
- `GET /api/testfiles/health` - Health check endpoint

## Example Usage in Tests

```csharp
// Download a text file
ProcessedPrompt prompt = new("DOWNLOAD [file] FROM {http://localhost:8765/api/testfiles/testfile.txt} TO {output.txt} .");
var result = engine.Run(prompt);

// Download JSON data
ProcessedPrompt prompt = new("DOWNLOAD [data] FROM {http://localhost:8765/api/testfiles/data.json} .");
var result = engine.Run(prompt);

// Download with synonym
ProcessedPrompt prompt = new("PULL [image] FROM {http://localhost:8765/api/testfiles/image.png} TO {image.png} .");
var result = engine.Run(prompt);
```

## Configuration

The server is configured to:
- Listen on `localhost:8765`
- Serve files without HTTPS (for testing simplicity)
- Use ASP.NET Core controllers
- Provide proper MIME types for all file types

## Development Notes

This server is intended for **testing purposes only**. It:
- Uses HTTP instead of HTTPS
- Has no authentication or authorization
- Serves test data without validation
- Should not be used in production environments
