# PR Prep: `feature/lobby-flow-api-wiring`

## Proposed PR title
`client: wire lobby flow to live game API`

## Proposed PR body
## Summary
- switch lobby create/join flow from mock wiring to live API calls
- connect the client lobby path to backend game lifecycle endpoints
- keep existing UI interaction flow while replacing local-only handoff points

## Verification
- `dotnet test --nologo`
  - Passed: 15, Failed: 0, Skipped: 0

## Notes
- Branch: `feature/lobby-flow-api-wiring`
- Local commit carrying the feature: `7e03f5f`
- Push/PR creation is currently blocked by missing GitHub auth in this environment.

## Ready-to-run commands (once auth is available)
```bash
git push -u origin feature/lobby-flow-api-wiring
gh pr create \
  --base main \
  --head feature/lobby-flow-api-wiring \
  --title "client: wire lobby flow to live game API" \
  --body-file PR_PREP_LOBBY_FLOW_API_WIRING.md
```
