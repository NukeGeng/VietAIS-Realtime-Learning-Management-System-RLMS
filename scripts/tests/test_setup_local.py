"""Exercise configuration migration in isolated temporary checkouts, never the developer's files."""
import json
from pathlib import Path
import shutil
import stat
import subprocess
import tempfile
import unittest


class LocalConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        scripts = self.root / "scripts"
        scripts.mkdir()
        shutil.copyfile(Path(__file__).resolve().parents[1] / "setup-local.py", scripts / "setup-local.py")
        self.config = self.root / "backend/src/Host/LearningPlatform.Api/appsettings.Local.json"

    def run_setup(self):
        subprocess.run(["python3", str(self.root / "scripts/setup-local.py")], check=True,
                       capture_output=True, text=True)

    def test_old_layout_preserves_custom_credentials_and_connection_settings(self):
        legacy = self.root / "backend/src/Api/appsettings.Local.json"
        legacy.parent.mkdir(parents=True)
        settings = {
            "ConnectionStrings": {"Postgres": "Host=custom;Password=test-existing-credential"},
            "Media": {"ServerUrl": "wss://custom-media.test"},
            "Identity": {"BootstrapToken": "test-existing-bootstrap"},
        }
        original = json.dumps(settings, indent=2).encode() + b"\n"
        legacy.write_bytes(original)
        self.run_setup()
        self.assertEqual(original, self.config.read_bytes())
        self.assertEqual(0o600, stat.S_IMODE(self.config.stat().st_mode))
        self.assertEqual(original, legacy.read_bytes())
        self.run_setup()
        self.assertEqual(original, self.config.read_bytes())

    def test_fresh_checkout_creates_new_host_config_and_preserves_it_on_rerun(self):
        self.run_setup()
        original = self.config.read_bytes()
        environment = (self.root / ".env").read_bytes()
        self.assertIn("Postgres", json.loads(original)["ConnectionStrings"])
        self.assertEqual(0o600, stat.S_IMODE(self.config.stat().st_mode))
        self.run_setup()
        self.assertEqual(original, self.config.read_bytes())
        self.assertEqual(environment, (self.root / ".env").read_bytes())


if __name__ == "__main__":
    unittest.main()
