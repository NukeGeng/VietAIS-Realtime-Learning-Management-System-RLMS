#!/usr/bin/env python3
"""Create ignored local configuration. Preserve existing credentials on rerun."""
import json
import os
from pathlib import Path
import secrets

root = Path(__file__).resolve().parent.parent
env_path = root / ".env"
if not env_path.exists():
    values = {
        "RLMS_POSTGRES_PASSWORD": secrets.token_hex(24),
        "RLMS_STORAGE_ACCESS_KEY": secrets.token_hex(12),
        "RLMS_STORAGE_SECRET_KEY": secrets.token_hex(24),
    }
    fd = os.open(env_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w") as stream:
        stream.write("".join(f"{key}={value}\n" for key, value in values.items()))
else:
    values = dict(line.split("=", 1) for line in env_path.read_text().splitlines()
                  if line and not line.startswith("#"))

config_path = root / "backend/src/Api/appsettings.Local.json"
if not config_path.exists():
    config = {
        "ConnectionStrings": {
            "Postgres": "Host=localhost;Port=55432;Database=rlms;Username=rlms;Password="
                        + values["RLMS_POSTGRES_PASSWORD"],
            "Redis": "localhost:56379",
        },
        "ObjectStorage": {
            "Endpoint": "http://localhost:59000",
            "Region": "us-east-1",
            "Bucket": "rlms-files",
            "AccessKey": values["RLMS_STORAGE_ACCESS_KEY"],
            "SecretKey": values["RLMS_STORAGE_SECRET_KEY"],
        },
        "Media": {"ServerUrl": "ws://localhost:57880"},
    }
    fd = os.open(config_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w") as stream:
        json.dump(config, stream, indent=2)
        stream.write("\n")
print("Local configuration is ready. Existing files were preserved.")
