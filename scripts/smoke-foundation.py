#!/usr/bin/env python3
"""Check the running local API, optionally with its peripheral services stopped."""
import argparse
from pathlib import Path
import subprocess
import urllib.error
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument("--url", default="http://localhost:5164")
parser.add_argument("--isolate-dependencies", action="store_true")
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
compose = ["docker", "compose", "--env-file", str(root / ".env"),
           "-f", str(root / "infra/compose/docker-compose.dev.yml")]


def check(path, expected):
    try:
        with urllib.request.urlopen(args.url + path, timeout=10) as response:
            status = response.status
    except urllib.error.HTTPError as error:
        status = error.code
    assert status == expected, f"{path}: expected {expected}, got {status}"
    print(f"PASS {path}: {status}")


check("/health/live", 200)
check("/health/ready", 200)
check("/api/identity/me", 401)
check("/health/dependencies", 401)
if args.isolate_dependencies:
    try:
        subprocess.run(compose + ["stop", "redis", "object-storage"], check=True)
        check("/health/live", 200)
        check("/health/ready", 200)
    finally:
        subprocess.run(compose + ["start", "redis", "object-storage"], check=True)
    print("PASS core health remains available during peripheral outage; services restarted")
