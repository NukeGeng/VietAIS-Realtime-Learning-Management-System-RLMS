#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

# Increase the workload without changing the test code, for example:
#   RLMS_LOAD_STUDENTS=32 bash scripts/load-test.sh
dotnet test backend/tests/IntegrationTests/LearningPlatform.IntegrationTests.csproj \
  --no-restore --filter 'Category=Load'
