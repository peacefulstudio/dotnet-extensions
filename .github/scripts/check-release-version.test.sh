#!/usr/bin/env bash
# Copyright (c) 2026 Peaceful Studio OÜ
# SPDX-License-Identifier: Apache-2.0

set -uo pipefail

case "${1:-}" in
  -h | --help)
    echo "Usage: $0 — regression test for check-release-version.sh; exits non-zero on any failed assertion."
    exit 0
    ;;
esac

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
subject="$script_dir/check-release-version.sh"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

failures=0

make_root() {
  root="$work_dir/$1"
  mkdir -p "$root"
  printf '<Project><PropertyGroup><Version>%s</Version></PropertyGroup></Project>\n' "$2" >"$root/Directory.Build.props"
  printf '# Changelog\n\n## [Unreleased]\n\n### Added\n\n## [%s] - 2026-01-01\n\n### Added\n- thing\n\n[Unreleased]: https://example.invalid\n' "$3" >"$root/CHANGELOG.md"
  mkdir -p "$root/release-notes"
  printf '# Release v%s\n\nCurated prose.\n' "$2" >"$root/release-notes/v$2.md"
}

check() {
  local description="$1" expected_status="$2" needle="$3"
  output="$("$subject" "$root" 2>&1)"
  status=$?
  if [ "$status" -ne "$expected_status" ] || ! grep -qF -- "$needle" <<<"$output"; then
    echo "FAIL: $description (exit $status, output: $output)" >&2
    failures=$((failures + 1))
    return
  fi
  echo "PASS: $description"
}

make_root matching 1.2.3 1.2.3
check "a version equal to the top CHANGELOG release heading prints the version" 0 "1.2.3"

make_root prerelease 1.2.3-preview.1 1.2.3-preview.1
check "a prerelease version is accepted" 0 "1.2.3-preview.1"

make_root heading-differs 1.2.3 1.2.2
check "a CHANGELOG that tops out at another release fails naming both" 1 "1.2.2"

make_root not-semver 1.2 1.2
check "a version that is not SemVer fails" 1 "not a release version"

make_root empty-section 1.2.3 1.2.3
printf '# Changelog\n\n## [Unreleased]\n\n## [1.2.3] - 2026-01-01\n\n## [1.2.2] - 2026-01-01\n\n### Added\n- old\n' >"$root/CHANGELOG.md"
check "an empty CHANGELOG section for the version fails" 1 "no non-empty section"

make_root no-release-heading 1.2.3 1.2.3
printf '# Changelog\n\n## [Unreleased]\n' >"$root/CHANGELOG.md"
check "a CHANGELOG without a release heading fails" 1 "tops out at []"

make_root missing-notes 1.2.3 1.2.3
rm "$root/release-notes/v1.2.3.md"
check "a missing release-notes file for the version fails naming the file" 1 "release-notes/v1.2.3.md is missing or empty"

make_root empty-notes 1.2.3 1.2.3
: >"$root/release-notes/v1.2.3.md"
check "an empty release-notes file for the version fails naming the file" 1 "release-notes/v1.2.3.md is missing or empty"

make_root prerelease-missing-notes 1.2.3-preview.1 1.2.3-preview.1
rm "$root/release-notes/v1.2.3-preview.1.md"
check "a prerelease without its release-notes file fails naming the file" 1 "release-notes/v1.2.3-preview.1.md is missing or empty"

make_root notes-for-another-version 1.2.3 1.2.3
mv "$root/release-notes/v1.2.3.md" "$root/release-notes/v1.2.2.md"
check "release notes written for another version do not satisfy the gate" 1 "release-notes/v1.2.3.md is missing or empty"

if [ "$failures" -ne 0 ]; then
  echo "$failures assertion(s) failed" >&2
  exit 1
fi
echo "all assertions passed"
