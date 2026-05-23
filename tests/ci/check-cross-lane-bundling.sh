#!/usr/bin/env bash
# Phase K Wave 6 — Vasquez (QA).
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
#
# Owner: Vasquez (QA).

set -euo pipefail

MODE="${MODE:-auto}"          # auto | main | pr
COUNT="${COUNT:-4}"
PR_REF="${PR_REF:-HEAD}"
BASE_REF="${BASE_REF:-origin/main}"
VERBOSE="${VERBOSE:-0}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --branch) MODE="main"; shift ;;
    --pr) MODE="pr"; PR_REF="$2"; shift 2 ;;
    --count) COUNT="$2"; shift 2 ;;
    --base) BASE_REF="$2"; shift 2 ;;
    --verbose|-v) VERBOSE=1; shift ;;
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
    src/backend/tests/*/Phase_K_W*/Bishop/*)
      echo "bishop" ;;
    src/backend/tests/*/Phase_K_W*/Hicks/*)
      echo "hicks" ;;
    src/backend/tests/*/Phase_K_W*/Apone/*)
      echo "apone" ;;
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
    .github/workflows/lane-discipline.yml)
      echo "vasquez" ;;
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
    touched="${lanes// /}"
    if [[ "$author_lane" != "other" && "$author_lane" != "$touched" ]]; then
      echo "✗ AUTHOR-LANE MISMATCH: $short_sha (touched=$touched, author=$author_lane)"
      echo "    subject: $subject"
      violations=$((violations + 1))
      continue
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

if (( violations > 0 )); then
  echo "[lane-discipline] FAIL — see violations above."
  exit 1
fi

echo "[lane-discipline] OK"
exit 0
