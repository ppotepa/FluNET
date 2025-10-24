# Test the FluNET Test Web Server
# This script verifies all endpoints are working correctly

$baseUrl = "http://localhost:8765/api/testfiles"

Write-Host "Testing FluNET Test Web Server..." -ForegroundColor Cyan
Write-Host "Base URL: $baseUrl" -ForegroundColor Gray
Write-Host ""

# Check if server is running
Write-Host "Checking server health..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get
    Write-Host "✓ Server is healthy" -ForegroundColor Green
    Write-Host "  Status: $($health.status)" -ForegroundColor Gray
    Write-Host "  Server: $($health.server)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "✗ Server is not running!" -ForegroundColor Red
    Write-Host "  Please start the server with: cd src/FluNET.TestWebServer; dotnet run" -ForegroundColor Yellow
    exit 1
}

# Test each endpoint
$endpoints = @(
    @{ Path = "testfile.txt"; Type = "Text File"; ContentType = "text/plain" }
    @{ Path = "data.json"; Type = "JSON Data"; ContentType = "application/json" }
    @{ Path = "largefile.json"; Type = "Large JSON"; ContentType = "application/json" }
    @{ Path = "image.png"; Type = "Binary Image"; ContentType = "image/png" }
    @{ Path = "document.txt"; Type = "Document"; ContentType = "text/plain" }
    @{ Path = "data.csv"; Type = "CSV Data"; ContentType = "text/csv" }
    @{ Path = "config.xml"; Type = "XML Config"; ContentType = "application/xml" }
    @{ Path = "slow.txt"; Type = "Slow Response"; ContentType = "text/plain" }
    @{ Path = "nested/path/file.txt"; Type = "Nested Path"; ContentType = "text/plain" }
)

Write-Host "Testing file endpoints..." -ForegroundColor Yellow
$successCount = 0
$failCount = 0

foreach ($endpoint in $endpoints) {
    $url = "$baseUrl/$($endpoint.Path)"
    try {
        $response = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-Host "✓ $($endpoint.Type) - $($endpoint.Path)" -ForegroundColor Green
            Write-Host "  Content-Type: $($response.Headers['Content-Type'])" -ForegroundColor Gray
            Write-Host "  Size: $($response.Content.Length) bytes" -ForegroundColor Gray
            $successCount++
        }
        else {
            Write-Host "✗ $($endpoint.Type) - Status: $($response.StatusCode)" -ForegroundColor Red
            $failCount++
        }
    }
    catch {
        Write-Host "✗ $($endpoint.Type) - Error: $($_.Exception.Message)" -ForegroundColor Red
        $failCount++
    }
    Write-Host ""
}

# Test 404 endpoint
Write-Host "Testing error handling..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/notfound.txt" -Method Get -ErrorAction Stop
    Write-Host "✗ 404 endpoint should return 404, but returned: $($response.StatusCode)" -ForegroundColor Red
    $failCount++
}
catch {
    if ($_.Exception.Response.StatusCode -eq 404) {
        Write-Host "✓ 404 endpoint correctly returns Not Found" -ForegroundColor Green
        $successCount++
    }
    else {
        Write-Host "✗ 404 endpoint returned unexpected error" -ForegroundColor Red
        $failCount++
    }
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Summary:" -ForegroundColor Cyan
Write-Host "  Passed: $successCount" -ForegroundColor Green
Write-Host "  Failed: $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host "========================================" -ForegroundColor Cyan

if ($failCount -eq 0) {
    Write-Host "All tests passed! Server is ready for DOWNLOAD command testing." -ForegroundColor Green
    exit 0
}
else {
    Write-Host "Some tests failed. Please check the server logs." -ForegroundColor Red
    exit 1
}
