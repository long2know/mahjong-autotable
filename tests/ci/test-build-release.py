#!/usr/bin/env python3
"""Build-release contracts: stdlib only, all GitHub calls mocked, no remote writes."""

import copy
import importlib.util
import io
import json
import os
from pathlib import Path
import re
import subprocess
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("build_release", ROOT / "scripts/publish_build_release.py")
RELEASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RELEASE)

REPOSITORY = "long2know/mahjong-autotable"
IMAGE = f"ghcr.io/{REPOSITORY}"
COMMIT = "a" * 40
PREVIOUS = "b" * 40
DIGEST = "sha256:" + "c" * 64
ENV = {
    "GITHUB_REPOSITORY": REPOSITORY, "GITHUB_SERVER_URL": "https://github.com",
    "GITHUB_REF": "refs/heads/main", "GITHUB_EVENT_NAME": "push",
    "GITHUB_SHA": COMMIT, "GITHUB_RUN_ID": "100", "GITHUB_RUN_ATTEMPT": "1",
    "BUILD_RESULT": "success", "BUILD_RUN_ATTEMPT": "1",
    "IMAGE": IMAGE, "IMAGE_DIGEST": DIGEST,
    "IMAGE_TAGS": f"{IMAGE}:latest\n{IMAGE}:sha-{COMMIT}",
    "PLATFORMS": "linux/amd64,linux/arm64",
}


def metadata(**changes):
    return RELEASE.build_metadata({**ENV, **changes})


def recorded(build=None, **changes):
    build = build or metadata()
    return {
        "id": 10, "published_at": "2026-09-21T18:00:00Z",
        "tag_name": build["tag"], "target_commitish": build["commit"],
        "draft": False, "prerelease": True,
        "body": RELEASE.metadata_header(build) + "Original PR notes.\n",
        **changes,
    }


def pull(number, **changes):
    return {
        "number": number, "merged_at": "2026-09-21T17:00:00Z",
        "merge_commit_sha": COMMIT,
        "base": {"ref": "main", "repo": {"full_name": REPOSITORY}},
        **changes,
    }


class FakeGitHub:
    def __init__(self):
        self.calls = []
        self.tags = {}
        self.releases = []
        self.pulls = []
        self.comparisons = {}
        self.annotated_tags = {}
        self.errors = {}
        self.notes = "## What's Changed\n* First change in #41\n* Second change in #42"
        self.created_tag_commit = None

    def add_release(self, release):
        self.releases.append(release)
        self.tags[release["tag_name"]] = {
            "type": "commit", "sha": release["target_commitish"],
        }

    def request(self, path, *, data=None, paginate=False):
        self.calls.append((path, copy.deepcopy(data), paginate))
        if path in self.errors:
            error = self.errors[path]
            if callable(error):
                return error(data)
            raise error
        if path.startswith("git/ref/tags/"):
            tag = path.removeprefix("git/ref/tags/")
            if tag not in self.tags:
                raise RELEASE.ApiError(path, 404)
            return {"object": self.tags[tag]}
        if path.startswith("git/tags/"):
            return {"object": self.annotated_tags[path.removeprefix("git/tags/")]}
        if path.startswith("releases/tags/"):
            tag = path.removeprefix("releases/tags/")
            match = next((r for r in self.releases if r["tag_name"] == tag), None)
            if match is None:
                raise RELEASE.ApiError(path, 404)
            return match
        if path == "releases?per_page=100":
            if not paginate:
                raise AssertionError("All release pages must be considered")
            return self.releases[:]
        if path.startswith("compare/"):
            return {"status": self.comparisons[path]}
        if path == "releases/generate-notes":
            return {"body": self.notes}
        if path == f"commits/{COMMIT}/pulls?per_page=100":
            if not paginate:
                raise AssertionError("All associated PR pages must be considered")
            return self.pulls
        if path == "releases" and data is not None:
            release = recorded(**data)
            self.add_release(release)
            if self.created_tag_commit:
                self.tags[data["tag_name"]]["sha"] = self.created_tag_commit
            return release
        raise AssertionError(f"Unexpected API call: {path}")

    @property
    def writes(self):
        return [call for call in self.calls if call[1] is not None
                and call[0] != "releases/generate-notes"]

    def baseline(self, commit=PREVIOUS, run="90", status="ahead", **changes):
        build = metadata(GITHUB_SHA=commit, GITHUB_RUN_ID=run,
                         IMAGE_TAGS=f"{IMAGE}:latest\n{IMAGE}:sha-{commit}")
        release = recorded(build, **changes)
        self.add_release(release)
        self.comparisons[f"compare/{commit}...{COMMIT}?per_page=1"] = status
        return release


