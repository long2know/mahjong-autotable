#!/usr/bin/env python3
"""Argument/error/secret-preservation tests; real image builds are separate."""
import base64
import json
import os
import pathlib
import secrets
import shutil
import subprocess
import unittest
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUTPUT = pathlib.Path(os.environ.get(
    "BUILD_SCRIPT_TEST_DIR", str(ROOT / ".work/build-entrypoint-tests"))).resolve()

DOCKER_STUB = r"""#!/usr/bin/env python3
import json, os, pathlib, sys
args = sys.argv[1:]
with pathlib.Path(os.environ["DOCKER_CALLS"]).open("a") as out:
    out.write(json.dumps(args) + "\n")
if args[:2] == ["buildx", "version"]:
    if os.environ.get("FAIL_BUILDX"):
        sys.exit(int(os.environ["FAIL_BUILDX"]))
    print("buildx test double")
elif args[:2] == ["buildx", "build"]:
    sys.exit(int(os.environ.get("FAIL_BUILD", "0")))
elif args[:2] == ["image", "inspect"]:
    if os.environ.get("FAIL_INSPECT"):
        sys.exit(int(os.environ["FAIL_INSPECT"]))
    print("linux" if args[-1] == "{{.Os}}" else "Built sha256:test (linux/amd64)")
elif args[:2] == ["image", "save"]:
    if os.environ.get("FAIL_SAVE"):
        sys.exit(int(os.environ["FAIL_SAVE"]))
    pathlib.Path(args[args.index("--output") + 1]).write_bytes(b"test archive")
else:
    raise SystemExit("unexpected Docker invocation")
"""

GIT_STUB = r"""#!/usr/bin/env python3
import json, os, pathlib, sys
args = sys.argv[1:]
with pathlib.Path(os.environ["GIT_CALLS"]).open("a") as out:
    out.write(json.dumps(args) + "\n")
if args[:1] == ["--no-optional-locks"]:
    args = args[1:]
if args[:1] != ["-C"]:
    raise SystemExit("Git must operate on the source root, not the caller")
args = args[2:]
if args == ["rev-parse", "--verify", "HEAD"]:
    if os.environ.get("GIT_STATE") == "unavailable":
        print("fatal: not a git repository", file=sys.stderr)
        sys.exit(128)
    print("a" * 40)
elif args == ["status", "--porcelain", "--untracked-files=normal"]:
    if os.environ.get("FAIL_GIT_STATUS"):
        sys.exit(int(os.environ["FAIL_GIT_STATUS"]))
    state = os.environ.get("GIT_STATE", "clean")
    if state == "tracked":
        print(" M tracked-file")
    elif state == "untracked":
        print("?? untracked-file")
else:
    raise SystemExit("unexpected/non-read-only Git invocation")
"""


