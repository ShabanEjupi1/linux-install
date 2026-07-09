#!/usr/bin/env bash
# Builds the shop-PC install package for the KosovaPOS hardware agent.
#
# Produces publish/agent/KosovaPOS-Agent-<version>.zip containing a self-contained
# win-x64 exe (no .NET runtime needed on the cashier PC) plus the install/uninstall/
# test scripts. Cross-compiles fine from Linux — no Windows box required to build.
#
#   ./tools/publish-agent.sh            # build the zip
#
# Then copy the zip to the shop PC, unzip, and from an elevated PowerShell:
#   .\install-agent.ps1
set -euo pipefail

cd "$(dirname "$0")/.."
ROOT="$PWD"
PROJECT="$ROOT/src/KosovaPOS.Agent"
STAGE="$ROOT/publish/agent"

VERSION=$(grep -oPm1 '(?<=<Version>)[^<]+' "$PROJECT/KosovaPOS.Agent.csproj")
[ -n "$VERSION" ] || { echo "Could not read <Version> from the csproj" >&2; exit 1; }

echo "==> Building KosovaPOS.Agent $VERSION (win-x64, self-contained, single file)"
rm -rf "$STAGE"
mkdir -p "$STAGE"

dotnet publish "$PROJECT" \
  -c Release \
  -r win-x64 \
  --self-contained \
  -o "$STAGE/KosovaPOS-Agent-$VERSION"

PKG="$STAGE/KosovaPOS-Agent-$VERSION"

# Trim to what the shop actually needs: drop debug symbols and the IIS in-process
# hosting module, which the runtime pack always emits but a self-hosted service
# never loads.
rm -f "$PKG"/*.pdb "$PKG"/*.staticwebassets.endpoints.json "$PKG"/aspnetcorev2_inprocess.dll

cp "$PROJECT/deploy/install-agent.ps1"   "$PKG/"
cp "$PROJECT/deploy/uninstall-agent.ps1" "$PKG/"
cp "$PROJECT/deploy/test-agent.ps1"      "$PKG/"
cp "$PROJECT/deploy/INSTALL.md"          "$PKG/"

command -v zip >/dev/null || { echo "zip not installed: sudo apt-get install -y zip" >&2; exit 1; }
( cd "$STAGE" && zip -qr "KosovaPOS-Agent-$VERSION.zip" "KosovaPOS-Agent-$VERSION" )

echo
echo "==> Package ready:"
ls -lh "$STAGE/KosovaPOS-Agent-$VERSION.zip" | awk '{print "    " $NF " (" $5 ")"}'
echo
echo "    Copy it to the shop PC, unzip, then from an ELEVATED PowerShell:"
echo "      .\\install-agent.ps1"
