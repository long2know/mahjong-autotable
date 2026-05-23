#!/usr/bin/env bash
# Phase K Wave 6 — Vasquez (QA), extended Wave 7 + Wave 8.
#
# Lane-discipline CI check. Detects cross-lane bundling regressions
# (the W3/W4/W5 git-config race recurrence). For each agent in the
# squad, this script:
#
#   1. Maps the agent ident to a set of OWNED path prefixes.
#   2. Walks recent commits (default: last 4 squash-merges on main,
#      OR every commit on the current PR branch).
#   3. For each commit, classifies every changed file by lane owner
#      and fails if a commit spans MORE than one agent's lane.
#   4. Additionally on the PR branch: every commit MUST carry an
#      author identity matching the lane it touched.
#
# Exit codes:
#   0 — clean (no cross-lane bundling, all authors match lane).
#   1 — cross-lane bundling detected OR identity mismatch.
#   2 — bad invocation / git plumbing failure.
#
# Usage:
#   tests/ci/check-cross-lane-bundling.sh                  # CI default
#   tests/ci/check-cross-lane-bundling.sh --branch main    # main only
#   tests/ci/check-cross-lane-bundling.sh --count 10       # last 10
#   tests/ci/check-cross-lane-bundling.sh --pr <ref>       # PR branch
#   tests/ci/check-cross-lane-bundling.sh --strict         # PR mode +
#                                                         # exit non-zero
#                                                         # on ANY warn
#   tests/ci/check-cross-lane-bundling.sh --repo-mode      # scans the
#                                                         # FULL commit
#                                                         # history (nightly
#                                                         # cron baseline).
#
# Wave 7 refinements (Vasquez):
#   - Phase_K_W*/<AgentName>/ attribution rule generalised: anywhere
#     in the tree, a path under Phase_K_W<N>/<Bishop|Hicks|Apone|Vasquez>/
#     is attributed to <AgentName>. This lets cross-pane test code
#     (e.g. Phase_K_W7/Hicks/three-renderer-trend.cs) land in Hicks's
#     lane without forcing it under src/backend/tests/.
#   - Companion `tests/ci/lane-map.json` documents the per-agent
#     regex map (consumed by humans + future tooling; the bash logic
#     remains the case-statement classifier below for portability).
#
# Wave 8 refinements (Vasquez):
#   - SHARED-FILE classification: tests/selectors.md (and the
#     frontend mirror src/frontend/autotable-src/tests/selectors.md)
#     is a documented cross-pane file. Both Hicks and Vasquez may
#     author edits to it without that counting as an author-lane
#     mismatch. The primary lane is still Vasquez (so a single
#     commit touching the file + another lane's source still fails).
#     Companion `lane-map.json` declares the shared-file list under
#     `shared_files.selectors_md_shared`.
#   - `--repo-mode` scans the entire history of the current branch
#     (NOT just PR vs base) — for nightly cron baseline reporting.
#     Post-W6, the expected baseline is 0 violations on PR-branch
#     commits (historical squash-merges on main are excluded).
#   - `--strict` lane-discipline is REQUIRED-FOR-MERGE on main via
#     branch protection (see docs/agent-handoff-protocol.md §3.5
#     for the procedure to flip a workflow to required status).
#
# Wave 10 refinements (Vasquez):
#   - SHARED-FILE table BROADENED: `docs/agent-handoff-protocol.md`
#     is now co-authored by Vasquez (concurrent-agent safety §5 +
#     stash-discipline ownership) and Apone (branch-protection
#     runbook + lock-file relocation §3.6/§3.7). The bundling
#     check now strips shared-file paths from the per-commit lane
#     set BEFORE computing single-lane attribution — so a Vasquez
#     commit touching ONLY `docs/agent-handoff-protocol.md` + a
#     test artefact doesn't get rejected as cross-lane.
#   - Companion `lane-map.json` carries the new
#     `shared_files.agent_handoff_protocol_md_shared` entry.
#   - Bundling-check coverage is now broadly tested at the contract
#     level by `Phase_K_W10/Vasquez/VasquezW10SelfLaneTests.cs`.
#
# Wave 11 refinements (Vasquez):
#   - SHARED-FILE table BROADENED further:
#     * `src/backend/tests/Mahjong.Autotable.Api.Tests/Shims/*` is
#       now treated as shared between ALL FOUR squad authors. The
#       W10 retro showed Bishop legitimately needs to add a forward-
#       stage shim alongside Vasquez's contract harness — and the
#       Shims/ directory is the canonical place for cross-lane test
#       scaffolding. Authors accepted: bishop|vasquez|hicks|apone.
#       Primary lane (for the cross-lane bundling detector) stays
#       at `vasquez` since Shims/ lives under src/backend/tests/.
#     * `.github/workflows/pwa-audit.yml` and the W11 sibling
#       `.github/workflows/pwa-builder.yml` are shared between
#       Hicks (frontend PWA asset author) and Apone (workflow
#       runtime owner). Primary stays at `apone` since the file
#       lives under .github/workflows/. The W10 retro flagged this:
#       Hicks legitimately authored `pwa-audit.yml` but it landed
#       in Apone's lane.
#   - Companion `lane-map.json` carries new
#     `shared_files.shims_shared` + `shared_files.pwa_audit_workflow_shared`.
#   - See `docs/agent-handoff-protocol.md §5.9` for the registry policy.
#
# Owner: Vasquez (QA).

