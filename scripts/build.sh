#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore backend/LearningPlatform.sln --locked-mode
dotnet build backend/LearningPlatform.sln --no-restore
