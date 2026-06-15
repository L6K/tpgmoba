#!/usr/bin/env bash
set -euo pipefail

ASSETS_DIR="Assets"
errors=0

# Check 1: Every file/folder (non-hidden, non-.meta) must have a corresponding .meta
while IFS= read -r -d '' entry; do
  # Skip .meta files themselves
  if [[ "$entry" == *.meta ]]; then
    continue
  fi
  # Skip hidden files/folders (starting with dot) at any path component
  basename_entry="$(basename "$entry")"
  if [[ "$basename_entry" == .* ]]; then
    continue
  fi

  meta_file="${entry}.meta"
  if [[ ! -e "$meta_file" ]]; then
    echo "MISSING meta: $meta_file"
    errors=$((errors + 1))
  fi
done < <(find "$ASSETS_DIR" -mindepth 1 \( -name ".*" -prune \) -o -print0)

# Check 2: Every .meta must have a corresponding file/folder
while IFS= read -r -d '' meta; do
  target="${meta%.meta}"
  if [[ ! -e "$target" ]]; then
    echo "ORPHAN meta: $meta"
    errors=$((errors + 1))
  fi
done < <(find "$ASSETS_DIR" -mindepth 1 -name "*.meta" -print0)

if [[ $errors -gt 0 ]]; then
  echo "Meta integrity check FAILED: $errors violation(s)"
  exit 1
else
  echo "Meta integrity check PASSED"
fi
