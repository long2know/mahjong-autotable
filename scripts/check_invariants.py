#!/usr/bin/env python3
# Phase K Wave 9 — Apone (DevOps).
#
# Cross-file invariant guard — JwtRsaKeys binding edition.
#
# `check_signer_identity.py` codified the W7 six-file signer-
# identity invariant. This script generalises the pattern to
# OTHER cross-file lock-step bindings the W8 retro audit
# surfaced. Right now it covers one binding (JwtRsaKeys ↔ ESO
# Secret name + SSM parameter path); add bindings here as the
# W10+ audit uncovers them.
#
# Why a separate script (not `check_signer_identity.py`):
#
#   * The signer-identity guard owns its own canonical-regex
#     storage + six-file extractor set + path-confusion guards.
#     Bolting more bindings onto that file would conflate two
#     concerns.
#   * This script imports and re-runs the signer-identity check
#     so a single pre-commit hook covers both — the developer
#     never has to install two hooks.
#   * The W7 history doc references `check_signer_identity.py`
#     by name; renaming it would break onboarding docs.
#
# Each binding declared below specifies:
#
#   * `name`        — human-readable identifier
#   * `description` — what the binding protects
#   * `surfaces`    — list of (path, extractor-spec, expected-value)
#                     triples. Extractor pulls the value, normaliser
#                     decodes, and `expected-value` is the
#                     canonical literal each surface must agree on.
#   * `min_surfaces` — fails if fewer than N actual files exist
#                      (catches accidentally-deleted surfaces).
#
# Bindings audited (W9 — see `docs/signer-identity-invariant.md §6
# "Other invariants audited"`):
#
#   1. JwtRsaKeys ↔ Secret name + SSM path ↔ env-var prefix.
#      `mahjong-jwt-rsa-keys` (prod) / `mahjong-jwt-rsa-keys-staging`
#      (staging) + `/mahjong/{env}/auth/jwt/rsa-*` + `Auth__JwtRsaKeys__N`
#      MUST appear in lock-step across ESO secret manifests, helm
#      values, helm subchart defaults, and the jwt-rotation doc.
#
# Run via the `cross-file-invariants` pre-commit hook (see
# `.pre-commit-config.yaml`) or directly:
#
#     python3 scripts/check_invariants.py
#     python3 scripts/check_invariants.py --show
#
# Exit code 0 = OK, 1 = drift detected, 2 = file missing.

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable

REPO_ROOT = Path(__file__).resolve().parents[1]


# ── Extractor helpers ─────────────────────────────────────────


def _extract_first_match(pattern: str) -> Callable[[str], str | None]:
    """Build an extractor that returns the FIRST regex match against
    the file body. Returns the capturing group's content, stripped.
    """

    rx = re.compile(pattern, re.MULTILINE)

    def extract(text: str) -> str | None:
        m = rx.search(text)
        return m.group(1).strip() if m else None

    return extract


def _count_matches(pattern: str) -> Callable[[str], int]:
    """Build a function that returns the NUMBER of regex matches in
    the file. Useful for asserting a token appears at least N times
    (covers all three slots of an array binding)."""

    rx = re.compile(pattern, re.MULTILINE)

    def count(text: str) -> int:
        return len(rx.findall(text))

    return count


# ── Binding declarations ──────────────────────────────────────


@dataclass(frozen=True)
class SurfaceCheck:
    """One file + one assertion about its content."""

    path: str
    description: str
    # Either `extractor` (returns value, compared to `expected`) OR
    # `min_count_pattern` + `min_count` (counts matches, asserts >=).
    extractor: Callable[[str], str | None] | None = None
    expected: str | None = None
    min_count_pattern: str | None = None
    min_count: int = 0


@dataclass(frozen=True)
class Invariant:
    name: str
    description: str
    surfaces: tuple[SurfaceCheck, ...]
    min_surfaces: int


# Invariant #1 — JwtRsaKeys binding.
#
# The W7 RS256 fallback wires four pieces of state in lock-step:
#
#   * K8s Secret name             → `mahjong-jwt-rsa-keys` (prod),
#                                    `-staging` (staging)
#   * SSM ParameterStore path     → `/mahjong/{env}/auth/jwt/rsa-*`
#   * Helm values reference       → external-secret name in values
#   * Env-var binding             → `Auth__JwtRsaKeys__N` (mounted
#                                    in the deployment envFrom)
#
# If ANY of these drift, the ESO-rendered Secret is named one thing
# but the helm template expects another → the pod starts without
# the RS256 keys → JWT minting falls back to HS256 silently → JWKS
# served at /.well-known/jwks.json is stale → external verifiers
# fail. The W5 rehearsal incident was the analogous HS256 drift; we
# do NOT want a repeat for the RS256 path.

