# syntax=docker/dockerfile:1
#
# Build context is the monorepo root (see ADR-0009): the web SPA is a pnpm workspace
# member (packages/api-client is workspace:*), so it needs the root lockfile/workspace
# file and packages/api-client alongside apps/web to install and build.
#
#   docker build -f apps/api/Settl.Api/Dockerfile .

# ---- Web SPA build ----
FROM node:24-alpine AS web-build
WORKDIR /repo
RUN corepack enable

COPY pnpm-workspace.yaml package.json pnpm-lock.yaml ./
COPY apps/web/package.json apps/web/package.json
COPY packages/api-client/package.json packages/api-client/package.json
RUN pnpm install --frozen-lockfile

COPY apps/web apps/web
COPY packages/api-client packages/api-client
# Empty, not unset: apps/web/src/lib/api.ts falls back to http://localhost:5000 when
# VITE_API_BASE_URL is undefined (dev convenience, talking to the API dev server on a
# different port than Vite's). In this single-container build the SPA and API share an
# origin (ADR-0009), so requests must stay relative — an explicit empty string, since ??
# only falls through on null/undefined, not "".
ENV VITE_API_BASE_URL=""
RUN pnpm --filter web build

# ---- API build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src

COPY apps/api/Settl.Api/Settl.Api.csproj apps/api/Settl.Api/Settl.Api.csproj
RUN dotnet restore apps/api/Settl.Api/Settl.Api.csproj

COPY apps/api/Settl.Api apps/api/Settl.Api
RUN dotnet publish apps/api/Settl.Api/Settl.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=api-build /app/publish .
COPY --from=web-build /repo/apps/web/dist ./wwwroot

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Settl.Api.dll"]
