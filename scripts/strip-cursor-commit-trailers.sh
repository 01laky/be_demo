#!/usr/bin/env sh
# Strips Cursor / Anysphere trailers from COMMIT_EDITMSG (Cursor Agent may inject them).
# See .cursor/rules/no-cursor-commit-attribution.mdc
set -eu
file="${1:-}"
[ -n "$file" ] && [ -f "$file" ] || exit 0
_tmp="${file}.nocursor"
grep -Ev '^(Co-authored-by: Cursor|Co-authored-by: cursoragent|Signed-off-by: Cursor( Agent)?|Made with Cursor)[[:space:]]*$' "$file" > "$_tmp"
mv "$_tmp" "$file"
