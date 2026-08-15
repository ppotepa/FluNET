$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$Artifacts = Join-Path ([System.IO.Path]::GetTempPath()) ("flunet-release-" + [Guid]::NewGuid().ToString('N'))
$Packages = Join-Path $Artifacts 'packages'
$ToolHome = Join-Path $Artifacts 'tool-home'
New-Item -ItemType Directory -Force -Path $Packages, $ToolHome | Out-Null
try {
    dotnet restore FluNET.sln
    dotnet build FluNET.sln --configuration Release --no-restore
    dotnet test FluNET.sln --configuration Release --no-build

    dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- version
    dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- contract
    dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- --help

    dotnet pack src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build --output $Packages
    dotnet tool install FluNET.Tool --tool-path $ToolHome --add-source $Packages --version 0.3.0-preview
    & (Join-Path $ToolHome 'flunet') --help
}
finally {
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $Artifacts
}
