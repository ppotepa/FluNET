# Script to rename FluNET to FluNet consistently across the codebase

Write-Host "Step 1: Updating all namespace declarations and using statements in .cs files..." -ForegroundColor Cyan

# Get all C# files
$csFiles = Get-ChildItem -Path "src", "tests" -Filter "*.cs" -Recurse -File

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Replace namespace declarations: namespace FluNET. → namespace FluNet.
    $content = $content -replace 'namespace FluNET\.', 'namespace FluNet.'
    $content = $content -replace 'namespace FluNET;', 'namespace FluNet;'
    
    # Replace using statements: using FluNET. → using FluNet.
    $content = $content -replace 'using FluNET\.', 'using FluNet.'
    
    # Replace qualified type names: FluNET. → FluNet.
    $content = $content -replace 'FluNET\.', 'FluNet.'
    
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "  Updated: $($file.FullName)" -ForegroundColor Green
    }
}

Write-Host "`nStep 2: Updating project files..." -ForegroundColor Cyan

# Update project files RootNamespace
$csprojFiles = Get-ChildItem -Path "src", "tests" -Filter "*.csproj" -Recurse -File

foreach ($file in $csprojFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Add or update RootNamespace
    if ($content -match '<RootNamespace>FluNET</RootNamespace>') {
        $content = $content -replace '<RootNamespace>FluNET</RootNamespace>', '<RootNamespace>FluNet</RootNamespace>'
        Write-Host "  Updated RootNamespace in: $($file.FullName)" -ForegroundColor Green
    }
    elseif ($content -notmatch '<RootNamespace>') {
        # Add RootNamespace to PropertyGroup
        $content = $content -replace '(<PropertyGroup>)', '$1`n    <RootNamespace>FluNet</RootNamespace>'
        Write-Host "  Added RootNamespace to: $($file.FullName)" -ForegroundColor Green
    }
    
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Write-Host "`nNamespace updates complete!" -ForegroundColor Green
Write-Host "`nNote: Folder and file renaming should be done manually after this to avoid breaking git history." -ForegroundColor Yellow
