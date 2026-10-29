#!/usr/bin/env bash
set -euo pipefail

target="${1:-all}"
threshold="${COVERAGE_THRESHOLD:-90}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

check_backend() {
  local results="$root/coverage-results/backend"
  rm -rf "$results"
  dotnet test "$root/backend/GymRatService.sln" \
    --settings "$root/backend/coverage.runsettings" \
    --collect:"XPlat Code Coverage" \
    --results-directory "$results"
  node "$root/scripts/coverage/check-backend.mjs" "$results" "$threshold"
}

check_frontend() {
  (cd "$root/frontend" && npm run test:coverage)
}

case "$target" in
  backend) check_backend ;;
  frontend) check_frontend ;;
  all) check_backend && check_frontend ;;
  *) echo "Usage: check.sh [backend|frontend|all]" >&2; exit 2 ;;
esac

echo "Coverage check passed (threshold ${threshold}%)."
