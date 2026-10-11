#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "${RLMS_REBUILD_STORAGE_IMAGES:-0}" == "1" ]] ||
   ! docker image inspect rlms-minio:RELEASE.2025-04-22T22-12-26Z >/dev/null 2>&1 ||
   ! docker image inspect rlms-mc:RELEASE.2025-04-16T18-13-26Z >/dev/null 2>&1; then
  docker compose --env-file .env -f infra/compose/docker-compose.dev.yml build object-storage storage-init
fi
docker compose --env-file .env -f infra/compose/docker-compose.dev.yml up -d --wait
docker compose --env-file .env -f infra/compose/docker-compose.dev.yml run --rm --no-deps storage-init
