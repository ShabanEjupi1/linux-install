#!/bin/bash
set -e

GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'
log() { echo -e "${GREEN}[+]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
err() { echo -e "${RED}[x]${NC} $1"; exit 1; }

[[ $EUID -ne 0 ]] && err "Run as root"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_DIR="/etc/cloudflared"

declare -A TUNNELS=(
    ["enisi"]="8dcf0bb7-b230-42ba-9eba-cafad8ad73a2"
    ["halalbank"]="3ffa8e8e-939e-4976-8556-3ba5869dff6c"
    ["spacecode"]="42286711-64b9-42c4-8e51-fe9d00a51c2c"
    ["shabanejupi"]="e4215fb1-5f61-42f3-89a1-1d17f9b6fb72"
)

log "Checking tunnel credential files..."
MISSING=0
for NAME in "${!TUNNELS[@]}"; do
    UUID="${TUNNELS[$NAME]}"
    CRED_FILE="$CONFIG_DIR/$UUID.json"
    if [[ ! -f "$CRED_FILE" ]]; then
        warn "Missing: $CRED_FILE  (tunnel: $NAME)"
        MISSING=$((MISSING + 1))
    else
        log "Found credentials for $NAME ($UUID)"
    fi
done

if [[ $MISSING -gt 0 ]]; then
    echo ""
    warn "=== How to get missing credentials ==="
    warn "Option A — Download from Cloudflare dashboard:"
    warn "  1. Go to https://one.dash.cloudflare.com"
    warn "  2. Networks > Tunnels > click the tunnel > Configure"
    warn "  3. Download the credentials JSON file"
    warn "  4. Place it at /etc/cloudflared/<UUID>.json"
    warn ""
    warn "Option B — Re-authenticate on this machine:"
    warn "  cloudflared tunnel login"
    warn "  cloudflared tunnel token <UUID>"
    warn "  Then save the output to the JSON file manually."
    warn ""
    warn "Re-run this script after placing credentials."
    exit 1
fi

log "All credentials present. Installing tunnel services..."

for NAME in "${!TUNNELS[@]}"; do
    UUID="${TUNNELS[$NAME]}"
    CONFIG_FILE="$SCRIPT_DIR/tunnel-${NAME}.yml"
    SERVICE_NAME="cloudflared-${NAME}"

    cp "$CONFIG_FILE" "$CONFIG_DIR/config-${NAME}.yml"

    # Create systemd service
    cat > "/etc/systemd/system/${SERVICE_NAME}.service" << EOF
[Unit]
Description=Cloudflare Tunnel - ${NAME}
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/usr/bin/cloudflared tunnel --config $CONFIG_DIR/config-${NAME}.yml run
Restart=on-failure
RestartSec=5
User=root

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable "${SERVICE_NAME}"
    systemctl restart "${SERVICE_NAME}"
    log "Started $SERVICE_NAME"
done

log ""
log "All Cloudflare tunnels are running."
log "Check status: systemctl status cloudflared-*"
