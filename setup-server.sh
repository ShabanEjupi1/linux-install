#!/bin/bash
# Fully automated one-shot server setup
# Usage: CF_API_TOKEN=xxx bash setup-server.sh
set -e

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'
log()    { echo -e "${GREEN}[+]${NC} $1"; }
warn()   { echo -e "${YELLOW}[!]${NC} $1"; }
err()    { echo -e "${RED}[x]${NC} $1"; exit 1; }
header() { echo -e "\n${CYAN}========== $1 ==========${NC}"; }

[[ $EUID -ne 0 ]] && err "Must run as root"
[[ -z "$CF_API_TOKEN" ]] && err "CF_API_TOKEN is not set"

APP_DIR="/opt/apps/linux-install"
CRED_FILE="/root/server-credentials.txt"
SERVER_IP="185.174.211.45"

gen_pass() { openssl rand -base64 32 | tr -dc 'a-zA-Z0-9!@#$%' | head -c 24; }
gen_hex()  { openssl rand -hex 32; }

header "System Update"
apt-get update -qq
apt-get upgrade -y -qq
apt-get install -y -qq curl wget git unzip ufw fail2ban jq

header "Docker"
if ! command -v docker &>/dev/null; then
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker
fi
log "Docker: $(docker --version)"

if ! docker compose version &>/dev/null 2>&1; then
    apt-get install -y -qq docker-compose-plugin
fi
log "Docker Compose: $(docker compose version)"

header "cloudflared"
if ! command -v cloudflared &>/dev/null; then
    curl -fsSL -o /tmp/cloudflared.deb \
        https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb
    dpkg -i /tmp/cloudflared.deb
    rm /tmp/cloudflared.deb
fi
log "cloudflared: $(cloudflared --version)"

header "Kernel Tuning"
sysctl -w vm.max_map_count=262144 &>/dev/null
grep -qxF 'vm.max_map_count=262144' /etc/sysctl.conf || \
    echo 'vm.max_map_count=262144' >> /etc/sysctl.conf

header "Firewall"
ufw --force reset
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow 10000/udp
ufw allow 3478/udp
ufw allow 3478/tcp
ufw --force enable
log "Firewall configured (SSH + Jitsi UDP ports open)"

# ─── Generate all passwords ───────────────────────────────────────────────────
header "Generating Passwords"

WP_SHABAN_ROOT=$(gen_pass)
WP_SHABAN_DB=$(gen_pass)
WP_ENISI_ROOT=$(gen_pass)
WP_ENISI_DB=$(gen_pass)
WP_HALALBANK_ROOT=$(gen_pass)
WP_HALALBANK_DB=$(gen_pass)
UMAMI_DB=$(gen_pass)
UMAMI_SECRET=$(gen_hex)
MEILI_KEY=$(gen_hex)
OS_ADMIN_PASS="Str0ng!$(gen_pass | head -c 12)"
JITSI_JICOFO=$(gen_hex | head -c 32)
JITSI_JVB=$(gen_hex | head -c 32)
JITSI_JIBRI=$(gen_hex | head -c 32)
JITSI_RECORDER=$(gen_hex | head -c 32)

log "All passwords generated"

# ─── Write .env files ─────────────────────────────────────────────────────────
header "Writing service configurations"

# WordPress - shabanejupi.tech
cat > "$APP_DIR/wordpress-shabanejupi/.env" << EOF
MYSQL_ROOT_PASSWORD=$WP_SHABAN_ROOT
MYSQL_DATABASE=wordpress
MYSQL_USER=wordpress
MYSQL_PASSWORD=$WP_SHABAN_DB
WP_TABLE_PREFIX=wp_
EOF

# WordPress - enisi.tech
cat > "$APP_DIR/wordpress-enisi/.env" << EOF
MYSQL_ROOT_PASSWORD=$WP_ENISI_ROOT
MYSQL_DATABASE=wordpress
MYSQL_USER=wordpress
MYSQL_PASSWORD=$WP_ENISI_DB
WP_TABLE_PREFIX=wp_
EOF

# WordPress - halalbankkosova.me
cat > "$APP_DIR/wordpress-halalbank/.env" << EOF
MYSQL_ROOT_PASSWORD=$WP_HALALBANK_ROOT
MYSQL_DATABASE=wordpress
MYSQL_USER=wordpress
MYSQL_PASSWORD=$WP_HALALBANK_DB
WP_TABLE_PREFIX=wp_
EOF

# Umami
cat > "$APP_DIR/umami/.env" << EOF
POSTGRES_DB=umami
POSTGRES_USER=umami
POSTGRES_PASSWORD=$UMAMI_DB
APP_SECRET=$UMAMI_SECRET
EOF

