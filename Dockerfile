# syntax=docker/dockerfile:1.7
#
# Mahjong Autotable — single-image deployment.
#
# Stage 1 — frontend bundle (Node 20 Alpine + Parcel)
# Stage 2 — backend build (.NET 10 SDK)
# Stage 3 — runtime (.NET 10 ASP.NET) hosting the published API + bundled
#           autotable assets. SQLite database lives on a /data volume.
#
# Phase J Wave 3 — Apone (DevOps).

############################
# Stage 1 — frontend build #
############################
FROM node:20-alpine AS frontend-build
WORKDIR /src/frontend/autotable-src

# Install dependencies first so the layer caches when source changes only.
COPY src/frontend/autotable-src/package.json src/frontend/autotable-src/package-lock.json ./
RUN npm ci --no-audit --no-fund

# Copy the rest of the bundle source and produce the static assets at /out/autotable.
# `--public-url .` keeps every emitted asset URL relative so the bundle works
# when served under /autotable/ (build invariant documented in decisions.md).
COPY src/frontend/autotable-src/ ./
RUN npx parcel build index.html \
    --dist-dir /out/autotable \
    --public-url . \
    --no-source-maps \
    --no-cache

############################
# Stage 2 — backend build  #
############################
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

# Bring in the whole backend tree so the .slnx + .csproj references resolve
# without hand-curated file lists. Tests are excluded via .dockerignore, so
# the restore stays scoped to the Api project.
COPY src/backend/ ./backend/

RUN dotnet restore backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj
RUN dotnet publish backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj \
    -c Release \
    -o /out/api \
    --no-restore \
    /p:UseAppHost=false

############################
# Stage 3 — runtime        #
############################
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# `curl` powers the HEALTHCHECK directive. tini gives PID 1 a proper signal
# handler so `docker stop` shuts the dotnet host down cleanly.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl tini \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=backend-build /out/api/ ./

# Program.cs resolves the autotable bundle at:
#   Path.GetFullPath(Path.Combine(ContentRootPath, "../../../frontend/autotable"))
# With WORKDIR=/app the path collapses to /frontend/autotable, so we drop the
# Parcel output exactly there. Keeping the source-tree layout means the .glb /
# .gltf MIME-type extensions registered in Program.cs are exercised on every
# request — no backend change required.
COPY --from=frontend-build /out/autotable/ /frontend/autotable/

# SQLite database lives on a writable named volume. Connection string is
# overridden via environment so the EF Core path is absolute (the in-repo
# default `data/mahjong-autotable.db` is relative to ContentRootPath).
RUN mkdir -p /data && chmod 777 /data

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    ConnectionStrings__Sqlite="Data Source=/data/mahjong-autotable.db" \
    Persistence__Provider=Sqlite \
    BUILD_SHA=""

EXPOSE 8080
VOLUME ["/data"]

# Healthcheck hits Bishop's canonical `/health` endpoint (Phase J Wave 3
# Task 3, commit 9235859 — `feat(api): add /health endpoint for Docker
# HEALTHCHECK + Linux deploy`). Falls back to the long-standing
# `/api/health` short-form probe (used by the frontend) so older builds
# or rollbacks still report healthy.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/health \
      || curl -fsS http://127.0.0.1:8080/api/health \
      || exit 1

ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "Mahjong.Autotable.Api.dll"]
