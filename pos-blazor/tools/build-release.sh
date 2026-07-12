#!/usr/bin/env bash
# Builds everything a deploy needs: the web app, AND the shop-PC install package it hands out.
#
# The agent no longer travels to the shop by USB stick or email — the POS serves it from
# /shkarko, and the cashier PC installs it with one command copied off the /pajisjet page. That
# only works if the package is inside the published output, which is what this script assembles:
#
#   publish/                     <- the image's /app
#     KosovaPOS.Web.dll ...
#     agent-package/
#       agjenti.zip              <- the self-contained win-x64 agent + its scripts
#       instalo.ps1              <- one-command installer (downloads agjenti.zip from the POS)
#       kiosk.ps1                <- Chrome --kiosk-printing shortcut (fallback, no agent)
#       version.txt
#
# The package is a BUILD ARTIFACT — publish/ is gitignored, and a 43MB zip has no business in
# git. Which also means: a deploy that runs `dotnet publish` on its own ships NO package, and
# /pajisjet will say so instead of offering a download that 404s. Use this script.
#
#   ./tools/build-release.sh
#   rsync -az --delete publish Dockerfile docker-compose.yml deploy <host>:~/apps/pos-blazor/
#   ssh <host> 'cd ~/apps/pos-blazor && sudo docker compose up -d --build'
set -euo pipefail

cd "$(dirname "$0")/.."
ROOT="$PWD"
DEPLOY="$ROOT/src/KosovaPOS.Agent/deploy"

# 1. The agent package (win-x64, self-contained). Also the only step that needs `zip`.
"$ROOT/tools/publish-agent.sh"

VERSION=$(grep -oPm1 '(?<=<Version>)[^<]+' "$ROOT/src/KosovaPOS.Agent/KosovaPOS.Agent.csproj")
ZIP="$ROOT/publish/agent/KosovaPOS-Agent-$VERSION.zip"
[ -f "$ZIP" ] || { echo "Agent zip not found at $ZIP" >&2; exit 1; }

# 2. The web app. This wipes publish/, so it must run AFTER the zip is built but BEFORE the
#    package is staged into it — hence the copy below, not a copy inside publish-agent.sh.
echo
echo "==> Publishing KosovaPOS.Web"
ZIP_TMP=$(mktemp -d)
cp "$ZIP" "$ZIP_TMP/agjenti.zip"

dotnet publish "$ROOT/src/KosovaPOS.Web/KosovaPOS.Web.csproj" -c Release -o "$ROOT/publish"

# 3. Stage the package the app serves from /shkarko.
PKG="$ROOT/publish/agent-package"
mkdir -p "$PKG"
mv "$ZIP_TMP/agjenti.zip" "$PKG/agjenti.zip"
rmdir "$ZIP_TMP"
cp "$DEPLOY/install-from-web.ps1" "$PKG/instalo.ps1"
cp "$ROOT/tools/kiosk-shortcut.ps1" "$PKG/kiosk.ps1"
printf '%s\n' "$VERSION" > "$PKG/version.txt"

echo
echo "==> Release ready in publish/"
echo "    agent-package/agjenti.zip  $(du -h "$PKG/agjenti.zip" | cut -f1)  (v$VERSION)"
echo "    served at /shkarko/agjenti.zip · install page at /pajisjet"
