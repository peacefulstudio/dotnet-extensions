#!/usr/bin/env bash
# Copyright (c) 2026 Peaceful Studio OÜ
# SPDX-License-Identifier: Apache-2.0

set -euo pipefail

usage() {
  cat <<EOF2
Usage: $0 [root]

Print the release version of the checkout at [root] (default: the current
directory) after asserting that it is releasable:

  1. <Version> in Directory.Build.props is a SemVer release version;
  2. the newest released heading of CHANGELOG.md (the first "## [x]" other than
     Unreleased) names that same version;
  3. that CHANGELOG section is not empty.

Exit codes: 0 releasable, 1 not releasable.
EOF2
}

case "${1:-}" in
  -h | --help)
    usage
    exit 0
    ;;
esac

root="${1:-.}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

refuse() {
  echo "::error::$*" >&2
  exit 1
}

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "${root}/Directory.Build.props" | head -n 1 | tr -d '[:space:]')"
[[ "${version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] ||
  refuse "'${version}' in ${root}/Directory.Build.props is not a release version."

changelog_version="$(sed -n 's:^## \[\([^]]*\)\].*:\1:p' "${root}/CHANGELOG.md" | grep -vix 'unreleased' | head -n 1 || true)"
[ "${changelog_version}" = "${version}" ] ||
  refuse "CHANGELOG.md tops out at [${changelog_version}] but <Version> is ${version}."

"${script_dir}/changelog-section.sh" "${version}" "${root}/CHANGELOG.md" > /dev/null ||
  refuse "CHANGELOG.md has no non-empty section for ${version}."

printf '%s\n' "${version}"
