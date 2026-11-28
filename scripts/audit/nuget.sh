#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
report="$(mktemp)"
trap 'rm -f "$report"' EXIT

cd "$root/backend"
dotnet restore GymRatService.sln
if ! dotnet list GymRatService.sln package --vulnerable --include-transitive --format json > "$report"; then
  cat "$report" >&2
  exit 1
fi

cd "$root"
node scripts/audit/check-nuget.mjs "$(command -v cygpath > /dev/null && cygpath -m "$report" || echo "$report")"
