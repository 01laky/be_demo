#!/usr/bin/env bash
# Lint be_demo — same style check as CI (dotnet format).
# Usage: ./lint.sh

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "🔍 Linting be_demo (dotnet format)..."
echo ""

dotnet restore BeDemo.sln
dotnet format BeDemo.sln --verify-no-changes --no-restore

echo ""
echo "✅ be_demo lint passed"
