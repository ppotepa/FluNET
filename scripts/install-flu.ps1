param(
    [string]$ToolPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$packageId = 'FluNET.Flu'
$packageDirectory = Join-Path ([IO.Path]::GetTempPath()) "flunet-flu-packages-$([Guid]::NewGuid().ToString('N'))"

try {
    New-Item -ItemType Directory -Path $packageDirectory | Out-Null

    dotnet pack (Join-Path $repoRoot 'src/FluNET.Flu/FluNET.Flu.csproj') `
        --configuration Release `
        --output $packageDirectory
    if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }

    $package = Get-ChildItem -Path $packageDirectory -Filter 'FluNET.Flu.*.nupkg' |
        Where-Object { $_.Name -notlike '*.symbols.nupkg' } |
        Select-Object -First 1
    if ($null -eq $package) {
        throw 'FluNET.Flu package was not produced by dotnet pack.'
    }

    $prefix = 'FluNET.Flu.'
    if (-not $package.BaseName.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cannot determine FluNET.Flu package version from '$($package.Name)'."
    }
    $version = $package.BaseName.Substring($prefix.Length)
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Cannot determine FluNET.Flu package version from '$($package.Name)'."
    }

    $installArguments = @(
        'tool', 'install', $packageId,
        '--add-source', $packageDirectory,
        '--version', $version,
        '--ignore-failed-sources'
    )

    if ([string]::IsNullOrWhiteSpace($ToolPath)) {
        $installArguments += '--global'
        Write-Host 'Installing flu as a global .NET tool...'
    }
    else {
        $resolvedToolPath = [IO.Path]::GetFullPath($ToolPath)
        $installArguments += @('--tool-path', $resolvedToolPath)
        Write-Host "Installing flu into $resolvedToolPath..."
    }

    & dotnet @installArguments
    if ($LASTEXITCODE -ne 0) {
        throw 'Tool installation failed. If flu is already installed, remove it or use a different -ToolPath.'
    }

    if ([string]::IsNullOrWhiteSpace($ToolPath)) {
        Write-Host 'Installed. Start a program with: flu run program.flu'
    }
    else {
        Write-Host "Installed. Start a program with: $ToolPath/flu run program.flu"
    }
}
finally {
    if (Test-Path -LiteralPath $packageDirectory) {
        Remove-Item -LiteralPath $packageDirectory -Recurse -Force
    }
}
