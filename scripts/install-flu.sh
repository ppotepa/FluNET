#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_directory="$(mktemp -d "${TMPDIR:-/tmp}/flunet-flu-packages.XXXXXX")"
tool_path="${1:-}"
trap 'rm -rf "$package_directory"' EXIT

dotnet pack "$repo_root/src/FluNET.Flu/FluNET.Flu.csproj" \
  --configuration Release \
  --output "$package_directory"

package="$(find "$package_directory" -maxdepth 1 -type f -name 'FluNET.Flu.*.nupkg' ! -name '*.symbols.nupkg' -print -quit)"
if [[ -z "$package" ]]; then
  echo 'FluNET.Flu package was not produced by dotnet pack.' >&2
  exit 1
fi
package_name="$(basename "$package")"
version="${package_name#FluNET.Flu.}"
version="${version%.nupkg}"
if [[ -z "$version" || "$version" == "$package_name" ]]; then
  echo "Cannot determine FluNET.Flu package version from '$package_name'." >&2
  exit 1
fi

install_args=(tool install FluNET.Flu
  --add-source "$package_directory"
  --version "$version"
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
