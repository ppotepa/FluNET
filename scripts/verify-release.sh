#!/usr/bin/env bash
set -euo pipefail

ARTIFACTS="${TMPDIR:-/tmp}/flunet-release-$RANDOM-$RANDOM"
TOOL_HOME="$ARTIFACTS/tool-home"
mkdir -p "$ARTIFACTS/packages" "$TOOL_HOME"
trap 'rm -rf "$ARTIFACTS"' EXIT

dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build

dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- version
dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- contract
dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- --help

dotnet pack src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build --output "$ARTIFACTS/packages"
dotnet tool install FluNET.Tool --tool-path "$TOOL_HOME" --add-source "$ARTIFACTS/packages" --version 0.3.0-preview
"$TOOL_HOME/flunet" --help
