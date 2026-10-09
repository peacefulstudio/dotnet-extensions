#!/usr/bin/env bash
# Copyright (c) 2026 Peaceful Studio OÜ
# SPDX-License-Identifier: Apache-2.0

set -uo pipefail

case "${1:-}" in
  -h | --help)
    echo "Usage: $0 — regression test for assert-versions-unpublished.sh with a stub curl; exits non-zero on any failed assertion."
    exit 0
    ;;
esac

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
subject="$script_dir/assert-versions-unpublished.sh"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

stub="$work_dir/curl"
cat >"$stub" <<'EOF2'
#!/usr/bin/env bash
out=""
url=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    -w) shift 2 ;;
    -sS) shift ;;
    *) url="$1"; shift ;;
  esac
done
id="$(basename "$(dirname "$url")")"
fixture="$FIXTURES/$id"
if [ -f "$fixture" ]; then
  cp "$fixture" "$out"
  printf '200'
elif [ -f "$FIXTURES/$id.status" ]; then
  : >"$out"
  cat "$FIXTURES/$id.status"
else
  : >"$out"
  printf '404'
fi
EOF2
chmod +x "$stub"

failures=0

run() {
  output="$(CURL="$stub" FIXTURES="$fixtures" NUGET_FLAT_BASE=https://nuget.invalid "$subject" "$@" 2>&1)"
  status=$?
}

check() {
  local description="$1" expected_status="$2" needle="$3"
  if [ "$status" -ne "$expected_status" ] || ! grep -qF -- "$needle" <<<"$output"; then
    echo "FAIL: $description (exit $status, output: $output)" >&2
    failures=$((failures + 1))
    return
  fi
  echo "PASS: $description"
}

fixtures="$work_dir/none"
mkdir -p "$fixtures"
run 1.0.0
check "packages unknown to nuget.org (404) pass" 0 "not on nuget.org"

fixtures="$work_dir/older"
mkdir -p "$fixtures"
echo '{"versions":["0.9.0","1.0.0-preview.1"]}' >"$fixtures/peaceful.extensions.core"
run 1.0.0
check "packages that only hold other versions pass" 0 "not on nuget.org"

fixtures="$work_dir/one-published"
mkdir -p "$fixtures"
echo '{"versions":["0.9.0","1.0.0"]}' >"$fixtures/peaceful.extensions.serilog"
run 1.0.0
check "a version published for one package fails naming it" 1 "Peaceful.Extensions.Serilog"

fixtures="$work_dir/case"
mkdir -p "$fixtures"
echo '{"versions":["1.0.0-preview.1"]}' >"$fixtures/peaceful.extensions.core"
run 1.0.0-Preview.1
check "version comparison ignores case" 1 "Peaceful.Extensions.Core"

fixtures="$work_dir/server-error"
mkdir -p "$fixtures"
printf '503' >"$fixtures/peaceful.extensions.hosting.status"
run 1.0.0
check "an unexpected HTTP status fails as a broken lookup" 2 "HTTP 503"

run
check "a missing argument fails with usage" 2 "Usage"

if [ "$failures" -ne 0 ]; then
  echo "$failures assertion(s) failed" >&2
  exit 1
fi
echo "all assertions passed"
