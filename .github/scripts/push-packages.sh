#!/usr/bin/env bash
# Copyright (c) 2026 Peaceful Studio OÜ
# SPDX-License-Identifier: Apache-2.0

set -euo pipefail

usage() {
  cat <<EOF2
Usage: $0 <package-dir> <version>

Push the shipped packages (see package-ids.sh) at <version> from <package-dir>
to nuget.org in dependency order. Each push carries the matching .snupkg that
sits beside the .nupkg. There is no --skip-duplicate: a package that already
exists fails the push, so a release never reports success for a version it did
not publish.

Needs NUGET_API_KEY. Reads NUGET_SOURCE (default https://api.nuget.org/v3/index.json)
and runs NUGET_PUSH (default "dotnet nuget push").
EOF2
}

case "${1:-}" in
  -h | --help)
    usage
    exit 0
    ;;
esac

if [ "$#" -ne 2 ]; then
  usage >&2
  exit 2
fi

package_dir="$1"
version="$2"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
nuget_source="${NUGET_SOURCE:-https://api.nuget.org/v3/index.json}"
push_command="${NUGET_PUSH:-dotnet nuget push}"

[ -n "${NUGET_API_KEY:-}" ] || { echo "::error::NUGET_API_KEY is unset; cannot push to nuget.org." >&2; exit 1; }

"${script_dir}/check-package-set.sh" "${package_dir}" "${version}"

while IFS= read -r package_id; do
  package="${package_dir}/${package_id}.${version}.nupkg"
  echo "pushing ${package_id} ${version}"
  ${push_command} "${package}" --source "${nuget_source}" --api-key "${NUGET_API_KEY}" ||
    { echo "::error::push failed for ${package_id} ${version}." >&2; exit 1; }
done < <("${script_dir}/package-ids.sh")
