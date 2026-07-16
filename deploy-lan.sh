#!/bin/bash
# LAN-box deployment: WordPress x3 + Supabase + OpenSearch + Meilisearch + Umami
# and the 4 Cloudflare tunnels. Jitsi is deployed separately on the Oracle VM
# (see oracle/deploy-jitsi.sh) because its WebRTC media needs a real public IP.
#
# Usage:  CF_API_TOKEN=xxxxx sudo -E bash deploy-lan.sh
set -e

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'
log()    { echo -e "${GREEN}[+]${NC} $1"; }
warn()   { echo -e "${YELLOW}[!]${NC} $1"; }
err()    { echo -e "${RED}[x]${NC} $1"; exit 1; }
header() { echo -e "\n${CYAN}========== $1 ==========${NC}"; }

[[ $EUID -ne 0 ]] && err "Must run as root (use: sudo -E bash deploy-lan.sh)"
[[ -z "$CF_API_TOKEN" ]] && err "CF_API_TOKEN is not set"

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CRED_FILE="/root/server-credentials.txt"

gen_pass() { openssl rand -base64 32 | tr -dc 'a-zA-Z0-9!@#$%' | head -c 24; }
gen_hex()  { openssl rand -hex 32; }

# Write a .env file only if it does not already exist (protects live DB passwords)
write_env() {
    local FILE="$1"; shift
    if [[ -f "$FILE" ]]; then
        warn "Keeping existing $FILE (not regenerating passwords)"
        return
    fi
    printf '%s\n' "$@" > "$FILE"
    log "Wrote $FILE"
}

header "System packages"
apt-get update -qq
apt-get install -y -qq curl wget git unzip jq openssl ca-certificates

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
log "Compose: $(docker compose version)"

header "Kernel tuning (OpenSearch)"
sysctl -w vm.max_map_count=262144 &>/dev/null
grep -qxF 'vm.max_map_count=262144' /etc/sysctl.conf || \
    echo 'vm.max_map_count=262144' >> /etc/sysctl.conf
log "vm.max_map_count=262144"

# ─── Generate + write .env files (idempotent) ─────────────────────────────────
header "Service configuration (.env files)"

write_env "$APP_DIR/wordpress-shabanejupi/.env" \
    "MYSQL_ROOT_PASSWORD=$(gen_pass)" \
    "MYSQL_DATABASE=wordpress" \
    "MYSQL_USER=wordpress" \
    "MYSQL_PASSWORD=$(gen_pass)" \
    "WP_TABLE_PREFIX=wp_"

write_env "$APP_DIR/wordpress-enisi/.env" \
    "MYSQL_ROOT_PASSWORD=$(gen_pass)" \
    "MYSQL_DATABASE=wordpress" \
    "MYSQL_USER=wordpress" \
    "MYSQL_PASSWORD=$(gen_pass)" \
    "WP_TABLE_PREFIX=wp_"

write_env "$APP_DIR/wordpress-halalbank/.env" \
    "MYSQL_ROOT_PASSWORD=$(gen_pass)" \
    "MYSQL_DATABASE=wordpress" \
    "MYSQL_USER=wordpress" \
    "MYSQL_PASSWORD=$(gen_pass)" \
    "WP_TABLE_PREFIX=wp_"

write_env "$APP_DIR/umami/.env" \
    "POSTGRES_DB=umami" \
    "POSTGRES_USER=umami" \
    "POSTGRES_PASSWORD=$(gen_pass)" \
    "APP_SECRET=$(gen_hex)"

write_env "$APP_DIR/meilisearch/.env" \
    "MEILI_MASTER_KEY=$(gen_hex)"

write_env "$APP_DIR/opensearch/.env" \
    "OPENSEARCH_ADMIN_PASSWORD=Str0ng!$(gen_pass | head -c 12)"

# ─── Cloudflare tunnels (token-based systemd services) ────────────────────────
# Requires an API token with account-level "Cloudflare Tunnel:Read" + "Account:Read".
# A zone/DNS-only token cannot fetch connector tokens — in that case this step is
# skipped and the apps still come up; wire the tunnels separately.
header "Cloudflare tunnels"
log "Fetching Cloudflare account ID..."
CF_ACCOUNT=$(curl -s -H "Authorization: Bearer $CF_API_TOKEN" \
    "https://api.cloudflare.com/client/v4/accounts" | jq -r '.result[0].id')
# Fallback: a token scoped to zones (but not Account Settings:Read) can't list
# /accounts, yet the account id is exposed on each zone.
if [[ -z "$CF_ACCOUNT" || "$CF_ACCOUNT" == "null" ]]; then
    CF_ACCOUNT=$(curl -s -H "Authorization: Bearer $CF_API_TOKEN" \
        "https://api.cloudflare.com/client/v4/zones" | jq -r '.result[0].account.id')
fi

