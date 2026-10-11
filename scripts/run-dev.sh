#!/usr/bin/env bash
set -Eeuo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"
for command_name in python3 docker dotnet npm lsof; do
  command -v "$command_name" >/dev/null 2>&1 || { echo "Missing required command: $command_name" >&2; exit 1; }
done
if [[ ! -f .env || ! -f backend/src/Host/LearningPlatform.Api/appsettings.Local.json ]]; then
  python3 scripts/setup-local.py
fi
for port in 5173 7041 5164 17225; do
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    echo "Port $port is already in use. Stop the previous RLMS run before starting AppHost." >&2
    exit 1
  fi
done
# Compose remains available for CI and existing volumes. Aspire owns API, migrator and frontend.
bash scripts/start-services.sh
case "${RLMS_INFRASTRUCTURE_MODE:-Existing}" in
  Existing) ;;
  Managed)
    # Stop only this project's containers. Never delete volumes or another project's services.
    docker compose --env-file .env -f infra/compose/docker-compose.dev.yml stop postgres redis object-storage
    ;;
  *) echo "RLMS_INFRASTRUCTURE_MODE must be Existing or Managed." >&2; exit 1 ;;
esac
if [[ ! -d frontend/node_modules ]]; then (cd frontend && npm ci); fi
export Infrastructure__Mode="${RLMS_INFRASTRUCTURE_MODE:-Existing}"
exec dotnet run --project backend/src/Host/LearningPlatform.AppHost --launch-profile https
