#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
API="$ROOT/BeDemo.Api/Validation"
TESTS="$ROOT/BeDemo.Api.Tests/Validation"
missing=0
while IFS= read -r -d '' v; do
  base="$(basename "$v" .cs)"
  [[ "$base" == IFileValidator ]] && continue
  found="$(find "$TESTS" -name "${base}Tests.cs" -print -quit)"
  if [[ -z "$found" ]]; then
    echo "MISSING TEST: $base -> expected ${base}Tests.cs under $TESTS"
    missing=1
  fi
done < <(find "$API" -name '*Validator.cs' -type f -print0)
exit "$missing"
