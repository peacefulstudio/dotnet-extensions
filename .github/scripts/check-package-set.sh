#!/usr/bin/env bash
# Copyright (c) 2026 Peaceful Studio OÜ
# SPDX-License-Identifier: Apache-2.0

set -euo pipefail

usage() {
  cat <<EOF2
Usage: $0 <package-dir> <version>

Fail unless <package-dir> holds exactly one .nupkg and one .snupkg for each
package id printed by package-ids.sh at <version>, and no other file.

Exit codes: 0 the set is complete, 1 it is not, 2 usage error.
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
[ -d "${package_dir}" ] || { echo "::error::${package_dir} is not a directory." >&2; exit 2; }

expected="$(mktemp)"
actual="$(mktemp)"
trap 'rm -f "${expected}" "${actual}"' EXIT

while IFS= read -r package_id; do
  printf '%s\n' "${package_id}.${version}.nupkg" "${package_id}.${version}.snupkg"
done < <("${script_dir}/package-ids.sh") | LC_ALL=C sort > "${expected}"
find "${package_dir}" -maxdepth 1 -type f -exec basename {} \; | LC_ALL=C sort > "${actual}"

if ! diff_output="$(diff "${expected}" "${actual}")"; then
  echo "::error::${package_dir} does not hold the shipped package set for ${version} (< expected, > found):" >&2
  printf '%s\n' "${diff_output}" >&2
  exit 1
fi
echo "${package_dir} holds all $(wc -l < "${expected}" | tr -d ' ') packages for ${version}."
