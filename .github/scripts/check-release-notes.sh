#!/usr/bin/env bash
# Copyright (c) 2026 Peaceful Studio OÜ
# SPDX-License-Identifier: Apache-2.0

set -euo pipefail

usage() {
  cat <<EOF2
Usage: $0 <version> [root]

Assert that release-notes/v<version>.md exists and is not empty in the
checkout at [root] (default: the current directory). That file is published
as the GitHub release body, so a release without it is refused.

Exit codes: 0 notes present, 1 notes missing or empty, 2 usage error.
EOF2
}

case "${1:-}" in
  -h | --help)
    usage
    exit 0
    ;;
  "")
    usage >&2
    exit 2
    ;;
esac

version="$1"
root="${2:-.}"

[ -s "${root}/release-notes/v${version}.md" ] || {
  echo "::error::release-notes/v${version}.md is missing or empty; the release body is the curated notes file." >&2
  exit 1
}
