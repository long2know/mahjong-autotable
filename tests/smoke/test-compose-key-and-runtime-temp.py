#!/usr/bin/env python3
"""Config-only Compose oracle and Dockerfile source contracts; no containers."""
import base64
import hashlib
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import unittest
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUTPUT = pathlib.Path(os.environ.get(
    "PACKAGING_REVISION_TEST_DIR",
    str(ROOT / ".work/compose-key-temp-tests"))).resolve()
PUBLIC_GENERATED_KEY = base64.b64encode(b"public-test-material-not-a-secret".ljust(48, b"!")).decode()
ABSENT = "__PUBLIC_TEST_ABSENT__"
BOOTSTRAP = pathlib.Path(os.environ.get(
    "COMPOSE_BOOTSTRAP_UNDER_TEST", str(ROOT / "scripts/compose-bootstrap.sh"))).resolve()
DOCKERFILE = pathlib.Path(os.environ.get(
    "RUNTIME_DOCKERFILE_UNDER_TEST", str(ROOT / "Dockerfile"))).resolve()

CONFIGURED = [
    ("hash-prefix", "JWT_SIGNING_KEY=#public-material\n", "#public-material"),
    ("hash-only", "JWT_SIGNING_KEY=#\n", "#"),
    ("hash-leading-space", "JWT_SIGNING_KEY= #public-material\n", "#public-material"),
    ("hash-leading-tab", "JWT_SIGNING_KEY=\t#public-material\n", "#public-material"),
    ("hash-adjacent", "JWT_SIGNING_KEY=public#material\n", "public#material"),
    ("hash-spaced-comment", "JWT_SIGNING_KEY=public # comment\n", "public"),
    ("hash-tab", "JWT_SIGNING_KEY=public\t# comment\n", None),
    ("hash-prefix-with-comment", "JWT_SIGNING_KEY=#public # comment\n", "#public"),
    ("double-quoted-hash", 'JWT_SIGNING_KEY="#public # material"\n', "#public # material"),
    ("single-quoted-hash", "JWT_SIGNING_KEY='#public # material'\n", "#public # material"),
    ("double-quoted-space", 'JWT_SIGNING_KEY=" "\n', " "),
    ("single-quoted-space", "JWT_SIGNING_KEY='   '\n", "   "),
    ("exported-hash", "export JWT_SIGNING_KEY=#public-material\n", "#public-material"),
    ("exported-quoted", ' \texport JWT_SIGNING_KEY = "public # value" # note\n', "public # value"),
    ("unquoted", "JWT_SIGNING_KEY=public-material\n", "public-material"),
    ("single-quoted", "JWT_SIGNING_KEY='public-material'\n", "public-material"),
    ("double-quoted", 'JWT_SIGNING_KEY="public-material"\n', "public-material"),
    ("escaped-double-quote", 'JWT_SIGNING_KEY="public\\"value"\n', 'public"value'),
    ("escaped-single-quote", "JWT_SIGNING_KEY='public\\'value'\n", "public'value"),
    ("double-quoted-escape", 'JWT_SIGNING_KEY="public\\nvalue"\n', "public\nvalue"),
    # Compare oracle output before/after without coupling to dollar escaping
    # in a particular Compose config serializer.
    ("single-quoted-literal", "JWT_SIGNING_KEY='${PUBLIC_OTHER}'\n", None),
    ("interpolated-value", "PUBLIC_OTHER=public-material\nJWT_SIGNING_KEY=${PUBLIC_OTHER}\n", "public-material"),
    ("last-hash", "JWT_SIGNING_KEY=''\nJWT_SIGNING_KEY=#public-material\n", "#public-material"),
    ("last-quoted", 'JWT_SIGNING_KEY=old-public\nexport JWT_SIGNING_KEY="new-public"\n', "new-public"),
    ("colon", "JWT_SIGNING_KEY: #public-material\n", "#public-material"),
    ("last-colon", "JWT_SIGNING_KEY=\nJWT_SIGNING_KEY: public-material\n", "public-material"),
    ("crlf", 'JWT_SIGNING_KEY="#public-material" # note\r\n', "#public-material"),
    ("no-final-newline", "JWT_SIGNING_KEY=#public-material", "#public-material"),
]

