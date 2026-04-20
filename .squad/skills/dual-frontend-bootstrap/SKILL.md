# Skill: Dual Frontend Bootstrap for Incremental Modernization

## Use when
You need to keep a legacy/baseline frontend active while introducing a modern React/Vite stack without forcing a rewrite.

## Pattern
1. Reserve a stable baseline directory (e.g., `src/frontend/autotable`) for existing assets.
2. Add a separate modern shell (e.g., `src/frontend/modern`) with independent npm scripts and proxy to backend APIs.
3. Keep backend static hosting default-compatible with baseline assets.
4. Provide separate Docker targets so deployment can choose baseline or modern overlay.
5. Add VS Code launch compound for full-stack modern dev and a backend-only fallback for baseline.

## Why it works
It minimizes delivery risk, preserves current behavior, and allows gradual modernization behind explicit operational choices.
