# PowerShell script to add missing using statements to test files

$testFiles = @(
    "tests\FluNET.Tests\SyntaxTests.cs",
    "tests\FluNET.Tests\ThenClauseTests.cs",
    "tests\FluNET.Tests\SyntacticEdgeCasesTests.cs",
    "tests\FluNET.Tests\ExecutionTests.cs",
    "tests\FluNET.Tests\GenericCommandTests.cs",
    "tests\FluNET.Tests\GetCommandTests.cs",
    "tests\FluNET.Tests\DownloadCommandTests.cs",
    "tests\FluNET.Tests\DownloadIntegrationTests.cs"
)

# Define the using statements that are commonly needed
$requiredUsings = @(
    "using FluNET.Sentences;",
    "using FluNET.Syntax.Validation;",
    "using FluNET.Words;",
    "using FluNET.Syntax.Verbs;"
)

foreach ($file in $testFiles) {
    $filePath = Join-Path $PSScriptRoot $file
    
    if (Test-Path $filePath) {
        Write-Host "Processing: $file" -ForegroundColor Cyan
        
        # Read the file content
        $content = Get-Content $filePath -Raw
        
        # Track what we added
        $added = @()
        
        # Check each required using and add if missing
        foreach ($using in $requiredUsings) {
            if ($content -notmatch [regex]::Escape($using)) {
                # Find the position after "using FluNET.Context;"
                $contextUsingPattern = "using FluNET\.Context;"
                if ($content -match $contextUsingPattern) {
                    # Add after FluNET.Context using
                    $content = $content -replace "($contextUsingPattern)", "`$1`r`n$using"
                    $added += $using
                }
            }
        }
        
        if ($added.Count -gt 0) {
            # Write the updated content back
            $content | Set-Content $filePath -NoNewline
            Write-Host "  Added: $($added -join ', ')" -ForegroundColor Green
        }
        else {
            Write-Host "  No changes needed" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "File not found: $filePath" -ForegroundColor Red
    }
}

Write-Host "`nDone! Building to verify..." -ForegroundColor Yellow
dotnet build
