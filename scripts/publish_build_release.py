#!/usr/bin/env python3
"""Record one published main image; default to a non-publishing API dry run."""

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys


MARKER = "<!-- mahjong-main-image-build:v1 -->"
BUILD_TAG = re.compile(r"build-[1-9][0-9]*-[1-9][0-9]*")
SHA = re.compile(r"[0-9a-f]{40}")


class ApiError(RuntimeError):
    def __init__(self, path, status=None):
        self.status = status
        super().__init__(
            f"GitHub API {path} failed (HTTP {status or 'unknown'}). "
            "Check token permissions, workflow-target restrictions and release/tag rules, "
            "then rerun the failed job."
        )


class GitHub:
    def __init__(self, repository):
        self.repository = repository

    def request(self, path, *, data=None, paginate=False):
        command = [
            "gh", "api", "--hostname", "github.com",
            "--method", "POST" if data is not None else "GET",
            "-H", "Accept: application/vnd.github+json",
            "-H", "X-GitHub-Api-Version: 2022-11-28",
            f"repos/{self.repository}/{path}",
        ]
        if paginate:
            command += ["--paginate", "--slurp"]
        if data is not None:
            command += ["--input", "-"]
        result = subprocess.run(
            command, input=json.dumps(data) if data is not None else None,
            capture_output=True, text=True, timeout=90,
        )
        if result.returncode:
            status = re.search(r"\(HTTP ([0-9]{3})\)", result.stderr)
            raise ApiError(path, int(status[1]) if status else None)
        value = json.loads(result.stdout)
        return [item for page in value for item in page] if paginate else value


def optional(api, path):
    try:
        return api.request(path)
    except ApiError as error:
        if error.status != 404:
            raise
        return None


def build_metadata(env):
    if (env.get("GITHUB_REF") != "refs/heads/main"
            or env.get("GITHUB_EVENT_NAME") not in ("push", "workflow_dispatch")
            or env.get("BUILD_RESULT") != "success"):
        raise ValueError("Only successfully published main builds may have build releases.")
    repository = env.get("GITHUB_REPOSITORY", "")
    if repository != "long2know/mahjong-autotable":
        raise ValueError("Build releases are restricted to the upstream repository.")
    if env.get("GITHUB_SERVER_URL") != "https://github.com":
        raise ValueError("Unexpected GitHub server.")
    sha = env.get("GITHUB_SHA", "")
    run_id = env.get("GITHUB_RUN_ID", "")
    attempt = env.get("BUILD_RUN_ATTEMPT", "")
    digest = env.get("IMAGE_DIGEST", "")
    if not SHA.fullmatch(sha):
        raise ValueError("The built source must be an exact commit SHA.")
    if not all(re.fullmatch(r"[1-9][0-9]*", value) for value in (run_id, attempt)):
        raise ValueError("A run ID and the original build job's attempt are required.")
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
        raise ValueError("The build action's exact manifest digest is required.")
    image = env.get("IMAGE", "")
    if image != f"ghcr.io/{repository}":
        raise ValueError("Unexpected image repository.")
    tags = sorted(set(env.get("IMAGE_TAGS", "").splitlines()))
    if (f"{image}:sha-{sha}" not in tags
            or any(not re.fullmatch(re.escape(image) + r":[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}", tag)
                   for tag in tags)):
        raise ValueError("Image tags must belong to the built image and include its SHA tag.")
    platforms = sorted(set(env.get("PLATFORMS", "").split(",")))
    if not all(re.fullmatch(r"linux/[a-z0-9_]+(?:/[a-z0-9_]+)?", p) for p in platforms):
        raise ValueError("Explicit Linux build platforms are required.")
    return {
        "repository": repository, "commit": sha, "run_id": run_id,
        "build_attempt": attempt, "tag": f"build-{run_id}-{attempt}",
        "image": image, "digest": digest, "tags": tags, "platforms": platforms,
    }


def metadata_header(build):
    base = f"https://github.com/{build['repository']}"
    tags = ", ".join(f"`{tag}`" for tag in build["tags"])
    platforms = ", ".join(f"`{platform}`" for platform in build["platforms"])
    return (
        f"{MARKER}\n"
        "## Published main image — build traceability only\n\n"
        f"- Build tag: `{build['tag']}`\n"
        f"- Exact source / merge commit: [{build['commit']}]({base}/commit/{build['commit']})\n"
        f"- Build workflow: [run {build['run_id']}, attempt {build['build_attempt']}]"
        f"({base}/actions/runs/{build['run_id']}/attempts/{build['build_attempt']})\n"
        f"- Manifest-list digest: `{build['digest']}`\n"
        f"- Image tags at publication (mutable): {tags}\n"
        f"- Architectures: {platforms}\n\n"
        "Pull the exact published image, independent of later tag changes:\n\n"
        f"```bash\ndocker pull {build['image']}@{build['digest']}\n```\n\n"
        "This record is **not** signing, SBOM, smoke-test, or deployment qualification. "
        "Those checks are separate; production `v*.*.*` releases retain their own gates.\n\n"
    )


def tag_commit(api, tag):
    reference = optional(api, f"git/ref/tags/{tag}")
    if reference is None:
        return None
    obj = reference["object"]
    for _ in range(10):
        if obj["type"] == "commit" and SHA.fullmatch(obj["sha"]):
            return obj["sha"]
        if obj["type"] != "tag" or not SHA.fullmatch(obj["sha"]):
            break
        obj = api.request(f"git/tags/{obj['sha']}")["object"]
    raise ValueError(f"Tag {tag} does not resolve to a commit.")


