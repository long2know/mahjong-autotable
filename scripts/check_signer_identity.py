#!/usr/bin/env python3
# Phase K Wave 7 — Apone (DevOps).
#
# Six-file signer-identity invariant guard.
#
# Cosign keyless signatures embed the OIDC subject of the signer
# (GitHub-Actions reusable-workflow URL). FOUR pieces of repo
# machinery verify against that subject:
#
#   1. The cosign signer workflow's `verify` step (post-sign).
#   2. The reusable verify-signature workflow's default input.
#   3. The slsa-provenance workflow's marker (W7).
#   4. The cluster-wide Kyverno cosign verifier policy.
#   5. The prod-overlay Kyverno enforce patch.
#   6. The slsa-provenance documentation §4a (W7).
#
# Each of those carries the SAME signer-identity regex. If they
# ever fall out of step, image admission stops working — a
# silent-until-prod-deploy failure mode that took down the W5
# rehearsal for ~25 minutes. THIS HOOK PREVENTS DRIFT by
# extracting the regex from each tracked file, normalising the
# escaping convention, and asserting all six match the canonical
# value declared below.
#
# Run via the `signer-identity-invariant` pre-commit hook (see
# `.pre-commit-config.yaml`) or directly:
#
#     python3 scripts/check_signer_identity.py
#     python3 scripts/check_signer_identity.py --show
#
# Exit code 0 = OK, 1 = drift detected, 2 = file missing.
#
# Rotation procedure: see `docs/signer-identity-invariant.md`.

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

REPO_ROOT = Path(__file__).resolve().parents[1]

# ── Canonical regex (single-escaped form) ─────────────────────
#
# This is the SINGLE SOURCE OF TRUTH for the W7 invariant.
# Every entry in TRACKED_FILES below MUST normalise to this
# string. Edit ONLY during a signer-identity rotation — and
# update all six files in the same commit.
CANONICAL_REGEX = (
    r"^https://github\.com/long2know/mahjong-autotable"
    r"/\.github/workflows/sign-image\.yml"
    r"@refs/(heads/main|tags/v.*)$"
)


def _normalise(raw: str) -> str:
    r"""Strip surrounding YAML/JSON quoting + decode double-escaped
    backslashes so the value can be compared against the canonical
    (single-escaped) form.

    Handles the three quoting conventions in our tracked files:

      * Unquoted YAML scalar (sign-image.yml, slsa-provenance.yml):
        backslashes appear once (``\.`` represents the regex ``\.``).
      * Double-quoted YAML scalar (verify-signature.yml, the two
        kyverno files): backslashes are doubled in source
        (``\\.`` represents the regex ``\.``).
      * Plain-text doc string (slsa-provenance.md §4a): same as the
        unquoted YAML form — single escapes.
    """
    raw = raw.strip()
    # Strip a single surrounding pair of double / single quotes.
    if len(raw) >= 2 and raw[0] in ("\"", "'") and raw[-1] == raw[0]:
        inner = raw[1:-1]
        # In a double-quoted YAML scalar, `\\.` in source means
        # `\.` in the value. Collapse double backslashes to single.
        if raw[0] == '"':
            inner = inner.replace("\\\\", "\\")
        return inner
    return raw


# ── Extraction strategies per tracked file ────────────────────
#
# Each strategy takes the file's text and returns the substring of
# the line carrying the regex (with surrounding quotes if any).
# `_normalise()` then strips quotes / decodes escapes before
# comparison.


def _extract_yaml_value_after(key: str) -> Callable[[str], str | None]:
    """Build an extractor that finds the FIRST line containing
    `<key>:` and returns everything after the colon (stripped).
    Works for both quoted and unquoted YAML scalars."""

    pattern = re.compile(rf"^\s*{re.escape(key)}\s*:\s*(.+?)\s*$", re.MULTILINE)

    def extract(text: str) -> str | None:
        m = pattern.search(text)
        return m.group(1) if m else None

    return extract


def _extract_yaml_default_under(parent_key: str) -> Callable[[str], str | None]:
    """Build an extractor that finds the `default:` line under the
    given parent key. The parent block must be indented more than
    the parent key itself."""

    parent_pattern = re.compile(
        rf"^(\s*){re.escape(parent_key)}\s*:\s*$", re.MULTILINE
    )

    def extract(text: str) -> str | None:
        pm = parent_pattern.search(text)
        if not pm:
            return None
        parent_indent = len(pm.group(1))
        block_start = pm.end()
        for line in text[block_start:].splitlines():
            stripped = line.lstrip()
            if not stripped or stripped.startswith("#"):
                continue
            indent = len(line) - len(stripped)
            if indent <= parent_indent:
                # Block ended.
                return None
            m = re.match(r"default\s*:\s*(.+?)\s*$", stripped)
            if m:
                return m.group(1)
        return None

    return extract


