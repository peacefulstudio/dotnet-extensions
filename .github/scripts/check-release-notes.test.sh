#!/usr/bin/env bash
# Copyright (c) 2026 Peaceful Studio OÜ
# SPDX-License-Identifier: Apache-2.0

set -uo pipefail

case "${1:-}" in
  -h | --help)
    echo "Usage: $0 — regression test for check-release-notes.sh; exits non-zero on any failed assertion."
    exit 0
    ;;
esac

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
subject="$script_dir/check-release-notes.sh"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

failures=0

make_root() {
  root="$work_dir/$1"
  mkdir -p "$root/release-notes"
  printf '# Release v%s\n\nCurated prose.\n' "$2" >"$root/release-notes/v$2.md"
}

check() {
  local description="$1" version="$2" expected_status="$3" needle="$4"
  output="$("$subject" "$version" "$root" 2>&1)"
  status=$?
  if [ "$status" -ne "$expected_status" ] || ! grep -qF -- "$needle" <<<"$output"; then
    echo "FAIL: $description (exit $status, output: $output)" >&2
    failures=$((failures + 1))
    return
  fi
  echo "PASS: $description"
}

make_root present 1.2.3
check "a non-empty release-notes file for the version passes" 1.2.3 0 ""

make_root missing-notes 1.2.3
rm "$root/release-notes/v1.2.3.md"
check "a missing release-notes file for the version fails naming the file" 1.2.3 1 "release-notes/v1.2.3.md is missing or empty"

make_root empty-notes 1.2.3
: >"$root/release-notes/v1.2.3.md"
check "an empty release-notes file for the version fails naming the file" 1.2.3 1 "release-notes/v1.2.3.md is missing or empty"

make_root prerelease-missing-notes 1.2.3-preview.1
rm "$root/release-notes/v1.2.3-preview.1.md"
check "a prerelease without its release-notes file fails naming the file" 1.2.3-preview.1 1 "release-notes/v1.2.3-preview.1.md is missing or empty"

make_root notes-for-another-version 1.2.2
check "release notes written for another version do not satisfy the gate" 1.2.3 1 "release-notes/v1.2.3.md is missing or empty"

if [ "$failures" -ne 0 ]; then
  echo "$failures assertion(s) failed" >&2
  exit 1
fi
