# Squad Decisions

## Active Decisions

### 2026-04-20: Project baseline and rules direction
**By:** Stephen Long (with Squad)
**What:** Use `pwmarcz/autotable` as the base framework and prioritize Changsha Mahjong support first, while keeping an expansion path for broader Chinese rules.
**Why:** Reusing an existing gameplay/table baseline reduces startup risk and keeps early delivery focused on rule adaptation and quality.

### 2026-04-20: Backend and persistence strategy
**By:** Stephen Long (with Squad)
**What:** Build the backend on .NET 10 with Entity Framework Core and SQLite initially, while keeping the persistence layer provider-flexible for PostgreSQL or SQL Server later.
**Why:** SQLite keeps local development friction low now and preserves a clean migration path to container-backed production databases.

### 2026-04-20: Developer workflow and deployment target
**By:** Stephen Long (with Squad)
**What:** Local development should run backend + frontend from VS Code with F5, and deployment should package both layers in a single Docker image for Linux hosting.
**Why:** This aligns daily dev ergonomics with the intended self-hosted production runtime.

### 2026-04-20: Frontend modernization approach
**By:** Stephen Long (with Squad)
**What:** Keep autotable's existing frontend as the immediate baseline and introduce React + Fluent UI 9 + TypeScript + Vite incrementally only where the migration cost is justified.
**Why:** This avoids slowing initial Changsha delivery while still enabling gradual UI modernization.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