# Meilisearch
cat > "$APP_DIR/meilisearch/.env" << EOF
MEILI_MASTER_KEY=$MEILI_KEY
EOF

# OpenSearch
cat > "$APP_DIR/opensearch/.env" << EOF
OPENSEARCH_ADMIN_PASSWORD=$OS_ADMIN_PASS
EOF

# Jitsi
cat > "$APP_DIR/jitsi/.env" << EOF
PUBLIC_IP=$SERVER_IP
TZ=Europe/Tirane
XMPP_DOMAIN=meet.jitsi
JICOFO_AUTH_PASSWORD=$JITSI_JICOFO
JVB_AUTH_PASSWORD=$JITSI_JVB
JIBRI_XMPP_PASSWORD=$JITSI_JIBRI
JIBRI_RECORDER_PASSWORD=$JITSI_RECORDER
EOF

log "All .env files written"

# ─── Cloudflare tunnels via API ───────────────────────────────────────────────
header "Cloudflare Tunnels"

log "Fetching Cloudflare account ID..."
CF_ACCOUNT=$(curl -s -H "Authorization: Bearer $CF_API_TOKEN" \
    "https://api.cloudflare.com/client/v4/accounts" | jq -r '.result[0].id')

[[ -z "$CF_ACCOUNT" || "$CF_ACCOUNT" == "null" ]] && \
    err "Could not get Cloudflare account ID — check your API token"

log "Account ID: $CF_ACCOUNT"

declare -A TUNNEL_IDS=(
    ["enisi"]="8dcf0bb7-b230-42ba-9eba-cafad8ad73a2"
    ["halalbank"]="3ffa8e8e-939e-4976-8556-3ba5869dff6c"
    ["spacecode"]="42286711-64b9-42c4-8e51-fe9d00a51c2c"
    ["shabanejupi"]="e4215fb1-5f61-42f3-89a1-1d17f9b6fb72"
)

mkdir -p /etc/cloudflared

for NAME in "${!TUNNEL_IDS[@]}"; do
    UUID="${TUNNEL_IDS[$NAME]}"
    log "Fetching token for tunnel: $NAME ($UUID)"

    TUNNEL_TOKEN=$(curl -s -H "Authorization: Bearer $CF_API_TOKEN" \
        "https://api.cloudflare.com/client/v4/accounts/$CF_ACCOUNT/cfd_tunnel/$UUID/token" \
        | jq -r '.result')

    if [[ -z "$TUNNEL_TOKEN" || "$TUNNEL_TOKEN" == "null" ]]; then
        warn "Could not fetch token for tunnel $NAME — skipping"
        continue
    fi

    # Copy cloudflare config
    cp "$APP_DIR/cloudflare/tunnel-${NAME}.yml" "/etc/cloudflared/config-${NAME}.yml"

    # Create systemd service using --token (no JSON credentials file needed)
    cat > "/etc/systemd/system/cloudflared-${NAME}.service" << EOF
[Unit]
Description=Cloudflare Tunnel - ${NAME}
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/usr/bin/cloudflared tunnel run --token ${TUNNEL_TOKEN}
Restart=on-failure
RestartSec=10
User=root

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable "cloudflared-${NAME}"
    systemctl start "cloudflared-${NAME}"
    log "Tunnel $NAME started"
done

# ─── Start all services ───────────────────────────────────────────────────────
header "Starting Services"

start_service() {
    local DIR="$1"
    local LABEL="$2"
    log "Starting $LABEL..."
    cd "$APP_DIR/$DIR"
    docker compose up -d
    log "$LABEL is up"
}

start_service "opensearch"           "OpenSearch + Dashboards"
start_service "meilisearch"          "Meilisearch"
start_service "umami"                "Umami Analytics"
start_service "wordpress-shabanejupi" "WordPress (shabanejupi.tech)"
start_service "wordpress-enisi"      "WordPress (enisi.tech)"
start_service "wordpress-halalbank"  "WordPress (halalbankkosova.me)"
start_service "jitsi"               "Jitsi Meet"

# ─── Supabase ─────────────────────────────────────────────────────────────────
header "Supabase"
SUPABASE_DIR="$APP_DIR/supabase/supabase-docker"

if [[ ! -d "$SUPABASE_DIR" ]]; then
    log "Cloning Supabase..."
    git clone --depth 1 https://github.com/supabase/supabase "$SUPABASE_DIR"
fi

cd "$SUPABASE_DIR/docker"

