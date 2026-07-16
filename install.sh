#!/bin/bash
set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log() { echo -e "${GREEN}[+]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
err() { echo -e "${RED}[x]${NC} $1"; exit 1; }

[[ $EUID -ne 0 ]] && err "Run as root: sudo bash install.sh"

SERVER_IP="185.174.211.45"
INTERNAL_IP="192.168.100.61"
APP_DIR="/opt/apps"

log "=== Server Setup for $SERVER_IP ==="

# System update
log "Updating system packages..."
apt-get update -qq && apt-get upgrade -y -qq

# Base dependencies
log "Installing base dependencies..."
apt-get install -y -qq curl wget git unzip ufw fail2ban

# Docker
if ! command -v docker &>/dev/null; then
    log "Installing Docker..."
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker
    log "Docker installed: $(docker --version)"
else
    log "Docker already installed: $(docker --version)"
fi

# Docker Compose plugin
if ! docker compose version &>/dev/null 2>&1; then
    log "Installing Docker Compose plugin..."
    apt-get install -y -qq docker-compose-plugin
fi
log "Docker Compose: $(docker compose version)"

# cloudflared
if ! command -v cloudflared &>/dev/null; then
    log "Installing cloudflared..."
    curl -L --output /tmp/cloudflared.deb \
        https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb
    dpkg -i /tmp/cloudflared.deb
    rm /tmp/cloudflared.deb
    log "cloudflared installed: $(cloudflared --version)"
else
    log "cloudflared already installed"
fi

# Firewall
log "Configuring firewall..."
ufw --force reset
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp    # SSH
ufw allow 80/tcp    # HTTP (for Let's Encrypt / redirects)
ufw allow 443/tcp   # HTTPS
ufw allow 10000/udp # Jitsi JVB (video)
ufw allow 3478/udp  # Jitsi TURN/STUN
ufw allow 3478/tcp  # Jitsi TURN/STUN
ufw --force enable
log "Firewall configured"

# Sysctl tuning (required for OpenSearch)
log "Tuning kernel parameters..."
cat >> /etc/sysctl.conf << 'EOF'

# OpenSearch requirement
vm.max_map_count=262144
# Network performance
net.core.somaxconn=65535
net.ipv4.tcp_max_syn_backlog=65535
EOF
sysctl -p -q

# Copy repo to /opt/apps
log "Deploying app configs to $APP_DIR..."
cp -r "$(dirname "$0")" "$APP_DIR" 2>/dev/null || true

# Create cloudflared config directory
mkdir -p /etc/cloudflared

log ""
log "=== Base installation complete ==="
log ""
warn "NEXT STEPS — run in order:"
warn ""
warn "1. Copy Cloudflare tunnel credentials:"
warn "   Place each tunnel .json file into /etc/cloudflared/"
warn "   File names must match the tunnel UUIDs exactly."
warn ""
warn "2. Install Cloudflare tunnels as services:"
warn "   cd $APP_DIR && bash cloudflare/setup-cloudflare.sh"
warn ""
warn "3. Start each application:"
warn "   cd $APP_DIR && bash deploy.sh"
warn ""
warn "4. Visit each site and complete WordPress setup wizards."