def verify_tag(api, tag, sha, *, allow_missing=False):
    actual = tag_commit(api, tag)
    if actual != sha and not (allow_missing and actual is None):
        raise ValueError(f"Tag {tag} is missing or targets another commit; refusing to retag.")
    return actual


def verify_release(release, build):
    if (release.get("tag_name") != build["tag"]
            or release.get("target_commitish") != build["commit"]
            or release.get("draft") is not False
            or release.get("prerelease") is not True
            or not (release.get("body") or "").startswith(metadata_header(build))):
        raise ValueError(
            f"Release {build['tag']} conflicts with this build; refusing to overwrite it."
        )


def change_notes(api, build):
    releases = api.request("releases?per_page=100", paginate=True)
    candidates = [
        release for release in releases
        if BUILD_TAG.fullmatch(release.get("tag_name", ""))
        and release.get("tag_name") != build["tag"]
        and not release.get("draft") and release.get("prerelease")
        and (release.get("body") or "").startswith(MARKER + "\n")
    ]
    candidates.sort(key=lambda r: (r["published_at"], r["id"]), reverse=True)
    base = f"https://github.com/{build['repository']}"
    for previous in candidates:
        sha = previous.get("target_commitish", "")
        if not SHA.fullmatch(sha):
            raise ValueError("A previous build record does not identify an exact source commit.")
        comparison = api.request(f"compare/{sha}...{build['commit']}?per_page=1")
        if comparison["status"] not in ("ahead", "identical"):
            # Builds may finish out of order; never use a newer/divergent commit as baseline.
            continue
        tag = previous["tag_name"]
        verify_tag(api, tag, sha)
        previous_link = f"[{tag}]({base}/releases/tag/{tag})"
        if comparison["status"] == "identical":
            return (
                f"## Included PRs\n\nNo source changes or newly included PRs since "
                f"{previous_link}; this is another image build of the same commit.\n"
            )
        notes = api.request("releases/generate-notes", data={
            "tag_name": build["tag"], "target_commitish": build["commit"],
            "previous_tag_name": tag,
        })
        return (
            f"## Included PRs\n\nGitHub-generated changes since {previous_link}.\n"
            f"[Exact commit comparison]({base}/compare/{sha}...{build['commit']})\n\n"
            + (notes["body"].strip() or "No PR entries generated; see the commit comparison.")
            + "\n"
        )
    pulls = api.request(f"commits/{build['commit']}/pulls?per_page=100", paginate=True)
    numbers = sorted({
        pull["number"] for pull in pulls
        if pull.get("merged_at")
        and pull.get("merge_commit_sha") == build["commit"]
        and pull.get("base", {}).get("ref") == "main"
        and pull.get("base", {}).get("repo", {}).get("full_name") == build["repository"]
    })
    links = "\n".join(f"- [#{number}]({base}/pull/{number})" for number in numbers)
    return (
        "## Included PRs\n\nNo preceding published build record is available in this "
        "commit's ancestry. Only merged main PRs associated with the exact source "
        "commit are listed; this is **not** a complete historical PR inventory.\n\n"
        + (links or "No merged main PR is associated with the source commit (possibly a direct push).")
        + "\n"
    )


def record_build(api, build, *, publish=False):
    tag = build["tag"]
    actual = verify_tag(api, tag, build["commit"], allow_missing=True)
    existing = optional(api, f"releases/tags/{tag}")
    if existing is not None:
        verify_release(existing, build)
        if actual is None:
            raise ValueError(f"Release {tag} has no matching tag; refusing to recreate history.")
        return existing
    payload = {
        "tag_name": tag, "target_commitish": build["commit"],
        "name": f"Main build {build['run_id']}, attempt {build['build_attempt']} ({build['commit'][:12]})",
        "body": metadata_header(build) + change_notes(api, build),
        "draft": False, "prerelease": True, "make_latest": "false",
    }
    if not publish:
        return payload
    try:
        release = api.request("releases", data=payload)
    except ApiError as error:
        # Another invocation may have published this exact record after the preflight.
        if error.status != 422:
            raise
        release = optional(api, f"releases/tags/{tag}")
        if release is None:
            raise error
    verify_release(release, build)
    verify_tag(api, tag, build["commit"])
    return release


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", action="store_true",
                        help="Create the release; otherwise only print the API payload.")
    args = parser.parse_args()
    try:
        if args.publish and (os.environ.get("GITHUB_ACTIONS") != "true"
                             or not os.environ.get("GH_TOKEN")):
            raise ValueError("Publishing requires the main Actions job's GITHUB_TOKEN.")
        build = build_metadata(os.environ)
        result = record_build(GitHub(build["repository"]), build, publish=args.publish)
        if args.publish:
            url = f"https://github.com/{build['repository']}/releases/tag/{build['tag']}"
            print(f"Verified build release: {url}")
            if os.environ.get("GITHUB_STEP_SUMMARY"):
                with Path(os.environ["GITHUB_STEP_SUMMARY"]).open("a") as summary:
                    summary.write(f"\nBuild traceability record: [{build['tag']}]({url})\n")
        else:
            print(json.dumps(result, indent=2))
        return 0
    except (RuntimeError, ValueError, OSError, subprocess.TimeoutExpired) as error:
        print(f"Build release failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