EMPTY = [
    ("absent-definition", "PUBLIC_OTHER=keep\n", ABSENT),
    ("empty", "JWT_SIGNING_KEY=\n", ""),
    ("whitespace", "JWT_SIGNING_KEY= \t \n", ""),
    ("double-empty", 'JWT_SIGNING_KEY=""\n', ""),
    ("single-empty", "JWT_SIGNING_KEY=''\n", ""),
    ("double-empty-comment", 'JWT_SIGNING_KEY= "" # unset\n', ""),
    ("single-empty-comment", "JWT_SIGNING_KEY= '' # unset\n", ""),
    ("double-empty-adjacent-comment", 'JWT_SIGNING_KEY=""# unset\n', ""),
    ("single-empty-adjacent-comment", "JWT_SIGNING_KEY=''# unset\n", ""),
    ("last-empty", "JWT_SIGNING_KEY=#old-public\nexport JWT_SIGNING_KEY='' # unset\n", ""),
    ("colon-empty", "JWT_SIGNING_KEY: \n", ""),
    ("crlf-empty", 'JWT_SIGNING_KEY="" # unset\r\n', ""),
]


class ComposeKeyPreservation(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.work = OUTPUT / uuid.uuid4().hex
        cls.work.mkdir(parents=True)
        cls.records = []
        cls.compose_calls = 0
        cls.docker = shutil.which("docker")
        cls.bash = shutil.which("bash")
        if not cls.docker or not cls.bash:
            raise RuntimeError("Existing Docker Compose and Bash are required")
        cls.home = cls.work / "isolated home"
        cls.home.mkdir()
        cls.tools = cls.work / "public entropy stub"
        cls.tools.mkdir()
        cls.compose_file = cls.work / "public-compose.yaml"
        cls.compose_file.write_text(
            "services:\n  oracle:\n    image: public-test:never-run\n"
            "    environment:\n"
            f'      OBSERVED_KEY: "${{JWT_SIGNING_KEY-{ABSENT}}}"\n')
        openssl = cls.tools / "openssl"
        openssl.write_text(
            f"#!{sys.executable}\n"
            "import os, pathlib, sys\n"
            "assert sys.argv[1:] == ['rand', '-base64', '48']\n"
            "with pathlib.Path(os.environ['PUBLIC_ENTROPY_CALLS']).open('a') as out:\n"
            "    out.write('generate-public-fixture\\n')\n"
            f"print({PUBLIC_GENERATED_KEY!r})\n")
        openssl.chmod(0o755)
        cls.env = {
            "PATH": str(cls.tools) + os.pathsep + os.environ.get("PATH", os.defpath),
            "HOME": str(cls.home),
            "DOCKER_CONFIG": str(cls.home),
            "DOCKER_HOST": "tcp://127.0.0.1:1",
            "COMPOSE_DISABLE_ENV_FILE": "1",
            "TMPDIR": str(cls.work),
        }
        version = subprocess.run([cls.docker, "compose", "version", "--short"],
                                 env=cls.env, cwd=cls.work, capture_output=True,
                                 text=True, timeout=30)
        if version.returncode:
            raise RuntimeError("Existing Compose config-only tool is unavailable")
        cls.version = version.stdout.strip()
        cls.script = BOOTSTRAP
        cls.script_sha256 = hashlib.sha256(cls.script.read_bytes()).hexdigest()

    @classmethod
    def tearDownClass(cls):
        (cls.work / "compose-oracle.json").write_text(json.dumps({
            "compose_version": cls.version,
            "bootstrap_sha256": cls.script_sha256,
            "compose_config_invocations": cls.compose_calls,
            "dockerfile_sha256": hashlib.sha256(DOCKERFILE.read_bytes()).hexdigest(),
            "scope": "Actual docker compose config only; public fixtures/entropy only. No daemon/container/build execution.",
            "cases": cls.records,
        }, indent=2) + "\n")

    def fixture(self, label, contents):
        directory = self.work / (label + "-" + uuid.uuid4().hex)
        directory.mkdir()
        file = directory / "public operator settings.env"
        file.write_bytes(contents.encode())
        return directory, file, directory / "entropy-calls.txt"

    def oracle(self, file):
        type(self).compose_calls += 1
        result = subprocess.run([
            self.docker, "compose", "--project-directory", str(file.parent),
            "-f", str(self.compose_file), "--env-file", str(file),
            "-p", "public-key-oracle", "config", "--format", "json",
        ], env=self.env, cwd=file.parent, capture_output=True, text=True, timeout=30)
        value = None
        if result.returncode == 0:
            value = json.loads(result.stdout)["services"]["oracle"]["environment"]["OBSERVED_KEY"]
        return {"exit": result.returncode, "public_value": value,
                "stderr": result.stderr}

    def bootstrap(self, file, calls):
        env = dict(self.env, PUBLIC_ENTROPY_CALLS=str(calls))
        result = subprocess.run([
            self.bash, str(self.script), "--env-file", file.name,
        ], env=env, cwd=file.parent, capture_output=True, text=True, timeout=30)
        return {"exit": result.returncode, "stdout": result.stdout,
                "stderr": result.stderr,
                "entropy_calls": len(calls.read_text().splitlines()) if calls.exists() else 0}

    def test_configured_values_match_compose_and_never_regenerate(self):
        for label, contents, expected in CONFIGURED:
            with self.subTest(case=label):
                _, file, calls = self.fixture(label, contents)
                before = file.read_bytes()
                record = {"case": label, "fixture": str(file),
                          "before_sha256": hashlib.sha256(before).hexdigest(),
                          "oracle_before": self.oracle(file)}
                self.records.append(record)
                self.assertEqual(record["oracle_before"]["exit"], 0)
                parsed = record["oracle_before"]["public_value"]
                self.assertNotIn(parsed, ("", ABSENT))
                if expected is not None:
                    self.assertEqual(parsed, expected)
                record["first"] = self.bootstrap(file, calls)
                record["after_sha256"] = hashlib.sha256(file.read_bytes()).hexdigest()
                record["oracle_after"] = self.oracle(file)
                self.assertEqual(record["first"]["exit"], 0)
                self.assertEqual(file.read_bytes(), before, "Bootstrap changed configured key bytes")
                self.assertEqual(record["first"]["entropy_calls"], 0, "A configured key was regenerated")
                self.assertEqual(record["oracle_after"]["public_value"], parsed)
                record["second"] = self.bootstrap(file, calls)
                self.assertEqual(record["second"]["exit"], 0)
                self.assertEqual(record["second"]["entropy_calls"], 0)
                self.assertEqual(file.read_bytes(), before, "Repeated bootstrap changed configured bytes")
                self.assertNotIn("JWT_SIGNING_KEY=", record["first"]["stdout"] + record["first"]["stderr"])

    def test_only_absent_or_genuinely_empty_values_generate_once(self):
        for label, contents, expected in EMPTY:
            with self.subTest(case=label):
                _, file, calls = self.fixture(label, contents)
                before = file.read_bytes()
                record = {"case": label, "fixture": str(file),
                          "oracle_before": self.oracle(file)}
                self.records.append(record)
                self.assertEqual(record["oracle_before"]["exit"], 0)
                self.assertEqual(record["oracle_before"]["public_value"], expected)
                record["first"] = self.bootstrap(file, calls)
                record["oracle_after"] = self.oracle(file)
                self.assertEqual(record["first"]["exit"], 0)
                self.assertEqual(record["first"]["entropy_calls"], 1)
                self.assertTrue(file.read_bytes().startswith(before))
                self.assertEqual(record["oracle_after"]["exit"], 0)
                self.assertEqual(record["oracle_after"]["public_value"], PUBLIC_GENERATED_KEY)
                generated = file.read_bytes()
                record["second"] = self.bootstrap(file, calls)
                self.assertEqual(record["second"]["exit"], 0)
                self.assertEqual(record["second"]["entropy_calls"], 1)
                self.assertEqual(file.read_bytes(), generated)
                self.assertNotIn(PUBLIC_GENERATED_KEY,
                                 record["first"]["stdout"] + record["first"]["stderr"])

    def test_missing_public_fixture_is_seeded_and_is_idempotent(self):
        _, file, calls = self.fixture("missing", "")
        file.unlink()
        first = self.bootstrap(file, calls)
        self.assertEqual(first["exit"], 0)
        self.assertEqual(first["entropy_calls"], 1)
        self.assertEqual(self.oracle(file)["public_value"], PUBLIC_GENERATED_KEY)
        before = file.read_bytes()
        second = self.bootstrap(file, calls)
        self.assertEqual(second["exit"], 0)
        self.assertEqual(second["entropy_calls"], 1)
        self.assertEqual(file.read_bytes(), before)

    def test_env_content_is_literal_not_shell_code(self):
        directory, file, calls = self.fixture("literal", "")
        marker = directory / "must-not-be-created"
        contents = ("JWT_SIGNING_KEY='#public-material'\n"
                    f"UNRELATED=$(printf not-executed > '{marker}')\n")
        file.write_text(contents)
        before = file.read_bytes()
        self.assertEqual(self.oracle(file)["public_value"], "#public-material")
        result = self.bootstrap(file, calls)
        self.assertEqual(result["exit"], 0)
        self.assertEqual(result["entropy_calls"], 0)
        self.assertEqual(file.read_bytes(), before)
        self.assertFalse(marker.exists(), "Bootstrap executed env-file contents")

    def test_nonempty_interpolation_is_not_replaced_when_compose_resolves_empty(self):
        _, file, calls = self.fixture(
            "deferred-interpolation",
            "PUBLIC_EMPTY=\nJWT_SIGNING_KEY=${PUBLIC_EMPTY}\n")
        before = file.read_bytes()
        self.assertEqual(self.oracle(file)["public_value"], "")
        result = self.bootstrap(file, calls)
        self.assertEqual(result["exit"], 0)
        self.assertEqual(result["entropy_calls"], 0)
        self.assertEqual(file.read_bytes(), before)

    def test_unsupported_quotes_and_bare_key_fail_closed_without_rotation(self):
        cases = [
            ("unterminated", 'JWT_SIGNING_KEY="public-material\n'),
            ("multiline-key", "JWT_SIGNING_KEY='public\nJWT_SIGNING_KEY=\nmaterial'\n"),
            ("multiline-other", "JWT_SIGNING_KEY=#public-material\nOTHER='public\nJWT_SIGNING_KEY=\nmaterial'\n"),
            ("bare-key", "JWT_SIGNING_KEY=#public-material\nJWT_SIGNING_KEY\n"),
            ("quoted-suffix", 'JWT_SIGNING_KEY=""unsupported\n'),
        ]
        for label, contents in cases:
            with self.subTest(case=label):
                _, file, calls = self.fixture(label, contents)
                before = file.read_bytes()
                record = {"case": label, "fixture": str(file),
                          "oracle_before": self.oracle(file)}
                self.records.append(record)
                record["bootstrap"] = self.bootstrap(file, calls)
                self.assertNotEqual(record["bootstrap"]["exit"], 0)
                self.assertEqual(record["bootstrap"]["entropy_calls"], 0)
                self.assertEqual(file.read_bytes(), before)
                self.assertNotIn("public-material", record["bootstrap"]["stderr"])


class RuntimeTempSourceContracts(unittest.TestCase):
    def test_runtime_temp_is_the_existing_data_volume_root(self):
        runtime = DOCKERFILE.read_text().split(
            "FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime\n", 1)[1]
        temp = re.search(r"(?m)^\s*TMPDIR=(\S+)", runtime).group(1)
        volume = json.loads(re.search(r"(?m)^VOLUME (.+)$", runtime).group(1))
        self.assertEqual(temp, "/data", "An inherited volume need not contain a new work/ subdirectory")
        self.assertIn(temp, volume)
        self.assertNotIn("mkdir -p /data/work", runtime)

    def test_nonroot_sqlite_tini_and_build_tmpfs_contracts_stay_intact(self):
        dockerfile = DOCKERFILE.read_text()
        self.assertIn("USER 1000:1000\n", dockerfile)
        self.assertIn('ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "Mahjong.Autotable.Api.dll"]', dockerfile)
        self.assertIn('ConnectionStrings__Sqlite="Data Source=/data/mahjong-autotable.db"', dockerfile)
        self.assertIn("HOME=/data", dockerfile)
        self.assertIn("&& chown -R 1000:1000 /data /app", dockerfile)
        self.assertEqual(dockerfile.count("--mount=type=tmpfs,target=/build-work"), 3)
        self.assertIn("--mount=type=tmpfs,target=/var/lib/apt/lists", dockerfile)
        self.assertIn("--mount=type=tmpfs,target=/var/cache/apt", dockerfile)
        self.assertIn("chmod 1777 /build-work", dockerfile)
        self.assertIn("TMPDIR=/build-work apt-get update", dockerfile)
        self.assertNotRegex(dockerfile, r"(?i)allow-unauthenticated|allow-insecure|sandbox::user")


if __name__ == "__main__":
    unittest.main()
