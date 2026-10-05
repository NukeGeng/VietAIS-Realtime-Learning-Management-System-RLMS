#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

for command_name in python3 docker dotnet npm curl lsof pgrep; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Missing required command: $command_name" >&2
    exit 1
  fi
done

if ! docker compose version >/dev/null 2>&1; then
  echo "Docker Compose is required to run RLMS infrastructure." >&2
  exit 1
fi

if [[ ! -f "$ROOT_DIR/.env" || ! -f "$ROOT_DIR/backend/src/Api/appsettings.Local.json" ]]; then
  python3 scripts/setup-local.py
fi

bash scripts/start-services.sh

if [[ ! -d "$ROOT_DIR/frontend/node_modules" ]]; then
  echo "Installing frontend dependencies..."
  (cd frontend && npm install)
fi

BACKEND_PID=""
FRONTEND_PID=""

stop_process_tree() {
  local process_id="${1:-}"
  if [[ -z "$process_id" ]] || ! kill -0 "$process_id" 2>/dev/null; then
    return
  fi

  local child_id
  while read -r child_id; do
    stop_process_tree "$child_id"
  done < <(pgrep -P "$process_id" || true)

  kill -TERM "$process_id" 2>/dev/null || true
}

assert_port_available() {
  local port="$1"
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    echo "Port $port is already in use. Stop its process before running RLMS." >&2
    lsof -nP -iTCP:"$port" -sTCP:LISTEN >&2 || true
    return 1
  fi
}

cleanup() {
  local exit_code=$?
  trap - EXIT INT TERM
  stop_process_tree "$FRONTEND_PID"
  stop_process_tree "$BACKEND_PID"
  wait "$FRONTEND_PID" 2>/dev/null || true
  wait "$BACKEND_PID" 2>/dev/null || true
  exit "$exit_code"
}

trap cleanup EXIT INT TERM

assert_port_available 5173
assert_port_available 7041
assert_port_available 5164

echo "Starting RLMS API..."
dotnet run --project backend/src/Api --launch-profile https &
BACKEND_PID=$!

backend_ready=false
for _ in {1..60}; do
  if curl -ksf https://localhost:7041/health/live >/dev/null 2>&1; then
    backend_ready=true
    break
  fi

  if ! kill -0 "$BACKEND_PID" 2>/dev/null; then
    wait "$BACKEND_PID" || true
    echo "Backend stopped before becoming healthy." >&2
    exit 1
  fi
  sleep 1
done

if [[ "$backend_ready" != true ]]; then
  echo "Backend did not become healthy within 60 seconds." >&2
  exit 1
fi

echo "Starting RLMS frontend..."
(
  cd frontend
  npm run dev -- --host 127.0.0.1 --strictPort
) &
FRONTEND_PID=$!

echo "RLMS is running at http://127.0.0.1:5173"
echo "API: https://localhost:7041"
echo "Press Ctrl+C to stop backend and frontend."

while true; do
  if ! kill -0 "$BACKEND_PID" 2>/dev/null; then
    wait "$BACKEND_PID" || true
    echo "Backend stopped; shutting down frontend." >&2
    exit 1
  fi

  if ! kill -0 "$FRONTEND_PID" 2>/dev/null; then
    wait "$FRONTEND_PID" || true
    echo "Frontend stopped; shutting down backend." >&2
    exit 1
  fi
  sleep 1
done
