param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsDirectory,
    [string]$BaselineFile = 'eng/coverage-baseline.txt'
)

$ErrorActionPreference = 'Stop'
$CoverageFile = Get-ChildItem -Path $ResultsDirectory -Recurse -Filter 'coverage.cobertura.xml' |
    Select-Object -First 1
if ($null -eq $CoverageFile) {
    throw "Cobertura coverage report was not produced under '$ResultsDirectory'."
}
if (-not (Test-Path -LiteralPath $BaselineFile)) {
    throw "Coverage baseline file '$BaselineFile' was not found."
}

[xml]$Coverage = Get-Content -LiteralPath $CoverageFile.FullName -Raw
$Culture = [System.Globalization.CultureInfo]::InvariantCulture
$Actual = [double]::Parse([string]$Coverage.coverage.'line-rate', $Culture)
$MinimumText = (Get-Content -LiteralPath $BaselineFile -Raw).Trim()
$Minimum = [double]::Parse($MinimumText, $Culture)

if ($Actual -lt $Minimum) {
    throw "Line coverage $Actual is below committed baseline $Minimum."
}

Write-Host ('Line coverage {0:P2} meets baseline {1:P2}.' -f $Actual, $Minimum)
