#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

git -C "$root" config core.hooksPath .githooks
chmod +x "$root"/.githooks/* "$root"/scripts/coverage/check.sh

echo "Git hooks installed (core.hooksPath=.githooks)."
