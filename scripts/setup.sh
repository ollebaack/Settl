#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

missing=0
if ! command -v node >/dev/null 2>&1; then
  echo 'Setup requires Node.js 22.12+ (or a newer supported release).' >&2
  missing=1
elif ! node -e 'const [major, minor] = process.versions.node.split(".").map(Number); process.exit(major > 22 || (major === 22 && minor >= 12) ? 0 : 1)'; then
  echo 'Setup requires Node.js 22.12+ (or a newer supported release).' >&2
  missing=1
fi
if ! command -v pnpm >/dev/null 2>&1; then
  echo 'Setup requires pnpm 10.18.3: npm install --global pnpm@10.18.3' >&2
  missing=1
fi
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | grep -q '^10\.'; then
  echo 'Setup requires the .NET 10 SDK; the runtime alone is insufficient.' >&2
  missing=1
else
  if ! dotnet --list-runtimes | grep -q '^Microsoft.AspNetCore.App 10\.'; then
    echo 'Setup requires the ASP.NET Core 10 runtime.' >&2
    missing=1
  fi
  # Arch packages the ASP.NET reference assemblies separately from the SDK.
  if command -v pacman >/dev/null 2>&1 && pacman -Q dotnet-sdk-10.0 >/dev/null 2>&1; then
    if ! pacman -Q aspnet-targeting-pack-10.0 >/dev/null 2>&1; then
      echo 'Setup requires the ASP.NET Core 10 targeting pack.' >&2
      missing=1
    fi
  fi
fi
if (( missing )); then
  if command -v omarchy >/dev/null 2>&1; then
    echo 'On Omarchy, install .NET prerequisites with:' >&2
    echo '  omarchy pkg add dotnet-sdk-10.0 aspnet-runtime-10.0 aspnet-targeting-pack-10.0' >&2
  fi
  exit 1
fi

pnpm install --frozen-lockfile
dotnet tool restore
dotnet restore Settl.slnx

echo 'Setup complete. Start Docker or Podman, then run pnpm dev.'