JWT_RSA_KEYS_BINDING = Invariant(
    name="jwt-rsa-keys-binding",
    description=(
        "JwtRsaKeys ↔ ESO Secret name + SSM path + env-var prefix "
        "lock-step across helm values, kustomize ESO manifests, the "
        "subchart default, and the rotation docs."
    ),
    min_surfaces=5,
    surfaces=(
        # Prod ESO manifest — defines the canonical Secret name and
        # SSM root.
        SurfaceCheck(
            path="infra/k8s/overlays/prod/jwt-rsa-keys-secret.yaml",
            description="prod ESO ExternalSecret materialising mahjong-jwt-rsa-keys",
            extractor=_extract_first_match(r"^\s*target:\s*\n\s*name:\s*(mahjong-jwt-rsa-keys)\s*$"),
            expected="mahjong-jwt-rsa-keys",
        ),
        SurfaceCheck(
            path="infra/k8s/overlays/prod/jwt-rsa-keys-secret.yaml",
            description="prod ESO secretKey mounts Auth__JwtRsaKeys__N (3 slots)",
            min_count_pattern=r"secretKey:\s*auth__jwtrsakeys__\d",
            min_count=3,
        ),
        SurfaceCheck(
            path="infra/k8s/overlays/prod/jwt-rsa-keys-secret.yaml",
            description="prod ESO references SSM path /mahjong/prod/auth/jwt/rsa-* (3 keys)",
            min_count_pattern=r"key:\s*/mahjong/prod/auth/jwt/rsa-(active|previous|archive)",
            min_count=3,
        ),
        # Staging ESO manifest — same shape, staging suffix.
        SurfaceCheck(
            path="infra/k8s/overlays/staging/jwt-rsa-keys-secret.yaml",
            description="staging ESO ExternalSecret materialising mahjong-jwt-rsa-keys-staging",
            extractor=_extract_first_match(
                r"^\s*target:\s*\n\s*name:\s*(mahjong-jwt-rsa-keys-staging)\s*$"
            ),
            expected="mahjong-jwt-rsa-keys-staging",
        ),
        SurfaceCheck(
            path="infra/k8s/overlays/staging/jwt-rsa-keys-secret.yaml",
            description="staging ESO references SSM path /mahjong/staging/auth/jwt/rsa-* (3 keys)",
            min_count_pattern=r"key:\s*/mahjong/staging/auth/jwt/rsa-(active|previous|archive)",
            min_count=3,
        ),
        # Helm umbrella values — must reference the prod-shaped name
        # at least once in the externalSecrets array.
        SurfaceCheck(
            path="helm/mahjong/values.yaml",
            description="umbrella helm default externalSecrets[] entry",
            min_count_pattern=r"-\s*name:\s*mahjong-jwt-rsa-keys\b",
            min_count=1,
        ),
        # Helm subchart default — same.
        SurfaceCheck(
            path="helm/mahjong/charts/mahjong-api/values.yaml",
            description="mahjong-api subchart default externalSecrets[] entry",
            min_count_pattern=r"-\s*name:\s*mahjong-jwt-rsa-keys\b",
            min_count=1,
        ),
        # Helm prod overlay — references prod name.
        SurfaceCheck(
            path="helm/mahjong/values-prod.yaml",
            description="helm prod overlay references mahjong-jwt-rsa-keys",
            min_count_pattern=r"-\s*name:\s*mahjong-jwt-rsa-keys\b",
            min_count=1,
        ),
        # Helm staging overlay — references staging suffix.
        SurfaceCheck(
            path="helm/mahjong/values-staging.yaml",
            description="helm staging overlay references mahjong-jwt-rsa-keys-staging",
            min_count_pattern=r"-\s*name:\s*mahjong-jwt-rsa-keys-staging\b",
            min_count=1,
        ),
        # Documentation must reference both names (prod + staging).
        SurfaceCheck(
            path="docs/jwt-rotation.md",
            description="rotation runbook references prod ESO Secret name",
            min_count_pattern=r"\bmahjong-jwt-rsa-keys\b(?!-staging)",
            min_count=1,
        ),
        SurfaceCheck(
            path="docs/jwt-rotation.md",
            description="rotation runbook references staging ESO Secret name",
            min_count_pattern=r"\bmahjong-jwt-rsa-keys-staging\b",
            min_count=1,
        ),
        SurfaceCheck(
            path="docs/jwt-rotation.md",
            description="rotation runbook references SSM path /mahjong/{env}/auth/jwt/rsa-*",
            min_count_pattern=r"/mahjong/(?:\{env\}|prod|staging|\$\{?ENV\}?)/auth/jwt/rsa-(active|previous|archive)",
            min_count=3,
        ),
        SurfaceCheck(
            path="docs/jwt-rotation.md",
            description="rotation runbook documents Auth__JwtRsaKeys__N env-var binding",
            min_count_pattern=r"Auth__JwtRsaKeys__\d",
            min_count=3,
        ),
    ),
)


