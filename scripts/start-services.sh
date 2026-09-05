#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
docker compose --env-file .env -f infra/compose/docker-compose.dev.yml up -d --wait
docker compose --env-file .env -f infra/compose/docker-compose.dev.yml run --rm --no-deps storage-init
