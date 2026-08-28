#!/usr/bin/env bash
set -euo pipefail

ARTIFACTS="${TMPDIR:-/tmp}/flunet-release-$RANDOM-$RANDOM"
PACKAGES="$ARTIFACTS/packages"
TOOL_HOME="$ARTIFACTS/tool-home"
NUGET_CONFIG="$ARTIFACTS/NuGet.Config"
mkdir -p "$PACKAGES" "$TOOL_HOME"
trap 'rm -rf "$ARTIFACTS"' EXIT

dotnet restore FluNET.sln
dotnet format FluNET.sln whitespace --verify-no-changes --no-restore
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build

dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- version
dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- contract
dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- --help
dotnet run --project src/FluNET.Flu/FluNET.Flu.csproj --configuration Release --no-build -- check samples/FluNET.Showcase/program.flu

dotnet pack src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build --output "$PACKAGES"
PACKAGE="$(find "$PACKAGES" -maxdepth 1 -type f -name 'FluNET.Tool.*.nupkg' ! -name '*.symbols.nupkg' -print -quit)"
if [[ -z "$PACKAGE" ]]; then
  echo "FluNET.Tool package was not produced by dotnet pack." >&2
  exit 1
fi
PACKAGE_NAME="$(basename "$PACKAGE")"
VERSION="${PACKAGE_NAME#FluNET.Tool.}"
VERSION="${VERSION%.nupkg}"
if [[ -z "$VERSION" || "$VERSION" == "$PACKAGE_NAME" ]]; then
  echo "Cannot determine FluNET.Tool package version from '$PACKAGE_NAME'." >&2
  exit 1
fi

cat > "$NUGET_CONFIG" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$PACKAGES" />
  </packageSources>
</configuration>
EOF

dotnet tool install FluNET.Tool --tool-path "$TOOL_HOME" --configfile "$NUGET_CONFIG" --version "$VERSION"
"$TOOL_HOME/flunet" --help
