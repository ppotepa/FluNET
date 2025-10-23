param(
    [switch]$WhatIf
)

# Tests files that need the ExecutionPipelineFactory registration
$testFiles = @(
    "tests\FluNET.Tests\SayCommandTests.cs",
    "tests\FluNET.Tests\GetCommandTests.cs",
    "tests\FluNET.Tests\SaveCommandTests.cs",
    "tests\FluNET.Tests\SendCommandTests.cs",
    "tests\FluNET.Tests\TransformCommandTests.cs",
    "tests\FluNET.Tests\VariableStorageTests.cs",
    "tests\FluNET.Tests\ReferenceResolutionTests.cs",
    "tests\FluNET.Tests\ThenClauseTests.cs"
)

$linesToAdd = "            services.AddTransient<Execution.ExecutionPipelineFactory>();"

foreach ($file in $testFiles) {
    $fullPath = Join-Path $PSScriptRoot $file
    
    if (Test-Path $fullPath) {
        Write-Host "Processing: $file" -ForegroundColor Cyan
        
        $content = Get-Content $fullPath -Raw
        
        # Check if already added
        if ($content -match "ExecutionPipelineFactory") {
            Write-Host "  ✓ Already has ExecutionPipelineFactory" -ForegroundColor Green
            continue
        }
        
        # Find the line with "services.AddTransient<Engine>();" and add before it
        $pattern = "(\s+)(services\.AddTransient<Engine>\(\);)"
        $replacement = "`$1$linesToAdd`r`n`$1`$2"
        
        $newContent = $content -replace $pattern, $replacement
        
        if ($newContent -ne $content) {
            if ($WhatIf) {
                Write-Host "  Would add ExecutionPipelineFactory registration" -ForegroundColor Yellow
            }
            else {
                Set-Content -Path $fullPath -Value $newContent -NoNewline
                Write-Host "  ✓ Added ExecutionPipelineFactory registration" -ForegroundColor Green
            }
        }
        else {
            Write-Host "  ⚠ Could not find insertion point" -ForegroundColor Red
        }
    }
    else {
        Write-Host "  ⚠ File not found: $fullPath" -ForegroundColor Red
    }
}

Write-Host "`n✓ Done!" -ForegroundColor Green

if (-not $WhatIf) {
    Write-Host "`nBuilding solution..." -ForegroundColor Cyan
    dotnet build
}
