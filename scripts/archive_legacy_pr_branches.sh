#!/usr/bin/env bash
set -euo pipefail

# Archives unique tip commits for legacy pr/* branches with annotated tags,
# then optionally deletes remotes once stakeholder approval is confirmed.
#
# Default mode is DRY-RUN (prints commands only).
# Use --apply to execute.

MODE="dry-run"
if [[ "${1:-}" == "--apply" ]]; then
  MODE="apply"
fi

DATE_TAG="${DATE_TAG:-2026-03-06}"
REMOTE="${REMOTE:-origin}"

branches=(
  "pr/01-scaffold-solution:defa50f"
  "pr/02-domain-rules-core:ed6f10a"
  "pr/07-tests-docs:46ce3de"
)

run() {
  if [[ "$MODE" == "apply" ]]; then
    echo "+ $*"
    "$@"
  else
    echo "$*"
  fi
}

echo "Mode: $MODE"
echo "Remote: $REMOTE"
echo "Date tag: $DATE_TAG"

run git fetch --all --prune

for entry in "${branches[@]}"; do
  branch="${entry%%:*}"
  sha="${entry##*:}"
  slug="${branch//\//-}"
  tag="archive/${slug}/${DATE_TAG}"

  run git rev-list --left-right --count "${REMOTE}/main...${REMOTE}/${branch}"
  run git cat-file -t "$sha"
  run git tag -a "$tag" "$sha" -m "Archive unique legacy branch tip for ${branch}"
  run git push "$REMOTE" "$tag"

  # Branch deletion is intentionally explicit and grouped after tag push.
  run git push "$REMOTE" --delete "$branch"
done

echo
if [[ "$MODE" == "apply" ]]; then
  echo "Done. Re-verify with: git fetch --all --prune && git branch -r | grep 'origin/pr/'"
else
  echo "Dry-run complete. Re-run with --apply after stakeholder approval."
fi