class BuildEntrypoints(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pwsh = shutil.which("pwsh")
        if not cls.pwsh:
            raise RuntimeError("pwsh is required to execute PowerShell coverage")
        cls.work = OUTPUT / uuid.uuid4().hex
        cls.work.mkdir(parents=True)
        cls.alias = cls.work / "repo with spaces"
        cls.alias.symlink_to(ROOT, target_is_directory=True)
        cls.bin = cls.work / "mock tools"
        cls.bin.mkdir()
        docker = cls.bin / "docker"
        docker.write_text(DOCKER_STUB)
        docker.chmod(0o755)
        git = cls.bin / "git"
        git.write_text(GIT_STUB)
        git.chmod(0o755)
        cls.no_git_bin = cls.work / "tools without git"
        cls.no_git_bin.mkdir()
        for tool in ["bash", "dirname", "date", "python3"]:
            (cls.no_git_bin / tool).symlink_to(shutil.which(tool))
        (cls.no_git_bin / "docker").symlink_to(docker)

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.work)

    def invoke(self, kind, options=None, overrides=None, script_root=None):
        cwd = self.work / ("caller with spaces " + uuid.uuid4().hex)
        cwd.mkdir()
        calls = cwd / "docker-calls.jsonl"
        env = os.environ.copy()
        for key in ["MAHJONG_IMAGE", "MAHJONG_PLATFORM", "BUILD_SHA",
                    "FAIL_BUILDX", "FAIL_BUILD", "FAIL_INSPECT", "FAIL_SAVE",
                    "GIT_STATE", "FAIL_GIT_STATUS"]:
            env.pop(key, None)
        env.update({
            "PATH": str(self.bin) + os.pathsep + env["PATH"],
            "DOCKER_CALLS": str(calls),
            "GIT_CALLS": str(cwd / "git-calls.jsonl"),
            "TMPDIR": str(cwd),
        })
        env.update(overrides or {})
        source = script_root or self.alias
        if kind == "bash":
            command = ["bash", str(source / "build.sh"), *(options or [])]
        else:
            command = [self.pwsh, "-NoLogo", "-NoProfile", "-File",
                       str(source / "build.ps1"), *(options or [])]
        result = subprocess.run(command, cwd=cwd, env=env, capture_output=True, text=True)
        recorded = [json.loads(line) for line in calls.read_text().splitlines()] if calls.exists() else []
        return result, recorded, cwd

    def test_default_local_linux_image_and_context(self):
        for kind in ["bash", "powershell"]:
            with self.subTest(kind=kind):
                result, calls, _ = self.invoke(kind)
                self.assertEqual(result.returncode, 0, result.stderr)
                build = next(args for args in calls if args[:2] == ["buildx", "build"])
                self.assertIn("--load", build)
                self.assertNotIn("--push", build)
                self.assertEqual(build[build.index("--tag") + 1], "mahjong-autotable:local")
                self.assertEqual(build[build.index("--platform") + 1], "linux/amd64")
                self.assertEqual(pathlib.Path(build[-1]).resolve(), ROOT)
                self.assertEqual(pathlib.Path(build[build.index("--file") + 1]).resolve(), ROOT / "Dockerfile")
                identity = build[build.index("--build-arg") + 1].removeprefix("BUILD_SHA=")
                self.assertRegex(identity, r"^a{40}-\d{8}T\d{6}Z$")
                self.assertIn("Build identity: " + identity, result.stdout)
                self.assertFalse(any("compose" in args for args in calls))

    def test_generated_identity_marks_tracked_and_untracked_changes(self):
        for kind in ["bash", "powershell"]:
            for state in ["clean", "tracked", "untracked"]:
                with self.subTest(kind=kind, state=state):
                    result, calls, cwd = self.invoke(kind, overrides={"GIT_STATE": state})
                    self.assertEqual(result.returncode, 0, result.stderr)
                    build = next(args for args in calls if args[:2] == ["buildx", "build"])
                    dirty = "" if state == "clean" else "-dirty"
                    self.assertRegex(build[build.index("--build-arg") + 1],
                                     "^BUILD_SHA=a{40}" + dirty + r"-\d{8}T\d{6}Z$")
                    git_calls = [json.loads(line) for line in (cwd / "git-calls.jsonl").read_text().splitlines()]
                    self.assertEqual(len(git_calls), 2)
                    self.assertIn("--no-optional-locks", git_calls[1])
                    for args in git_calls:
                        self.assertEqual(pathlib.Path(args[args.index("-C") + 1]).resolve(), ROOT)

    def test_generated_identity_without_git_or_commit_is_honest(self):
        for kind in ["bash", "powershell"]:
            for overrides in [{"GIT_STATE": "unavailable"}, {"PATH": str(self.no_git_bin)}]:
                with self.subTest(kind=kind, overrides=overrides):
                    result, calls, _ = self.invoke(kind, overrides=overrides)
                    self.assertEqual(result.returncode, 0, result.stderr)
                    build = next(args for args in calls if args[:2] == ["buildx", "build"])
                    self.assertRegex(build[build.index("--build-arg") + 1],
                                     r"^BUILD_SHA=local-source-unavailable-\d{8}T\d{6}Z$")

    def test_explicit_identity_is_unchanged_and_never_queries_git(self):
        for kind in ["bash", "powershell"]:
            for identity in ["public build 'quoted' & <literal>", ""]:
                with self.subTest(kind=kind, identity=identity):
                    result, calls, cwd = self.invoke(kind, overrides={"BUILD_SHA": identity})
                    self.assertEqual(result.returncode, 0, result.stderr)
                    build = next(args for args in calls if args[:2] == ["buildx", "build"])
                    self.assertEqual(build[build.index("--build-arg") + 1], "BUILD_SHA=" + identity)
                    self.assertIn("Build identity: " + identity, result.stdout)
                    self.assertFalse((cwd / "git-calls.jsonl").exists())

    def test_latest_is_opt_in_and_still_gets_a_generated_identity(self):
        for kind in ["bash", "powershell"]:
            with self.subTest(kind=kind):
                options = ["--tag" if kind == "bash" else "-Tag", "mahjong-autotable:latest"]
                result, calls, _ = self.invoke(kind, options)
                self.assertEqual(result.returncode, 0, result.stderr)
                build = next(args for args in calls if args[:2] == ["buildx", "build"])
                self.assertEqual(build[build.index("--tag") + 1], "mahjong-autotable:latest")
                self.assertRegex(build[build.index("--build-arg") + 1], r"^BUILD_SHA=a{40}-\d{8}T\d{6}Z$")

    def test_source_env_file_is_neither_loaded_nor_exposed(self):
        source = self.work / "source with env"
        source.mkdir()
        for name in ["build.sh", "build.ps1", "Dockerfile"]:
            shutil.copy2(ROOT / name, source / name)
        sentinel = "do-not-load-this-runtime-value"
        (source / ".env").write_text("BUILD_SHA=dotenv-build\nJWT_SIGNING_KEY=" + sentinel + "\n")
        for kind in ["bash", "powershell"]:
            with self.subTest(kind=kind):
                result, calls, _ = self.invoke(kind, script_root=source)
                self.assertEqual(result.returncode, 0, result.stderr)
                output = result.stdout + result.stderr + json.dumps(calls)
                self.assertNotIn(sentinel, output)
                self.assertNotIn("dotenv-build", output)

    def test_git_status_failure_cannot_claim_a_clean_build(self):
        for kind in ["bash", "powershell"]:
            with self.subTest(kind=kind):
                result, calls, _ = self.invoke(kind, overrides={"FAIL_GIT_STATUS": "47"})
                self.assertEqual(result.returncode, 47, result.stderr)
                self.assertFalse(any(args[:2] == ["buildx", "build"] for args in calls))

    def test_dockerfile_shares_identity_without_invalidating_dependency_cache(self):
        source = (ROOT / "Dockerfile").read_text()
        frontend, rest = source.split("FROM mcr.microsoft.com/dotnet/sdk:", 1)
        runtime = rest.split(" AS runtime", 1)[1]
        self.assertEqual(frontend.count('ARG BUILD_SHA=""'), 1)
        self.assertLess(frontend.index("npm ci"), frontend.index('ARG BUILD_SHA=""'))
        self.assertLess(frontend.index('ARG BUILD_SHA=""'), frontend.index("npm run build"))
        self.assertIn('ARG BUILD_SHA=""\nENV BUILD_SHA=${BUILD_SHA}', runtime)

    def test_tag_platform_metadata_and_archive_with_spaces(self):
        for kind in ["bash", "powershell"]:
            with self.subTest(kind=kind):
                archive = self.work / (kind + " saved image.tar")
                options = (["--tag", "example:test", "--platform", "linux/arm64",
                            "--archive", str(archive), "--no-cache"] if kind == "bash"
                           else ["-Tag", "example:test", "-Platform", "linux/arm64",
                                 "-Archive", str(archive), "-NoCache"])
                result, calls, _ = self.invoke(kind, options, {"BUILD_SHA": "public-build-id"})
                self.assertEqual(result.returncode, 0, result.stderr)
                build = next(args for args in calls if args[:2] == ["buildx", "build"])
                self.assertIn("--no-cache", build)
                self.assertIn("BUILD_SHA=public-build-id", build)
                self.assertEqual(build[build.index("--tag") + 1], "example:test")
                self.assertEqual(build[build.index("--platform") + 1], "linux/arm64")
                self.assertEqual(archive.read_bytes(), b"test archive")

    def test_shared_environment_defaults(self):
        for kind in ["bash", "powershell"]:
            with self.subTest(kind=kind):
                result, calls, _ = self.invoke(kind, overrides={
                    "MAHJONG_IMAGE": "example:env", "MAHJONG_PLATFORM": "linux/arm64"})
                self.assertEqual(result.returncode, 0, result.stderr)
                build = next(args for args in calls if args[:2] == ["buildx", "build"])
                self.assertEqual(build[build.index("--tag") + 1], "example:env")
                self.assertEqual(build[build.index("--platform") + 1], "linux/arm64")

    def test_build_and_inspection_failures_propagate(self):
        for kind in ["bash", "powershell"]:
            for variable, code in [("FAIL_BUILDX", 35), ("FAIL_BUILD", 37), ("FAIL_INSPECT", 41)]:
                with self.subTest(kind=kind, variable=variable):
                    result, calls, _ = self.invoke(kind, overrides={variable: str(code)})
                    self.assertEqual(result.returncode, code, result.stderr)
                    self.assertFalse(any(args[:2] == ["image", "save"] for args in calls))
                    if variable in ["FAIL_BUILDX", "FAIL_BUILD"]:
                        self.assertFalse(any(args[:2] == ["image", "inspect"] for args in calls))
                    if variable == "FAIL_BUILDX":
                        self.assertFalse(any(args[:2] == ["buildx", "build"] for args in calls))

    def test_invalid_platform_and_existing_archive_fail_without_build(self):
        existing = self.work / "existing archive.tar"
        existing.write_bytes(b"preserve")
        for kind in ["bash", "powershell"]:
            sets = ([["--platform", "windows/amd64"], ["--platform", "linux/amd64,linux/arm64"],
                     ["--archive", str(existing)]] if kind == "bash"
                    else [["-Platform", "windows/amd64"], ["-Platform", "linux/amd64,linux/arm64"],
                          ["-Archive", str(existing)]])
            for options in sets:
                with self.subTest(kind=kind, options=options):
                    result, calls, _ = self.invoke(kind, options)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertEqual(calls, [])
        self.assertEqual(existing.read_bytes(), b"preserve")

    def test_archive_failure_propagates(self):
        for kind in ["bash", "powershell"]:
            with self.subTest(kind=kind):
                archive = self.work / (kind + " failed archive.tar")
                options = (["--archive", str(archive)] if kind == "bash"
                           else ["-Archive", str(archive)])
                result, calls, _ = self.invoke(kind, options, {"FAIL_SAVE": "43"})
                self.assertEqual(result.returncode, 43, result.stderr)
                self.assertTrue(any(args[:2] == ["image", "save"] for args in calls))
                self.assertFalse(archive.exists())

    def test_bootstrap_is_idempotent_from_other_cwd(self):
        cwd = self.work / "bootstrap caller"
        cwd.mkdir()
        env_file = cwd / "operator settings.env"
        command = ["bash", str(ROOT / "scripts/compose-bootstrap.sh"),
                   "--env-file", env_file.name]
        first = subprocess.run(command, cwd=cwd, capture_output=True, text=True)
        self.assertEqual(first.returncode, 0, first.stderr)
        before = env_file.read_bytes()
        key = next(line.split(b"=", 1)[1] for line in before.splitlines()
                   if line.startswith(b"JWT_SIGNING_KEY="))
        self.assertEqual(len(base64.b64decode(key, validate=True)), 48)
        second = subprocess.run(command, cwd=cwd, capture_output=True, text=True)
        self.assertEqual(second.returncode, 0, second.stderr)
        self.assertTrue(env_file.read_bytes() == before, "Bootstrap changed an existing environment")
        self.assertFalse(key.decode() in first.stdout + first.stderr + second.stdout + second.stderr,
                         "Bootstrap exposed its generated key")

    def test_bootstrap_preserves_key_and_handles_empty_last_definition(self):
        cwd = self.work / "bootstrap definitions"
        cwd.mkdir()
        file = cwd / ".env"
        key = base64.b64encode(secrets.token_bytes(48)).decode()
        file.write_text(f'OTHER_SETTING=keep\nJWT_SIGNING_KEY="{key}"\n')
        command = ["bash", str(ROOT / "scripts/compose-bootstrap.sh"), "--env-file", str(file)]
        before = file.read_bytes()
        subprocess.run(command, cwd=cwd, check=True, capture_output=True)
        self.assertTrue(file.read_bytes() == before, "Bootstrap changed the existing key")
        with file.open("a") as out:
            out.write('JWT_SIGNING_KEY="" # unset\n')
        subprocess.run(command, cwd=cwd, check=True, capture_output=True)
        last = [line for line in file.read_text().splitlines()
                if line.startswith("JWT_SIGNING_KEY=")][-1].split("=", 1)[1]
        self.assertEqual(len(base64.b64decode(last, validate=True)), 48)
        self.assertTrue(last != key, "The final empty definition was not replaced")
        self.assertIn("OTHER_SETTING=keep", file.read_text())

    def compose_config(self, env_file, project="mahjong-dotenv-contract"):
        env = os.environ.copy()
        for key in ["JWT_SIGNING_KEY", "MAHJONG_IMAGE", "MAHJONG_PLATFORM",
                    "MAHJONG_HOST_PORT", "MAHJONG_BIND_ADDRESS", "BUILD_SHA"]:
            env.pop(key, None)
        result = subprocess.run(
            ["docker", "compose", "-f", str(ROOT / "docker-compose.yml"),
             "--env-file", str(env_file), "-p", project, "config", "--format", "json"],
            cwd=env_file.parent, env=env, capture_output=True, text=True)
        return result.returncode, json.loads(result.stdout) if result.returncode == 0 else None

    def test_actual_compose_empty_quoted_keys_generate_once(self):
        values = ["", '""', "''", ' "" # empty double quotes',
                  " '' # empty single quotes"]
        for value in values:
            with self.subTest(value=value):
                directory = self.work / ("empty dotenv " + uuid.uuid4().hex)
                directory.mkdir()
                file = directory / ".env"
                file.write_text("OTHER_SETTING=keep\nJWT_SIGNING_KEY=" + value + "\n")
                code, _ = self.compose_config(file)
                self.assertNotEqual(code, 0, "Compose must reject the original empty runtime key")
                command = ["bash", str(ROOT / "scripts/compose-bootstrap.sh"),
                           "--env-file", str(file)]
                first = subprocess.run(command, cwd=directory, capture_output=True, text=True)
                self.assertEqual(first.returncode, 0, "Bootstrap did not populate an empty key")
                code, config = self.compose_config(file)
                self.assertEqual(code, 0, "Compose rejected the generated key")
                key = config["services"]["mahjong"]["environment"]["Authentication__JwtSigningKeys__0"]
                self.assertEqual(len(base64.b64decode(key, validate=True)), 48)
                before = file.read_bytes()
                second = subprocess.run(command, cwd=directory, capture_output=True, text=True)
                self.assertEqual(second.returncode, 0)
                self.assertTrue(file.read_bytes() == before, "Repeated bootstrap changed the env/key")
                self.assertFalse(key in first.stdout + first.stderr + second.stdout + second.stderr,
                                 "A generated key was exposed")

    def test_actual_compose_quoted_keys_and_last_definition_preserved(self):
        key = base64.b64encode(secrets.token_bytes(48)).decode()
        declarations = [
            f"JWT_SIGNING_KEY={key}",
            f"JWT_SIGNING_KEY='{key}'",
            f'JWT_SIGNING_KEY="{key}"',
            f'  JWT_SIGNING_KEY = "{key}" # retained operator value',
            f"export JWT_SIGNING_KEY='{key}'",
            f"JWT_SIGNING_KEY=''\nJWT_SIGNING_KEY='{key}'",
        ]
        for index, declaration in enumerate(declarations):
            with self.subTest(case=index):
                directory = self.work / ("preserved dotenv " + uuid.uuid4().hex)
                directory.mkdir()
                file = directory / ".env"
                file.write_text("OTHER_SETTING=keep\n" + declaration + "\n")
                code, config = self.compose_config(file)
                self.assertEqual(code, 0, "Compose did not accept the quoted fixture")
                actual = config["services"]["mahjong"]["environment"]["Authentication__JwtSigningKeys__0"]
                self.assertTrue(actual == key, "Compose parsed different signing material")
                before = file.read_bytes()
                result = subprocess.run(
                    ["bash", str(ROOT / "scripts/compose-bootstrap.sh"), "--env-file", str(file)],
                    cwd=directory, capture_output=True, text=True)
                self.assertEqual(result.returncode, 0)
                self.assertTrue(file.read_bytes() == before, "Bootstrap rotated a configured key")
                self.assertFalse(key in result.stdout + result.stderr, "Configured key exposed")

    def test_bootstrap_does_not_execute_env_content(self):
        directory = self.work / "untrusted dotenv"
        directory.mkdir()
        file = directory / ".env"
        marker = directory / "must-not-exist"
        key = base64.b64encode(secrets.token_bytes(48)).decode()
        file.write_text(f"JWT_SIGNING_KEY='{key}'\n"
                        f"UNRELATED=$(printf forbidden > '{marker}')\n")
        before = file.read_bytes()
        result = subprocess.run(
            ["bash", str(ROOT / "scripts/compose-bootstrap.sh"), "--env-file", str(file)],
            cwd=directory, capture_output=True, text=True)
        self.assertEqual(result.returncode, 0)
        self.assertFalse(marker.exists(), "Bootstrap executed user env content")
        self.assertTrue(file.read_bytes() == before, "Bootstrap rewrote unrelated env content")
        self.assertFalse(key in result.stdout + result.stderr, "Configured key exposed")

    def test_real_compose_project_names_and_build_secret_boundary(self):
        directory = self.work / "compose projects"
        directory.mkdir()
        file = directory / ".env"
        key = base64.b64encode(secrets.token_bytes(48)).decode()
        file.write_text(f"JWT_SIGNING_KEY='{key}'\nMAHJONG_HOST_PORT=8951\n")
        volumes = []
        for project in ["mahjong-dotenv-a", "mahjong-dotenv-b"]:
            code, config = self.compose_config(file, project)
            self.assertEqual(code, 0, "Compose project fixture failed")
            self.assertTrue(all("container_name" not in service
                                for service in config["services"].values()))
            service = config["services"]["mahjong"]
            self.assertEqual(service["ports"][0]["target"], 8080)
            self.assertEqual(str(service["ports"][0]["published"]), "8951")
            self.assertFalse(key in json.dumps(service["build"]),
                             "Compose forwarded a runtime secret into image build settings")
            volumes.append(config["volumes"]["mahjong-data"]["name"])
        self.assertNotEqual(volumes[0], volumes[1])


if __name__ == "__main__":
    unittest.main()
