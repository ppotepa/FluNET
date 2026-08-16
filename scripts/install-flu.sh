#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_directory="$(mktemp -d "${TMPDIR:-/tmp}/flunet-flu-packages.XXXXXX")"
tool_path="${1:-}"
trap 'rm -rf "$package_directory"' EXIT

dotnet pack "$repo_root/src/FluNET.Flu/FluNET.Flu.csproj" \
  --configuration Release \
  --output "$package_directory"

install_args=(tool install FluNET.Flu
  --add-source "$package_directory"
  --version 0.3.0-preview
  --ignore-failed-sources)

if [[ -n "$tool_path" ]]; then
  mkdir -p "$tool_path"
  install_args+=(--tool-path "$tool_path")
  dotnet "${install_args[@]}"
  echo "Installed. Start a program with: $tool_path/flu run program.flu"
else
  install_args+=(--global)
  dotnet "${install_args[@]}"
  echo 'Installed. Start a program with: flu run program.flu'
fi
