$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$Artifacts = Join-Path ([System.IO.Path]::GetTempPath()) ("flunet-release-" + [Guid]::NewGuid().ToString('N'))
$Packages = Join-Path $Artifacts 'packages'
$ToolHome = Join-Path $Artifacts 'tool-home'
$NuGetConfig = Join-Path $Artifacts 'NuGet.Config'
New-Item -ItemType Directory -Force -Path $Packages, $ToolHome | Out-Null
try {
    dotnet restore FluNET.sln
    dotnet build FluNET.sln --configuration Release --no-restore
    dotnet test FluNET.sln --configuration Release --no-build

    dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- version
    dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- contract
    dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- --help
    dotnet run --project src/FluNET.Flu/FluNET.Flu.csproj --configuration Release --no-build -- check samples/FluNET.Showcase/program.flu

    dotnet pack src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build --output $Packages
    $Package = Get-ChildItem -Path $Packages -Filter 'FluNET.Tool.*.nupkg' |
        Where-Object { $_.Name -notlike '*.symbols.nupkg' } |
        Select-Object -First 1
    if ($null -eq $Package) {
        throw 'FluNET.Tool package was not produced by dotnet pack.'
    }

    $Prefix = 'FluNET.Tool.'
    if (-not $Package.BaseName.StartsWith($Prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cannot determine FluNET.Tool package version from '$($Package.Name)'."
    }
    $Version = $Package.BaseName.Substring($Prefix.Length)
    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "Cannot determine FluNET.Tool package version from '$($Package.Name)'."
    }

    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$Packages" />
  </packageSources>
</configuration>
"@ | Set-Content -Path $NuGetConfig -Encoding utf8

    dotnet tool install FluNET.Tool --tool-path $ToolHome --configfile $NuGetConfig --version $Version
    $ToolCommand = if ($IsWindows) { Join-Path $ToolHome 'flunet.exe' } else { Join-Path $ToolHome 'flunet' }
    & $ToolCommand --help
}
finally {
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $Artifacts
}
