#!/usr/bin/env bash
set -euo pipefail

RESULTS_DIRECTORY="${1:?Usage: verify-coverage.sh RESULTS_DIRECTORY [BASELINE_FILE]}"
BASELINE_FILE="${2:-eng/coverage-baseline.txt}"

COVERAGE_FILE="$(find "$RESULTS_DIRECTORY" -type f -name 'coverage.cobertura.xml' -print -quit)"
if [[ -z "$COVERAGE_FILE" ]]; then
  echo "Cobertura coverage report was not produced under '$RESULTS_DIRECTORY'." >&2
  exit 1
fi

if [[ ! -f "$BASELINE_FILE" ]]; then
  echo "Coverage baseline file '$BASELINE_FILE' was not found." >&2
  exit 1
fi

ACTUAL="$(sed -n 's/.*line-rate="\([0-9.]*\)".*/\1/p' "$COVERAGE_FILE" | head -n 1)"
MINIMUM="$(tr -d '[:space:]' < "$BASELINE_FILE")"
if [[ -z "$ACTUAL" || -z "$MINIMUM" ]]; then
  echo "Could not read line coverage or baseline." >&2
  exit 1
fi

if ! awk -v actual="$ACTUAL" -v minimum="$MINIMUM" 'BEGIN { exit !(actual + 0 >= minimum + 0) }'; then
  echo "Line coverage $ACTUAL is below committed baseline $MINIMUM." >&2
  exit 1
fi

printf 'Line coverage %.2f%% meets baseline %.2f%%.\n' \
  "$(awk -v value="$ACTUAL" 'BEGIN { print value * 100 }')" \
  "$(awk -v value="$MINIMUM" 'BEGIN { print value * 100 }')"