declare -A TUNNEL_IDS=(
    ["enisi"]="8dcf0bb7-b230-42ba-9eba-cafad8ad73a2"
    ["halalbank"]="3ffa8e8e-939e-4976-8556-3ba5869dff6c"
    ["spacecode"]="42286711-64b9-42c4-8e51-fe9d00a51c2c"
    ["shabanejupi"]="e4215fb1-5f61-42f3-89a1-1d17f9b6fb72"
)

if [[ -z "$CF_ACCOUNT" || "$CF_ACCOUNT" == "null" ]]; then
    warn "Token lacks account-level access — SKIPPING tunnel setup."
    warn "Apps will still start locally; run the 4 tunnels separately to expose them."
    TUNNEL_IDS=()
else
    log "Account: $CF_ACCOUNT"
fi

for NAME in "${!TUNNEL_IDS[@]}"; do
    UUID="${TUNNEL_IDS[$NAME]}"
    TOKEN=$(curl -s -H "Authorization: Bearer $CF_API_TOKEN" \
        "https://api.cloudflare.com/client/v4/accounts/$CF_ACCOUNT/cfd_tunnel/$UUID/token" \
        | jq -r '.result')
    if [[ -z "$TOKEN" || "$TOKEN" == "null" ]]; then
        warn "No token for tunnel '$NAME' ($UUID) — skipping (does it exist in this account?)"
        continue
    fi
    cat > "/etc/systemd/system/cloudflared-${NAME}.service" << EOF
[Unit]
Description=Cloudflare Tunnel - ${NAME}
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/usr/bin/cloudflared --no-autoupdate tunnel run --token ${TOKEN}
Restart=on-failure
RestartSec=10
User=root

[Install]
WantedBy=multi-user.target
EOF
    systemctl daemon-reload
    systemctl enable "cloudflared-${NAME}" &>/dev/null
    systemctl restart "cloudflared-${NAME}"
    log "Tunnel '$NAME' running"
done

# ─── Start application services (Jitsi lives on Oracle, not here) ─────────────
header "Starting services"
start_service() {
    log "Starting $2..."
    cd "$APP_DIR/$1"
    docker compose up -d || { warn "$1 failed (likely DB still initializing) — retrying in 25s..."; sleep 25; docker compose up -d; }
}
start_service "opensearch"            "OpenSearch + Dashboards"
start_service "meilisearch"           "Meilisearch"
start_service "umami"                 "Umami Analytics"
start_service "wordpress-shabanejupi" "WordPress (shabanejupi.tech)"
start_service "wordpress-enisi"       "WordPress (enisi.tech)"
start_service "wordpress-halalbank"   "WordPress (halalbankkosova.me)"

# ─── Supabase ─────────────────────────────────────────────────────────────────
header "Supabase"
SUPABASE_DIR="$APP_DIR/supabase/supabase-docker"
[[ -d "$SUPABASE_DIR" ]] || git clone --depth 1 https://github.com/supabase/supabase "$SUPABASE_DIR"
cd "$SUPABASE_DIR/docker"
if [[ ! -f .env ]]; then
    cp .env.example .env
    sed -i "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$(gen_pass)|"       .env
    sed -i "s|^JWT_SECRET=.*|JWT_SECRET=$(gen_hex)|"                      .env
    sed -i "s|^DASHBOARD_PASSWORD=.*|DASHBOARD_PASSWORD=$(gen_pass)|"     .env
    sed -i "s|^SITE_URL=.*|SITE_URL=https://supabase.shabanejupi.tech|"   .env
    sed -i "s|^API_EXTERNAL_URL=.*|API_EXTERNAL_URL=https://supabase.shabanejupi.tech|" .env
    log "Supabase .env generated"
else
    warn "Keeping existing Supabase .env"
fi
# Kong is already published by the upstream compose on ${KONG_HTTP_PORT}:8000.
# Just make sure the host port is 8000 (matches the tunnel ingress). Do NOT
# inject a second "ports:" key — the upstream compose already defines one.
sed -i "s|^KONG_HTTP_PORT=.*|KONG_HTTP_PORT=8000|" .env
docker compose up -d
log "Supabase started"

# ─── Credentials summary ──────────────────────────────────────────────────────
header "Credentials"
{
    echo "===== LAN SERVER CREDENTIALS — $(date) ====="
    for d in wordpress-shabanejupi wordpress-enisi wordpress-halalbank umami meilisearch opensearch; do
        echo ""; echo "--- $d ---"; cat "$APP_DIR/$d/.env"
    done
    echo ""; echo "--- supabase (docker/.env, key values) ---"
    grep -E '^(POSTGRES_PASSWORD|JWT_SECRET|DASHBOARD_USERNAME|DASHBOARD_PASSWORD)=' \
        "$SUPABASE_DIR/docker/.env" 2>/dev/null
} > "$CRED_FILE"
chmod 600 "$CRED_FILE"
log "Saved to $CRED_FILE"

header "Done"
log "Containers:"; docker ps --format '  {{.Names}}\t{{.Status}}'
log "Tunnels:";   systemctl list-units 'cloudflared*' --no-legend --plain | awk '{print "  "$1"  "$4}'
