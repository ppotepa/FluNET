# FluNET Test Web Server Startup Script
# Starts the test server for DOWNLOAD command testing

Write-Host "Starting FluNET Test Web Server..." -ForegroundColor Cyan
Write-Host ""

$serverPath = Join-Path $PSScriptRoot "."
$url = "http://localhost:8765"

Push-Location $serverPath

try {
    Write-Host "Server URL: $url" -ForegroundColor Yellow
    Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Available endpoints:" -ForegroundColor Yellow
    Write-Host "  Health Check:  $url/api/testfiles/health" -ForegroundColor Gray
    Write-Host "  Text File:     $url/api/testfiles/testfile.txt" -ForegroundColor Gray
    Write-Host "  JSON Data:     $url/api/testfiles/data.json" -ForegroundColor Gray
    Write-Host "  Large JSON:    $url/api/testfiles/largefile.json" -ForegroundColor Gray
    Write-Host "  Binary Image:  $url/api/testfiles/image.png" -ForegroundColor Gray
    Write-Host "  CSV Data:      $url/api/testfiles/data.csv" -ForegroundColor Gray
    Write-Host "  XML Config:    $url/api/testfiles/config.xml" -ForegroundColor Gray
    Write-Host ""
    
    dotnet run
}
finally {
    Pop-Location
}
