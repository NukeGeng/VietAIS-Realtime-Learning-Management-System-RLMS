#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore backend/LearningPlatform.slnx --locked-mode
dotnet build backend/LearningPlatform.slnx --no-restore