def _extract_doc_codeblock_after(heading: str) -> Callable[[str], str | None]:
    """Find the first ```-fenced code block after `heading` and
    return its first non-empty content line."""

    def extract(text: str) -> str | None:
        idx = text.find(heading)
        if idx == -1:
            return None
        block_start = text.find("```", idx)
        if block_start == -1:
            return None
        block_open_end = text.find("\n", block_start)
        if block_open_end == -1:
            return None
        block_close = text.find("```", block_open_end + 1)
        if block_close == -1:
            return None
        body = text[block_open_end + 1 : block_close]
        for line in body.splitlines():
            stripped = line.strip()
            if stripped:
                return stripped
        return None

    return extract


@dataclass(frozen=True)
class TrackedFile:
    path: str
    extractor: Callable[[str], str | None]
    description: str


TRACKED_FILES: tuple[TrackedFile, ...] = (
    TrackedFile(
        path=".github/workflows/sign-image.yml",
        extractor=_extract_yaml_value_after("EXPECTED_IDENTITY_REGEXP"),
        description="cosign verify step in the signer workflow",
    ),
    TrackedFile(
        path=".github/workflows/verify-signature.yml",
        extractor=_extract_yaml_default_under("expected-identity-pattern"),
        description="reusable verify-signature workflow default input",
    ),
    TrackedFile(
        path=".github/workflows/slsa-provenance.yml",
        extractor=_extract_yaml_value_after("EXPECTED_IDENTITY_REGEXP"),
        description="SLSA provenance workflow W7 marker",
    ),
    TrackedFile(
        path="infra/k8s/policies/kyverno-cosign-verify.yaml",
        extractor=_extract_yaml_value_after("subjectRegExp"),
        description="cluster-wide Kyverno cosign verifier policy",
    ),
    TrackedFile(
        path="infra/k8s/overlays/prod/kyverno-enforce-patch.yaml",
        extractor=_extract_yaml_value_after("subjectRegExp"),
        description="prod-overlay Kyverno enforce patch",
    ),
    TrackedFile(
        path="docs/slsa-provenance.md",
        extractor=_extract_doc_codeblock_after("## 4a. Signer-identity invariant"),
        description="slsa-provenance documentation §4a",
    ),
)


def _check_file(tracked: TrackedFile) -> tuple[bool, str]:
    """Returns (ok, message). `message` describes the failure or
    the extracted value (on success, for --show output)."""
    p = REPO_ROOT / tracked.path
    if not p.is_file():
        return False, f"FILE MISSING — {tracked.path}"
    try:
        text = p.read_text(encoding="utf-8")
    except OSError as exc:  # pragma: no cover - filesystem failure
        return False, f"READ ERROR — {tracked.path}: {exc}"
    raw = tracked.extractor(text)
    if raw is None:
        return False, f"REGEX NOT FOUND — {tracked.path} ({tracked.description})"
    normalised = _normalise(raw)
    if normalised != CANONICAL_REGEX:
        return False, (
            f"DRIFT — {tracked.path}\n"
            f"  expected: {CANONICAL_REGEX!r}\n"
            f"  found:    {normalised!r}\n"
            f"  raw line: {raw!r}"
        )
    return True, normalised


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(
        description="Verify the signer-identity regex is in lock-step across six files.",
    )
    parser.add_argument(
        "--show",
        action="store_true",
        help="Print the extracted regex from each tracked file (still exits non-zero on drift).",
    )
    # pre-commit passes the staged file paths as positional args; we
    # ignore them (this hook always checks the full six-file set).
    parser.add_argument("files", nargs="*", help=argparse.SUPPRESS)
    args = parser.parse_args(argv)

    rc = 0
    print(f"signer-identity-invariant — canonical:\n    {CANONICAL_REGEX}\n")
    for tracked in TRACKED_FILES:
        ok, message = _check_file(tracked)
        symbol = "✓" if ok else "✗"
        if ok:
            if args.show:
                print(f"  {symbol} {tracked.path}: {message}")
            else:
                print(f"  {symbol} {tracked.path}")
        else:
            print(f"  {symbol} {message}")
            rc = max(rc, 2 if message.startswith("FILE MISSING") else 1)
    if rc == 0:
        print("\nAll six surfaces agree.")
    else:
        print(
            "\nDrift detected. Update ALL six surfaces in a single commit;\n"
            "see docs/signer-identity-invariant.md for the rotation procedure."
        )
    return rc


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