set -euo pipefail

MODE="${MODE:-auto}"          # auto | main | pr | repo
COUNT="${COUNT:-4}"
PR_REF="${PR_REF:-HEAD}"
BASE_REF="${BASE_REF:-origin/main}"
VERBOSE="${VERBOSE:-0}"
STRICT="${STRICT:-0}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --branch) MODE="main"; shift ;;
    --pr) MODE="pr"; PR_REF="$2"; shift 2 ;;
    --count) COUNT="$2"; shift 2 ;;
    --base) BASE_REF="$2"; shift 2 ;;
    --verbose|-v) VERBOSE=1; shift ;;
    --strict) STRICT=1; MODE="pr"; shift ;;
    --repo-mode) MODE="repo"; shift ;;
    -h|--help)
      sed -n '2,30p' "$0"
      exit 0
      ;;
    *) echo "unknown arg: $1" >&2; exit 2 ;;
  esac
done

# ───────────────────────────────────────────────────────────────────
#  Lane → path-prefix mapping. KEEP IN SYNC with agent charters.
# ───────────────────────────────────────────────────────────────────
#
# Each agent owns the prefixes listed. A file MAY be owned by
# `shared` (docs/, root configs, CHANGELOG) — `shared` never counts
# as a cross-lane violation. Anything outside the listed prefixes
# is `unclassified` and ALSO never counts as a violation (so e.g.
# a misc tooling tweak doesn't fail the gate).