SUPA_POSTGRES=$(gen_pass)
SUPA_JWT=$(gen_hex)
SUPA_ANON=$(gen_hex)
SUPA_SERVICE=$(gen_hex)
SUPA_DASHBOARD_PASS=$(gen_pass)

if [[ ! -f .env ]]; then
    cp .env.example .env
fi

# Patch the critical values in supabase .env
sed -i "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$SUPA_POSTGRES|" .env
sed -i "s|^JWT_SECRET=.*|JWT_SECRET=$SUPA_JWT|" .env
sed -i "s|^ANON_KEY=.*|ANON_KEY=$SUPA_ANON|" .env
sed -i "s|^SERVICE_ROLE_KEY=.*|SERVICE_ROLE_KEY=$SUPA_SERVICE|" .env
sed -i "s|^DASHBOARD_PASSWORD=.*|DASHBOARD_PASSWORD=$SUPA_DASHBOARD_PASS|" .env
sed -i "s|^SITE_URL=.*|SITE_URL=https://supabase.shabanejupi.tech|" .env
sed -i "s|^API_EXTERNAL_URL=.*|API_EXTERNAL_URL=https://supabase.shabanejupi.tech|" .env

# Expose Kong on port 8000 if not already
if ! grep -q '"8000:8000"' docker-compose.yml 2>/dev/null; then
    sed -i 's/container_name: supabase-kong/container_name: supabase-kong\n    ports:\n      - "8000:8000"/' docker-compose.yml || true
fi

docker compose up -d
log "Supabase started"

# ─── Save all credentials ─────────────────────────────────────────────────────
header "Saving Credentials"

cat > "$CRED_FILE" << EOF
=================================================================
SERVER CREDENTIALS — $(date)
Server: $SERVER_IP
=================================================================

--- WordPress: shabanejupi.tech (port 8080) ---
DB Root Password : $WP_SHABAN_ROOT
DB User Password : $WP_SHABAN_DB
Setup Wizard     : https://shabanejupi.tech/wp-admin/install.php

--- WordPress: enisi.tech (port 8085) ---
DB Root Password : $WP_ENISI_ROOT
DB User Password : $WP_ENISI_DB
Setup Wizard     : https://enisi.tech/wp-admin/install.php

--- WordPress: halalbankkosova.me (port 8090) ---
DB Root Password : $WP_HALALBANK_ROOT
DB User Password : $WP_HALALBANK_DB
Setup Wizard     : https://halalbankkosova.me/wp-admin/install.php

--- Umami Analytics (port 8002) ---
URL              : https://audit.spacecode.tech
Default Login    : admin / umami  (change on first login)
DB Password      : $UMAMI_DB
App Secret       : $UMAMI_SECRET

--- Meilisearch (port 8097) ---
URL              : https://search.shabanejupi.tech
Master Key       : $MEILI_KEY

--- OpenSearch (port 9200) ---
URL              : https://os.shabanejupi.tech
Dashboards URL   : https://dashboard.shabanejupi.tech
Username         : admin
Password         : $OS_ADMIN_PASS

--- Jitsi Meet (port 8095) ---
URL              : https://meet.shabanejupi.tech
(No login required by default — rooms are open)

--- Supabase (port 8000) ---
URL              : https://supabase.shabanejupi.tech
Dashboard User   : supabase
Dashboard Pass   : $SUPA_DASHBOARD_PASS
Postgres Pass    : $SUPA_POSTGRES
JWT Secret       : $SUPA_JWT
Anon Key         : $SUPA_ANON
Service Key      : $SUPA_SERVICE

=================================================================
EOF

chmod 600 "$CRED_FILE"
log "Credentials saved to $CRED_FILE"

# ─── Done ─────────────────────────────────────────────────────────────────────
header "Setup Complete"

echo ""
log "All services are running. Your sites:"
echo ""
echo "  https://shabanejupi.tech"
echo "  https://www.shabanejupi.tech"
echo "  https://enisi.tech"
echo "  https://www.enisi.tech"
echo "  https://halalbankkosova.me"
echo "  https://www.halalbankkosova.me"
echo "  https://supabase.shabanejupi.tech"
echo "  https://meet.shabanejupi.tech"
echo "  https://dashboard.shabanejupi.tech"
echo "  https://os.shabanejupi.tech"
echo "  https://search.shabanejupi.tech"
echo "  https://audit.spacecode.tech"
echo ""
warn "WordPress sites need a one-time setup wizard (just fill in site name + admin email)."
warn "All passwords are at: $CRED_FILE"
echo ""
echo "Check service status: docker ps"
echo "Check tunnel status:  systemctl status cloudflared-*"
