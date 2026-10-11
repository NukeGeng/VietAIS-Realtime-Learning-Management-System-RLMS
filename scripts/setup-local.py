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
        "RLMS_BOOTSTRAP_TOKEN": secrets.token_urlsafe(32),
    }
    fd = os.open(env_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w") as stream:
        stream.write("".join(f"{key}={value}\n" for key, value in values.items()))
else:
    values = dict(line.split("=", 1) for line in env_path.read_text().splitlines()
                  if line and not line.startswith("#"))
    if "RLMS_BOOTSTRAP_TOKEN" not in values:
        values["RLMS_BOOTSTRAP_TOKEN"] = secrets.token_urlsafe(32)
        env_path.write_text("".join(f"{key}={value}\n" for key, value in values.items()))
        os.chmod(env_path, 0o600)

config_path = root / "backend/src/Host/LearningPlatform.Api/appsettings.Local.json"
config_path.parent.mkdir(parents=True, exist_ok=True)
legacy_config_path = root / "backend/src/Api/appsettings.Local.json"
if not config_path.exists() and legacy_config_path.exists():
    # Preserve custom connection/media/identity settings on checkouts upgraded from the old layout.
    fd = os.open(config_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "wb") as stream:
        stream.write(legacy_config_path.read_bytes())
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
        "Identity": {"BootstrapToken": values["RLMS_BOOTSTRAP_TOKEN"]},
    }
    fd = os.open(config_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w") as stream:
        json.dump(config, stream, indent=2)
        stream.write("\n")
else:
    config = json.loads(config_path.read_text())
    identity = config.setdefault("Identity", {})
    if not identity.get("BootstrapToken"):
        identity["BootstrapToken"] = values["RLMS_BOOTSTRAP_TOKEN"]
        config_path.write_text(json.dumps(config, indent=2) + "\n")
        os.chmod(config_path, 0o600)
print("Local configuration is ready. Existing files were preserved.")