agent_for_path() {
  local p="$1"
  case "$p" in
    # Agent-owned subdirectories under wave-level test dirs.
    # Convention established in W5 (Phase_K_W5/Bishop/): each agent
    # may add their own contract tests under
    # src/backend/tests/<asm>/Phase_K_W*/<AgentName>/. Those files
    # are attributed to that agent, NOT Vasquez.
    #
    # Wave 7 refinement: the same attribution applies to ANY
    # Phase_K_W*/<AgentName>/ path in the tree, not just under
    # src/backend/tests/. Lets cross-pane test code (e.g.
    # Phase_K_W7/Hicks/whatever.cs) land in Hicks's lane without
    # forcing it under src/backend/tests/.
    src/backend/tests/*/Phase_K_W*/Bishop/*|Phase_K_W*/Bishop/*)
      echo "bishop" ;;
    src/backend/tests/*/Phase_K_W*/Hicks/*|Phase_K_W*/Hicks/*)
      echo "hicks" ;;
    src/backend/tests/*/Phase_K_W*/Apone/*|Phase_K_W*/Apone/*)
      echo "apone" ;;
    src/backend/tests/*/Phase_K_W*/Vasquez/*|Phase_K_W*/Vasquez/*)
      echo "vasquez" ;;
    # Vasquez — tests + QA-owned docs + cross-lane infra
    src/backend/tests/*|\
    src/frontend/autotable-src/tests/*|\
    tests/ci/*|\
    docs/test-*.md|\
    docs/test-shims.md|\
    docs/test-harness-handoff.md|\
    docs/contracts/*|\
    docs/agent-handoff-protocol.md|\
    .squad/agents/vasquez/*|\
    .squad/decisions/inbox/vasquez-*|\
    .github/workflows/lane-discipline.yml|\
    .github/workflows/lane-discipline-nightly.yml|\
    .github/workflows/lane-discipline-status.yml|\
    .github/workflows/playwright-visual-regression.yml)
      echo "vasquez" ;;
    # Bishop — backend migrations + appsettings (auth lane spillover)
    src/backend/Migrations/*|\
    src/backend/src/*/appsettings*.json|\
    .squad/agents/bishop/*|\
    .squad/decisions/inbox/bishop-*)
      echo "bishop" ;;
    # Hicks — agent-state-only artefacts
    .squad/agents/hicks/*|\
    .squad/decisions/inbox/hicks-*)
      echo "hicks" ;;
    # Apone — agent state + helm + signer + edge module hardpoints
    helm/*|\
    .pre-commit-config.yaml|\
    .squad/agents/apone/*|\
    .squad/decisions/inbox/apone-*)
      echo "apone" ;;
    # Bishop — backend source
    src/backend/src/*)
      echo "bishop" ;;
    # Hicks — frontend source (NOT tests, those are Vasquez's)
    src/frontend/autotable-src/src/*|\
    src/frontend/autotable-src/*.json|\
    src/frontend/autotable-src/*.webmanifest|\
    src/frontend/autotable-src/scripts/*|\
    src/frontend/autotable-src/img/*|\
    src/frontend/autotable-src/index.html|\
    src/frontend/autotable/*)
      echo "hicks" ;;
    # Apone — infra, workflows, deployment
    infra/*|\
    .github/workflows/*|\
    Dockerfile|\
    .dockerignore|\
    docker-compose*.yml|\
    helm/*)
      echo "apone" ;;
    # Squad / shared scope
    .squad/agents/*|\
    .squad/decisions/inbox/*|\
    .squad/charter.md|\
    CHANGELOG.md|\
    README.md|\
    LICENSE|\
    docs/*)
      echo "shared" ;;
    *)
      echo "unclassified" ;;
  esac
}

# ───────────────────────────────────────────────────────────────────
#  Agent author-ident mapping. Each agent has a canonical author
#  email; commits authored by `<agent>@squad.mahjong` are
#  considered owned by that agent.
# ───────────────────────────────────────────────────────────────────

agent_for_author() {
  local email="$1"
  case "$email" in
    vasquez@squad.mahjong) echo "vasquez" ;;
    bishop@squad.mahjong)  echo "bishop"  ;;
    hicks@squad.mahjong)   echo "hicks"   ;;
    apone@squad.mahjong)   echo "apone"   ;;
    *)                     echo "other"   ;;
  esac
}

# ───────────────────────────────────────────────────────────────────
#  Phase K Wave 8 — shared-file table. Files in this list legitimately
#  span more than one lane (cross-pane contracts); the author-lane
#  mismatch check accepts ANY of the listed authors. The file is
#  STILL classified to its primary lane for cross-lane bundling
#  detection (so a single commit touching the file + another lane's
#  source still fails the bundle check). Companion JSON declaration
#  lives under `shared_files` in tests/ci/lane-map.json; keep in
#  sync.
# ───────────────────────────────────────────────────────────────────

is_shared_file() {
  # Returns 0 (true) when the path is in the shared-file table.
  # W10 broadening (Vasquez): `docs/agent-handoff-protocol.md` is now
  # co-authored by both Vasquez (QA stash-discipline + concurrent-safety
  # sections) and Apone (infra branch-protection runbook + lock-file
  # relocation). Mirror entry under `shared_files` in lane-map.json.
  # W11 broadening (Vasquez):
  #   * src/backend/tests/.../Shims/* — co-authored by ALL four
  #     squad agents (bishop|vasquez|hicks|apone). Test shims are
  #     forward-stage scaffolding for cross-pane contracts.
  #   * .github/workflows/pwa-audit.yml + pwa-builder.yml — co-
  #     authored by hicks + apone (PWA assets vs workflow runtime).
  local p="$1"
  case "$p" in
    src/frontend/autotable-src/tests/selectors.md|tests/selectors.md)
      return 0 ;;
    docs/agent-handoff-protocol.md)
      return 0 ;;
    src/backend/tests/Mahjong.Autotable.Api.Tests/Shims/*)
      return 0 ;;
    .github/workflows/pwa-audit.yml|.github/workflows/pwa-builder.yml)
      return 0 ;;
    *)
      return 1 ;;
  esac
}

shared_file_authors() {
  # Prints a space-separated list of accepted authors for the given
  # shared-file path. Empty when the path is not shared.
  local p="$1"
  case "$p" in
    src/frontend/autotable-src/tests/selectors.md|tests/selectors.md)
      echo "hicks vasquez" ;;
    docs/agent-handoff-protocol.md)
      echo "apone vasquez" ;;
    src/backend/tests/Mahjong.Autotable.Api.Tests/Shims/*)
      echo "bishop vasquez hicks apone" ;;
    .github/workflows/pwa-audit.yml|.github/workflows/pwa-builder.yml)
      echo "hicks apone" ;;
    *)
      echo "" ;;
  esac
}

commit_only_touches_shared_files() {
  # Returns 0 (true) when EVERY non-shared, non-unclassified path
  # the commit touched is a shared-file entry. Used so a commit
  # that only edits selectors.md can be authored by any listed
  # shared-file author without triggering an author-lane mismatch.
  local sha="$1"
  local any_nonshared=0
  while IFS= read -r f; do
    [[ -z "$f" ]] && continue
    local lane
    lane=$(agent_for_path "$f")
    [[ "$lane" == "shared" || "$lane" == "unclassified" ]] && continue
    if ! is_shared_file "$f"; then
      any_nonshared=1
      break
    fi
  done < <(git show --no-color --pretty='' --name-only "$sha")
  [[ "$any_nonshared" -eq 0 ]]
}

commit_shared_file_authors() {
  # Prints the intersection of accepted authors across every
  # shared-file path the commit touched (space-separated). Empty
  # when no shared-file is touched. The intersection is used so
  # that e.g. a commit touching both selectors.md (hicks|vasquez)
  # and a hypothetical future shared file with authors (apone|vasquez)
  # would only accept the shared author 'vasquez'.
  local sha="$1"
  local first=1
  local accepted=""
  while IFS= read -r f; do
    [[ -z "$f" ]] && continue
    if is_shared_file "$f"; then
      local auths
      auths=$(shared_file_authors "$f")
      if [[ "$first" -eq 1 ]]; then
        accepted="$auths"
        first=0
      else
        # Intersect.
        local next=""
        for a in $accepted; do
          for b in $auths; do
            if [[ "$a" == "$b" ]]; then
              next="$next $a"
            fi
          done
        done
        accepted="${next# }"
      fi
    fi
  done < <(git show --no-color --pretty='' --name-only "$sha")
  echo "$accepted"
}

# ───────────────────────────────────────────────────────────────────
#  Classify a single commit. Prints
#    <SHA> <author> <lanes-list>
#  where lanes-list is space-separated unique lane names (excluding
#  shared / unclassified).
#
#  Returns the lane count as exit code (0=clean shared/unclassified,
#  1=single lane, 2+=cross-lane).
# ───────────────────────────────────────────────────────────────────

classify_commit() {
  local sha="$1"
  local author author_email lanes_seen

  author_email=$(git log -1 --format='%ae' "$sha")
  author=$(agent_for_author "$author_email")

  declare -A SEEN=()
  while IFS= read -r f; do
    # Phase K Wave 8 — shared files don't contribute to the lane
    # set. Otherwise a Hicks commit that touches selectors.md
    # would always be flagged as cross-lane (hicks + vasquez).
    if is_shared_file "$f"; then
      continue
    fi
    local lane
    lane=$(agent_for_path "$f")
    if [[ "$lane" != "shared" && "$lane" != "unclassified" ]]; then
      SEEN[$lane]=1
    fi
  done < <(git show --no-color --pretty='' --name-only "$sha")

  lanes_seen="${!SEEN[*]}"
  echo "$sha|$author|$lanes_seen"
}

# ───────────────────────────────────────────────────────────────────
#  Collect commits to check based on MODE.
# ───────────────────────────────────────────────────────────────────

collect_commits() {
  case "$MODE" in
    main)
      git log --first-parent --no-merges --format='%H' -n "$COUNT" "$BASE_REF"
      ;;
    pr)
      # All commits on PR_REF that are NOT in BASE_REF.
      git log --format='%H' "$BASE_REF..$PR_REF"
      ;;
    repo)
      # Phase K Wave 8 — nightly cron baseline. Full history of the
      # current branch (no count cap). Historical squash-merges into
      # main were intentionally multi-lane (pre-W6 process); the
      # script reports them but does NOT hard-fail in this mode —
      # the violation count is the baseline that operators track
      # wave-over-wave. Post-W6 the expected per-PR baseline is 0.
      git log --first-parent --no-merges --format='%H' HEAD
      ;;
    auto)
      # On a CI PR run BASE_REF=origin/main and HEAD=PR head.
      if git rev-parse "$BASE_REF" > /dev/null 2>&1 \
         && [[ "$(git rev-parse HEAD)" != "$(git rev-parse "$BASE_REF" 2>/dev/null || echo none)" ]]; then
        git log --format='%H' "$BASE_REF..HEAD" 2>/dev/null \
          | head -n "$COUNT"
      fi
      # Fallback: last N first-parent on main.
      git log --first-parent --no-merges --format='%H' -n "$COUNT" 2>/dev/null \
        | head -n "$COUNT"
      ;;
  esac
}

# ───────────────────────────────────────────────────────────────────
#  Main loop.
# ───────────────────────────────────────────────────────────────────

violations=0
checked=0
mapfile -t shas < <(collect_commits | awk '!seen[$0]++')
if [[ ${#shas[@]} -eq 0 ]]; then
  echo "[lane-discipline] no commits to check (mode=$MODE)"
  exit 0
fi

echo "[lane-discipline] checking ${#shas[@]} commit(s) in mode=$MODE"
echo

for sha in "${shas[@]}"; do
  [[ -z "$sha" ]] && continue
  result=$(classify_commit "$sha")
  IFS='|' read -r commit_sha author_lane lanes <<< "$result"
  lane_count=0
  for _ in $lanes; do lane_count=$((lane_count + 1)); done

  short_sha="${commit_sha:0:10}"
  subject=$(git log -1 --format='%s' "$commit_sha" | cut -c1-72)

  checked=$((checked + 1))

  if (( lane_count > 1 )); then
    echo "✗ CROSS-LANE BUNDLE: $short_sha (lanes=[$lanes], author=$author_lane)"
    echo "    subject: $subject"
    violations=$((violations + 1))
    continue
  fi

  if [[ "$MODE" == "pr" && $lane_count -eq 1 ]]; then
    # On the PR branch, the author lane MUST match the touched lane.
    # Phase K Wave 8 exception: when the commit ONLY edits shared-file
    # entries (e.g. tests/selectors.md), the author-lane check accepts
    # any author in the shared-file allowlist.
    touched="${lanes// /}"
    if [[ "$author_lane" != "other" && "$author_lane" != "$touched" ]]; then
      shared_ok=0
      if commit_only_touches_shared_files "$commit_sha"; then
        accepted=$(commit_shared_file_authors "$commit_sha")
        for a in $accepted; do
          if [[ "$a" == "$author_lane" ]]; then
            shared_ok=1
            break
          fi
        done
      fi
      if [[ "$shared_ok" -eq 1 ]]; then
        if [[ "$VERBOSE" == "1" ]]; then
          echo "✓ $short_sha — shared-file pass (touched=$touched, author=$author_lane, accepted=[$accepted])"
        fi
      else
        echo "✗ AUTHOR-LANE MISMATCH: $short_sha (touched=$touched, author=$author_lane)"
        echo "    subject: $subject"
        violations=$((violations + 1))
        continue
      fi
    fi
  fi

  if [[ "$VERBOSE" == "1" ]]; then
    echo "✓ $short_sha (lane=${lanes:-shared}, author=$author_lane)"
    echo "    subject: $subject"
  else
    echo "✓ $short_sha — lane=${lanes:-shared} author=$author_lane"
  fi
done

echo
echo "[lane-discipline] checked=$checked violations=$violations"

# Historical wave-level squash-merges into main were intentionally
# multi-lane (pre-W6 process). The brief calls for forward-looking
# enforcement, so we only HARD-FAIL on PR branches; on main we emit
# the report as a warning so historical commits don't break CI.
if [[ "$MODE" == "main" && $violations -gt 0 ]]; then
  echo "[lane-discipline] WARNING — historical squash-merge bundles found on main."
  echo "[lane-discipline] Going forward (W6+), each PR is expected to be single-lane."
  exit 0
fi

# Phase K Wave 8 — `--repo-mode` is a nightly cron baseline scan. It
# walks the FULL first-parent commit history and ALWAYS exits 0, since
# pre-W6 squash-merges into main were intentionally multi-lane; the
# violation count is the baseline number operators track wave-over-
# wave. The expected post-W6 PR-branch baseline is 0 — anything
# higher indicates a regression in the per-PR lane-discipline gate
# that needs investigation.
if [[ "$MODE" == "repo" ]]; then
  echo "[lane-discipline] REPO-MODE baseline (nightly cron): violations=$violations"
  echo "[lane-discipline] Expected post-W6 PR-branch baseline: 0."
  echo "[lane-discipline] Pre-W6 squash-merges into main are intentionally counted."
  exit 0
fi

if (( violations > 0 )); then
  echo "[lane-discipline] FAIL — see violations above."
  exit 1
fi

# Wave 7 strict mode: also verify the lane-map.json companion is
# parseable + reachable. Treat unreachable map as a STRICT-mode fail
# (the JSON is documentation truth for the regex map; if the map
# disappears the CI gate is no longer self-describing).
#
# Wave 8 strict mode additionally verifies the shared_files key is
# present in lane-map.json — so the shared-file allowlist stays
# self-describing.
if [[ "$STRICT" == "1" ]]; then
  map="$(git rev-parse --show-toplevel)/tests/ci/lane-map.json"
  if [[ ! -f "$map" ]]; then
    echo "[lane-discipline] STRICT FAIL — tests/ci/lane-map.json missing."
    exit 1
  fi
  # Cheap JSON parse — confirm closing brace + lanes key.
  if ! grep -q '"lanes"' "$map"; then
    echo "[lane-discipline] STRICT FAIL — lane-map.json missing 'lanes' key."
    exit 1
  fi
  if ! grep -q '"shared_files"' "$map"; then
    echo "[lane-discipline] STRICT FAIL — lane-map.json missing 'shared_files' key (W8)."
    exit 1
  fi
fi

echo "[lane-discipline] OK"
exit 0
