#!/usr/bin/env bash
# Copyright (c) 2026 Peaceful Studio OÜ
# SPDX-License-Identifier: Apache-2.0

set -uo pipefail

case "${1:-}" in
  -h | --help)
    echo "Usage: $0 — regression test for check-package-set.sh and push-packages.sh with a stub push command; exits non-zero on any failed assertion."
    exit 0
    ;;
esac

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

push_stub="$work_dir/push"
cat >"$push_stub" <<'EOF2'
#!/usr/bin/env bash
echo "$1" >>"$PUSH_LOG"
[ "$(basename "$1")" != "$FAIL_ON" ]
EOF2
chmod +x "$push_stub"

failures=0
ids=(Peaceful.Extensions.Core Peaceful.Extensions.Hosting Peaceful.Extensions.Serilog Peaceful.Extensions.Telemetry)

make_set() {
  dir="$work_dir/$1"
  mkdir -p "$dir"
  for id in "${ids[@]}"; do
    : >"$dir/$id.$2.nupkg"
    : >"$dir/$id.$2.snupkg"
  done
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

make_set complete 1.0.0
output="$("$script_dir/check-package-set.sh" "$dir" 1.0.0 2>&1)"; status=$?
check "a complete set passes" 0 "all 8 packages"

make_set missing-symbols 1.0.0
rm "$dir/Peaceful.Extensions.Hosting.1.0.0.snupkg"
output="$("$script_dir/check-package-set.sh" "$dir" 1.0.0 2>&1)"; status=$?
check "a missing snupkg fails naming it" 1 "Peaceful.Extensions.Hosting.1.0.0.snupkg"

make_set stray 1.0.0
: >"$dir/Peaceful.Extensions.Logging.1.0.0.nupkg"
output="$("$script_dir/check-package-set.sh" "$dir" 1.0.0 2>&1)"; status=$?
check "an extra package fails naming it" 1 "Peaceful.Extensions.Logging"

make_set wrong-version 1.0.0
output="$("$script_dir/check-package-set.sh" "$dir" 1.0.1 2>&1)"; status=$?
check "a set packed at another version fails" 1 "1.0.1"

make_set push-ok 1.0.0
export PUSH_LOG="$work_dir/push.log" FAIL_ON=""
: >"$PUSH_LOG"
output="$(NUGET_API_KEY=key NUGET_PUSH="$push_stub" "$script_dir/push-packages.sh" "$dir" 1.0.0 2>&1)"; status=$?
check "a complete set is pushed" 0 "pushing Peaceful.Extensions.Telemetry"
order="$(xargs -n1 basename <"$PUSH_LOG" | tr '\n' ' ')"
if [ "$order" != "Peaceful.Extensions.Core.1.0.0.nupkg Peaceful.Extensions.Hosting.1.0.0.nupkg Peaceful.Extensions.Serilog.1.0.0.nupkg Peaceful.Extensions.Telemetry.1.0.0.nupkg " ]; then
  echo "FAIL: push order was: $order" >&2
  failures=$((failures + 1))
else
  echo "PASS: Core is pushed first, one push per package"
fi

make_set push-fails 1.0.0
: >"$PUSH_LOG"
output="$(FAIL_ON=Peaceful.Extensions.Hosting.1.0.0.nupkg NUGET_API_KEY=key NUGET_PUSH="$push_stub" "$script_dir/push-packages.sh" "$dir" 1.0.0 2>&1)"; status=$?
check "a failed push fails the script naming the package" 1 "push failed for Peaceful.Extensions.Hosting"
if grep -q Serilog "$PUSH_LOG"; then
  echo "FAIL: pushing continued after a failure" >&2
  failures=$((failures + 1))
else
  echo "PASS: pushing stops at the first failure"
fi

make_set no-key 1.0.0
output="$(env -u NUGET_API_KEY NUGET_PUSH="$push_stub" "$script_dir/push-packages.sh" "$dir" 1.0.0 2>&1)"; status=$?
check "a missing API key fails before any push" 1 "NUGET_API_KEY"

make_set incomplete-push 1.0.0
rm "$dir/Peaceful.Extensions.Core.1.0.0.nupkg"
: >"$PUSH_LOG"
output="$(NUGET_API_KEY=key NUGET_PUSH="$push_stub" "$script_dir/push-packages.sh" "$dir" 1.0.0 2>&1)"; status=$?
check "an incomplete set fails before any push" 1 "Peaceful.Extensions.Core.1.0.0.nupkg"
if [ -s "$PUSH_LOG" ]; then
  echo "FAIL: pushed from an incomplete set" >&2
  failures=$((failures + 1))
fi

if [ "$failures" -ne 0 ]; then
  echo "$failures assertion(s) failed" >&2
  exit 1
fi
echo "all assertions passed"