INVARIANTS: tuple[Invariant, ...] = (JWT_RSA_KEYS_BINDING,)


# ── Checking ──────────────────────────────────────────────────


def _check_surface(surf: SurfaceCheck) -> tuple[bool, str]:
    """Returns (ok, message)."""
    p = REPO_ROOT / surf.path
    if not p.is_file():
        return False, f"FILE MISSING — {surf.path}"
    try:
        text = p.read_text(encoding="utf-8")
    except OSError as exc:  # pragma: no cover - filesystem failure
        return False, f"READ ERROR — {surf.path}: {exc}"

    if surf.extractor is not None and surf.expected is not None:
        value = surf.extractor(text)
        if value is None:
            return False, (
                f"VALUE NOT FOUND — {surf.path} ({surf.description})"
            )
        if value != surf.expected:
            return False, (
                f"DRIFT — {surf.path} ({surf.description})\n"
                f"      expected: {surf.expected!r}\n"
                f"      found:    {value!r}"
            )
        return True, f"{surf.description}: {value!r}"

    if surf.min_count_pattern is not None:
        n = len(re.findall(surf.min_count_pattern, text, re.MULTILINE))
        if n < surf.min_count:
            return False, (
                f"COUNT SHORT — {surf.path} ({surf.description})\n"
                f"      expected ≥ {surf.min_count} match(es) of /{surf.min_count_pattern}/\n"
                f"      found {n}"
            )
        return True, f"{surf.description}: {n} match(es)"

    # No assertion configured → file existence alone is the check.
    return True, f"{surf.description}: present"


def _check_invariant(inv: Invariant, show: bool) -> int:
    """Returns 0 = OK, 1 = drift, 2 = file missing."""
    print(f"\n=== {inv.name} ===")
    print(f"    {inv.description}\n")
    rc = 0
    present_paths = set()
    for surf in inv.surfaces:
        ok, message = _check_surface(surf)
        symbol = "✓" if ok else "✗"
        if ok:
            present_paths.add(surf.path)
            if show:
                print(f"  {symbol} {surf.path}: {message}")
            else:
                print(f"  {symbol} {surf.path}")
        else:
            print(f"  {symbol} {message}")
            rc = max(rc, 2 if message.startswith("FILE MISSING") else 1)

    if len(present_paths) < inv.min_surfaces:
        print(
            f"  ✗ MIN-SURFACES — only {len(present_paths)} distinct file(s) present; "
            f"invariant requires ≥ {inv.min_surfaces}."
        )
        rc = max(rc, 1)
    return rc


def _run_signer_identity_check() -> int:
    """Re-run check_signer_identity.py as a sub-invocation so a
    single pre-commit hook covers both invariant scripts."""
    script = REPO_ROOT / "scripts" / "check_signer_identity.py"
    print("=== signer-identity (delegated to check_signer_identity.py) ===")
    result = subprocess.run(
        [sys.executable, str(script)],
        cwd=str(REPO_ROOT),
        check=False,
    )
    return result.returncode


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Verify cross-file lock-step invariants. Wraps "
            "check_signer_identity.py + adds W9 audit bindings "
            "(see docs/signer-identity-invariant.md §6)."
        ),
    )
    parser.add_argument(
        "--show",
        action="store_true",
        help="Print the value / match-count extracted from each surface.",
    )
    parser.add_argument(
        "--skip-signer-identity",
        action="store_true",
        help="Skip the delegated check_signer_identity.py invocation "
        "(useful when running both hooks independently from pre-commit).",
    )
    # pre-commit passes the staged file paths as positional args; we
    # ignore them (this hook always checks the full surface set).
    parser.add_argument("files", nargs="*", help=argparse.SUPPRESS)
    args = parser.parse_args(argv)

    rc = 0
    if not args.skip_signer_identity:
        rc = max(rc, _run_signer_identity_check())

    for inv in INVARIANTS:
        rc = max(rc, _check_invariant(inv, args.show))

    if rc == 0:
        print("\nAll invariants pass.")
    else:
        print(
            "\nInvariant drift detected.\n"
            "  * signer-identity: see docs/signer-identity-invariant.md §4 rotation procedure.\n"
            "  * jwt-rsa-keys-binding: see docs/jwt-rotation.md + docs/signer-identity-invariant.md §6."
        )
    return rc


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