class BuildReleaseTests(unittest.TestCase):
    def setUp(self):
        self.api = FakeGitHub()

    def test_identity_format_and_exact_build_outputs(self):
        build = metadata()
        header = RELEASE.metadata_header(build)
        self.assertEqual(build["tag"], "build-100-1")
        self.assertIn(COMMIT, header)
        self.assertIn(f"/commit/{COMMIT}", header)
        self.assertIn("/actions/runs/100/attempts/1", header)
        self.assertIn(f"docker pull {IMAGE}@{DIGEST}", header)
        for value in build["tags"] + build["platforms"]:
            self.assertIn(f"`{value}`", header)
        self.assertIn("**not** signing, SBOM, smoke-test, or deployment qualification", header)

    def test_rejects_untrusted_context_or_incomplete_build_identity(self):
        invalid = {
            "GITHUB_REF": ["refs/heads/preview", "refs/tags/v1.2.3", "refs/pull/1/merge", ""],
            "GITHUB_EVENT_NAME": ["pull_request", "pull_request_target", "workflow_run", ""],
            "BUILD_RESULT": ["failure", "cancelled", "skipped", ""],
            "GITHUB_REPOSITORY": ["fork/mahjong-autotable", "../escape", ""],
            "GITHUB_SERVER_URL": ["http://github.com", "https://example.org", ""],
            "GITHUB_SHA": ["main", "abcd1234", COMMIT + "\n", ""],
            "GITHUB_RUN_ID": ["0", "-1", "1;echo unsafe", ""],
            "BUILD_RUN_ATTEMPT": ["0", "attempt-1", ""],
            "IMAGE": [IMAGE + ":latest", "ghcr.io/another/image", ""],
            "IMAGE_DIGEST": ["", "latest", "sha256:123", DIGEST + "\n"],
            "IMAGE_TAGS": ["", f"{IMAGE}:latest", f"other:tag\n{IMAGE}:sha-{COMMIT}",
                           f"{IMAGE}:sha-{COMMIT}\n$(echo unsafe)",
                           f"{IMAGE}:sha-{COMMIT}\n{IMAGE}:非ascii"],
            "PLATFORMS": ["", "linux/amd64,", "windows/amd64", "linux/amd64\nunsafe"],
        }
        for key, values in invalid.items():
            for value in values:
                with self.subTest(key=key, value=value):
                    with self.assertRaises(ValueError):
                        metadata(**{key: value})

    def test_manual_main_build_is_allowed(self):
        self.assertEqual(metadata(GITHUB_EVENT_NAME="workflow_dispatch")["tag"], "build-100-1")

    def test_release_rerun_uses_original_build_attempt_not_current_attempt(self):
        self.assertEqual(metadata(GITHUB_RUN_ATTEMPT="2"), metadata())

    def test_first_release_filters_and_deduplicates_source_commit_prs(self):
        self.api.pulls = [
            pull(42), pull(41), pull(42), pull(43, merged_at=None),
            pull(44, merge_commit_sha=PREVIOUS),
            pull(45, base={"ref": "preview", "repo": {"full_name": REPOSITORY}}),
            pull(46, base={"ref": "main", "repo": {"full_name": "other/repo"}}),
        ]
        result = RELEASE.record_build(self.api, metadata(), publish=True)
        self.assertIn("[#41]", result["body"])
        self.assertEqual(result["body"].count("[#42]"), 1)
        for number in (43, 44, 45, 46):
            self.assertNotIn(f"[#{number}]", result["body"])
        self.assertIn("**not** a complete historical PR inventory", result["body"])
        self.assertEqual(result["target_commitish"], COMMIT)
        self.assertEqual(result["tag_name"], "build-100-1")
        self.assertIs(result["draft"], False)
        self.assertIs(result["prerelease"], True)
        self.assertEqual(result["make_latest"], "false")
        self.assertEqual(self.api.tags["build-100-1"]["sha"], COMMIT)
        self.assertEqual(len(self.api.writes), 1)

    def test_first_record_with_no_pr_is_explicit(self):
        result = RELEASE.record_build(self.api, metadata())
        self.assertIn("No merged main PR is associated", result["body"])
        self.assertNotIn("[#", result["body"])
        self.assertEqual(self.api.writes, [])

    def test_production_mobile_draft_and_unmanaged_releases_are_not_baselines(self):
        self.api.releases = [
            recorded(tag_name="v1.2.3", prerelease=False),
            recorded(tag_name="mobile-123"),
            recorded(tag_name="build-90-1", draft=True),
            recorded(tag_name="build-80-1", body="Not managed by this helper"),
        ]
        result = RELEASE.record_build(self.api, metadata())
        self.assertIn("No preceding published build record", result["body"])
        self.assertFalse(any(path.startswith("compare/") for path, _, _ in self.api.calls))

    def test_batch_pr_notes_are_generated_for_the_explicit_previous_build_range(self):
        previous = self.api.baseline()
        result = RELEASE.record_build(self.api, metadata(), publish=True)
        self.assertIn("#41", result["body"])
        self.assertIn("#42", result["body"])
        self.assertIn(f"/compare/{PREVIOUS}...{COMMIT}", result["body"])
        self.assertIn(("releases/generate-notes", {
            "tag_name": "build-100-1", "target_commitish": COMMIT,
            "previous_tag_name": previous["tag_name"],
        }, False), self.api.calls)
        self.assertFalse(any(path.startswith("commits/") for path, _, _ in self.api.calls))

    def test_baseline_is_latest_published_ancestor_not_newer_or_divergent_source(self):
        self.api.baseline(published_at="2026-09-21T17:00:00Z")
        self.api.baseline("d" * 40, "91", "behind", published_at="2026-09-21T18:00:00Z")
        self.api.baseline("e" * 40, "92", "diverged", published_at="2026-09-21T19:00:00Z")
        result = RELEASE.record_build(self.api, metadata())
        self.assertIn("since [build-90-1]", result["body"])
        self.assertNotIn("since [build-91-1]", result["body"])
        self.assertNotIn("since [build-92-1]", result["body"])

    def test_no_ancestor_uses_honest_first_record_fallback(self):
        self.api.baseline(status="behind")
        result = RELEASE.record_build(self.api, metadata())
        self.assertIn("No preceding published build record", result["body"])

    def test_same_source_rebuild_has_no_new_prs_or_generated_notes(self):
        self.api.baseline(commit=COMMIT, status="identical")
        result = RELEASE.record_build(self.api, metadata())
        self.assertIn("No source changes or newly included PRs", result["body"])
        self.assertFalse(any(path.startswith("commits/") or path == "releases/generate-notes"
                             for path, _, _ in self.api.calls))

    def test_empty_generated_notes_keep_exact_commit_range(self):
        self.api.baseline()
        self.api.notes = ""
        result = RELEASE.record_build(self.api, metadata())
        self.assertIn("No PR entries generated", result["body"])
        self.assertIn(f"/compare/{PREVIOUS}...{COMMIT}", result["body"])

    def test_release_only_retry_is_idempotent_without_recomputing_notes(self):
        existing = recorded()
        self.api.add_release(existing)
        result = RELEASE.record_build(self.api, metadata(GITHUB_RUN_ATTEMPT="2"), publish=True)
        self.assertEqual(result, existing)
        self.assertEqual(self.api.writes, [])
        self.assertFalse(any(path == "releases?per_page=100" for path, _, _ in self.api.calls))

    def test_existing_record_rejects_conflicting_digest_tags_or_platforms(self):
        self.api.add_release(recorded())
        cases = [
            {"IMAGE_DIGEST": "sha256:" + "d" * 64},
            {"IMAGE_TAGS": f"{IMAGE}:sha-{COMMIT}"},
            {"PLATFORMS": "linux/amd64"},
        ]
        for change in cases:
            with self.subTest(change=change):
                with self.assertRaisesRegex(ValueError, "refusing to overwrite"):
                    RELEASE.record_build(self.api, metadata(**change), publish=True)
        self.assertEqual(self.api.writes, [])

    def test_rebuilding_creates_distinct_attempt_record_without_overwriting_previous(self):
        existing = recorded()
        self.api.add_release(existing)
        self.api.comparisons[f"compare/{COMMIT}...{COMMIT}?per_page=1"] = "identical"
        rebuilt = metadata(BUILD_RUN_ATTEMPT="2", IMAGE_DIGEST="sha256:" + "d" * 64)
        result = RELEASE.record_build(self.api, rebuilt, publish=True)
        self.assertEqual(result["tag_name"], "build-100-2")
        self.assertIn(rebuilt["digest"], result["body"])
        self.assertEqual(self.api.releases[0], existing)
        self.assertEqual(len(self.api.releases), 2)
        self.assertEqual(len(self.api.writes), 1)

    def test_conflicting_tag_fails_before_any_mutation(self):
        self.api.tags["build-100-1"] = {"type": "commit", "sha": PREVIOUS}
        with self.assertRaisesRegex(ValueError, "refusing to retag"):
            RELEASE.record_build(self.api, metadata(), publish=True)
        self.assertEqual(self.api.writes, [])

    def test_matching_orphan_tag_can_finish_interrupted_release_creation(self):
        self.api.tags["build-100-1"] = {"type": "commit", "sha": COMMIT}
        RELEASE.record_build(self.api, metadata(), publish=True)
        self.assertEqual(len(self.api.writes), 1)
        self.assertEqual(self.api.tags["build-100-1"]["sha"], COMMIT)

    def test_existing_release_with_missing_tag_is_not_silently_repaired(self):
        self.api.releases.append(recorded())
        with self.assertRaisesRegex(ValueError, "refusing to recreate history"):
            RELEASE.record_build(self.api, metadata(), publish=True)
        self.assertEqual(self.api.writes, [])

    def test_release_flags_target_and_visible_identity_cannot_disagree(self):
        cases = [
            {"tag_name": "build-100-2"}, {"target_commitish": "main"},
            {"target_commitish": PREVIOUS}, {"draft": True}, {"prerelease": False},
            {"body": RELEASE.metadata_header(metadata()).replace(DIGEST, "sha256:" + "d" * 64)},
        ]
        for change in cases:
            with self.subTest(change=change):
                with self.assertRaisesRegex(ValueError, "conflicts"):
                    RELEASE.verify_release(recorded(**change), metadata())

    def test_previous_tag_is_verified_before_generating_range(self):
        previous = self.api.baseline()
        self.api.tags[previous["tag_name"]]["sha"] = "d" * 40
        with self.assertRaisesRegex(ValueError, "refusing to retag"):
            RELEASE.record_build(self.api, metadata(), publish=True)
        self.assertEqual(self.api.writes, [])
        self.assertFalse(any(path == "releases/generate-notes" for path, _, _ in self.api.calls))

    def test_new_release_tag_is_verified_after_create(self):
        self.api.created_tag_commit = PREVIOUS
        with self.assertRaisesRegex(ValueError, "refusing to retag"):
            RELEASE.record_build(self.api, metadata(), publish=True)

    def test_annotated_tag_resolution_and_non_commit_rejection(self):
        self.api.tags["build-100-1"] = {"type": "tag", "sha": PREVIOUS}
        self.api.annotated_tags[PREVIOUS] = {"type": "commit", "sha": COMMIT}
        self.assertEqual(RELEASE.tag_commit(self.api, "build-100-1"), COMMIT)
        for obj in ({"type": "tree", "sha": COMMIT}, {"type": "tag", "sha": PREVIOUS}):
            self.api.annotated_tags[PREVIOUS] = obj
            with self.assertRaisesRegex(ValueError, "does not resolve to a commit"):
                RELEASE.tag_commit(self.api, "build-100-1")

    def test_concurrent_identical_creation_is_verified_not_updated(self):
        def race(payload):
            self.api.add_release(recorded(**payload))
            raise RELEASE.ApiError("releases", 422)

        self.api.errors["releases"] = race
        result = RELEASE.record_build(self.api, metadata(), publish=True)
        self.assertEqual(result["tag_name"], "build-100-1")
        self.assertEqual(len(self.api.writes), 1)

    def test_concurrent_conflicting_creation_is_not_overwritten(self):
        def race(payload):
            self.api.add_release(recorded(body="Conflicting record"))
            raise RELEASE.ApiError("releases", 422)

        self.api.errors["releases"] = race
        with self.assertRaisesRegex(ValueError, "refusing to overwrite"):
            RELEASE.record_build(self.api, metadata(), publish=True)
        self.assertEqual(len(self.api.writes), 1)

    def test_validation_failure_without_existing_release_is_fatal(self):
        self.api.errors["releases"] = RELEASE.ApiError("releases", 422)
        with self.assertRaises(RELEASE.ApiError):
            RELEASE.record_build(self.api, metadata(), publish=True)

    def test_release_creation_permission_or_server_failure_is_fatal_without_updates(self):
        for status in (403, 404, 429, 500):
            with self.subTest(status=status):
                api = FakeGitHub()
                api.errors["releases"] = RELEASE.ApiError("releases", status)
                with self.assertRaises(RELEASE.ApiError) as raised:
                    RELEASE.record_build(api, metadata(), publish=True)
                self.assertEqual(raised.exception.status, status)
                self.assertEqual(len(api.writes), 1)
                self.assertEqual(api.releases, [])

    def test_auth_rate_limit_and_api_failures_never_become_empty_history(self):
        paths = [
            "git/ref/tags/build-100-1", "releases/tags/build-100-1",
            "releases?per_page=100", f"commits/{COMMIT}/pulls?per_page=100",
        ]
        for path in paths:
            for status in (401, 403, 429, 500):
                with self.subTest(path=path, status=status):
                    api = FakeGitHub()
                    api.errors[path] = RELEASE.ApiError(path, status)
                    with self.assertRaises(RELEASE.ApiError):
                        RELEASE.record_build(api, metadata(), publish=True)
                    self.assertEqual(api.writes, [])

    def test_notes_generation_failure_does_not_publish_partial_record(self):
        self.api.baseline()
        self.api.errors["releases/generate-notes"] = RELEASE.ApiError("releases/generate-notes", 500)
        with self.assertRaises(RELEASE.ApiError):
            RELEASE.record_build(self.api, metadata(), publish=True)
        self.assertEqual(self.api.writes, [])

    def test_default_dry_run_may_generate_notes_but_never_creates_release_or_tag(self):
        self.api.baseline()
        result = RELEASE.record_build(self.api, metadata())
        self.assertEqual(result["tag_name"], "build-100-1")
        self.assertEqual(self.api.writes, [])
        self.assertNotIn("build-100-1", self.api.tags)

    @patch.object(RELEASE.subprocess, "run")
    def test_gh_adapter_paginates_releases_and_prs(self, run):
        run.return_value = subprocess.CompletedProcess([], 0, json.dumps([[{"id": 1}], [{"id": 2}]]), "")
        for path in ("releases?per_page=100", f"commits/{COMMIT}/pulls?per_page=100"):
            with self.subTest(path=path):
                self.assertEqual(RELEASE.GitHub(REPOSITORY).request(path, paginate=True),
                                 [{"id": 1}, {"id": 2}])
                command = run.call_args.args[0]
                self.assertIn("--paginate", command)
                self.assertIn("--slurp", command)
                self.assertIn(f"repos/{REPOSITORY}/{path}", command)

    @patch.object(RELEASE.subprocess, "run")
    def test_gh_adapter_passes_json_as_stdin_without_shell_or_secret_arguments(self, run):
        run.return_value = subprocess.CompletedProcess([], 0, "{}", "")
        payload = {"body": "Quoted `$(do-not-execute)` text\n\"a PR title\""}
        RELEASE.GitHub(REPOSITORY).request("releases", data=payload)
        command = run.call_args.args[0]
        self.assertEqual(command[:4], ["gh", "api", "--hostname", "github.com"])
        self.assertEqual(command[command.index("--method") + 1], "POST")
        self.assertEqual(command[-2:], ["--input", "-"])
        self.assertEqual(json.loads(run.call_args.kwargs["input"]), payload)
        self.assertFalse(run.call_args.kwargs.get("shell", False))
        self.assertNotIn(payload["body"], command)

    @patch.object(RELEASE.subprocess, "run")
    def test_gh_adapter_reports_status_without_echoing_sensitive_stderr(self, run):
        run.return_value = subprocess.CompletedProcess(
            [], 1, "", "gh: Resource inaccessible (HTTP 403)\nsecret-sentinel"
        )
        with self.assertRaises(RELEASE.ApiError) as raised:
            RELEASE.GitHub(REPOSITORY).request("releases")
        self.assertEqual(raised.exception.status, 403)
        self.assertNotIn("secret-sentinel", str(raised.exception))

    @patch.object(RELEASE, "GitHub")
    def test_cli_publish_is_guarded_before_any_api_request(self, github):
        with patch.dict(os.environ, ENV, clear=True), \
                patch.object(RELEASE.sys, "argv", ["publish_build_release.py", "--publish"]), \
                patch("sys.stderr", new_callable=io.StringIO) as stderr:
            self.assertEqual(RELEASE.main(), 1)
        github.assert_not_called()
        self.assertIn("requires the main Actions job", stderr.getvalue())

    @patch.object(RELEASE, "GitHub")
    def test_cli_defaults_to_dry_run(self, github):
        github.return_value = self.api
        with patch.dict(os.environ, ENV, clear=True), \
                patch.object(RELEASE.sys, "argv", ["publish_build_release.py"]), \
                patch("sys.stdout", new_callable=io.StringIO) as stdout:
            self.assertEqual(RELEASE.main(), 0)
        self.assertEqual(json.loads(stdout.getvalue())["tag_name"], "build-100-1")
        self.assertEqual(self.api.writes, [])


class WorkflowContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = (ROOT / ".github/workflows/docker-build.yml").read_text()
        cls.build, cls.release = cls.workflow.split("\n  release-record:\n")
        cls.production = (ROOT / ".github/workflows/release.yml").read_text()

    def test_release_job_only_follows_successful_upstream_main_image_publication(self):
        for condition in (
            "needs: build-and-push",
            "needs.build-and-push.result == 'success'",
            "github.repository == 'long2know/mahjong-autotable'",
            "github.ref == 'refs/heads/main'",
            "(github.event_name == 'push' || github.event_name == 'workflow_dispatch')",
        ):
            self.assertIn(condition, self.release)
        self.assertNotIn("continue-on-error", self.release)
        self.assertNotIn("pull_request", self.workflow)
        self.assertIn('tags: ["v*.*.*"]', self.workflow)
        self.assertNotRegex(self.workflow, r"tags:.*build-")

    def test_content_write_is_scoped_to_release_job_without_signing_or_package_write(self):
        self.assertIn("permissions:\n  contents: read\n  packages: write\n", self.build)
        self.assertNotIn("contents: write", self.build)
        self.assertIn("permissions:\n      contents: write\n      pull-requests: read", self.release)
        self.assertNotIn("packages: write", self.release)
        self.assertNotIn("id-token:", self.workflow)
        self.assertEqual(self.workflow.count("contents: write"), 1)
        self.assertIn("GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}", self.release)
        self.assertIn("persist-credentials: false", self.release)

    def test_release_uses_build_outputs_and_built_sha_not_a_registry_lookup(self):
        for text in (
            "image-digest: ${{ steps.build.outputs.digest }}",
            "image-tags: ${{ steps.meta.outputs.tags }}",
            "build-attempt: ${{ steps.publication.outputs.build-attempt }}",
            "id: publication",
            r'''printf 'build-attempt=%s\n' "$GITHUB_RUN_ATTEMPT" >> "$GITHUB_OUTPUT"''',
        ):
            self.assertIn(text, self.build)
        for text in (
            "IMAGE_DIGEST: ${{ needs.build-and-push.outputs.image-digest }}",
            "IMAGE_TAGS: ${{ needs.build-and-push.outputs.image-tags }}",
            "BUILD_RUN_ATTEMPT: ${{ needs.build-and-push.outputs.build-attempt }}",
            "BUILD_RESULT: ${{ needs.build-and-push.result }}",
            "ref: ${{ github.sha }}",
            "run: python3 -B scripts/publish_build_release.py --publish",
        ):
            self.assertIn(text, self.release)
        self.assertNotIn("imagetools", self.release)
        self.assertNotIn(":latest", self.release)
        self.assertNotIn("${{ github.run_attempt }}", self.release)
        self.assertIn("PLATFORMS: linux/amd64,linux/arm64", self.build)
        self.assertIn("platforms: ${{ env.PLATFORMS }}", self.build)
        self.assertIn("BUILD_SHA=${{ github.sha }}", self.build)
        self.assertIn("push: true", self.build)

    def test_mocked_contract_checks_run_before_image_build(self):
        tests = self.build.index("run: python3 -B tests/ci/test-build-release.py")
        image_build = self.build.index("uses: docker/build-push-action@")
        self.assertLess(tests, image_build)

    def test_actions_remain_sha_pinned_and_version_release_gates_are_separate(self):
        actions = re.findall(r"^\s+uses: (.+)$", self.workflow, re.MULTILINE)
        self.assertGreater(len(actions), 0)
        for action in actions:
            self.assertRegex(action, r"^[\w./-]+@[0-9a-f]{40}(?:\s+#.*)?$")
        self.assertIn('- "v*.*.*"', self.production)
        self.assertIn("needs: [smoke, verify-signature, verify-sbom]", self.production)
        self.assertIn('gh release create "$TAG"', self.production)
        self.assertNotIn("build-", self.production.split("\npermissions:")[0].split("\non:")[1])


if __name__ == "__main__":
    unittest.main()
