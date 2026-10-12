#!/usr/bin/env bash
# Copyright (c) 2026 Peaceful Studio OÜ
# SPDX-License-Identifier: Apache-2.0

set -euo pipefail

case "${1:-}" in
  -h | --help)
    cat <<EOF2
Usage: $0

Print the NuGet package ids this repository ships in lockstep, one per line,
dependency-first: Peaceful.Extensions.Core leads because the other three
reference it, so a push in this order never publishes a package whose
dependency is missing from nuget.org.
EOF2
    exit 0
    ;;
esac

printf '%s\n' \
  Peaceful.Extensions.Core \
  Peaceful.Extensions.Hosting \
  Peaceful.Extensions.Serilog \
  Peaceful.Extensions.Telemetry
