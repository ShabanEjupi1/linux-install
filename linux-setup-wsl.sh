#!/bin/bash
# Run this INSIDE Ubuntu (WSL2) on the Windows PC
# After windows-setup-ssh.ps1 has run and the PC has rebooted
# Usage: bash linux-setup-wsl.sh
set -e

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'
log()    { echo -e "${GREEN}[+]${NC} $1"; }
warn()   { echo -e "${YELLOW}[!]${NC} $1"; }
header() { echo -e "\n${CYAN}========== $1 ==========${NC}"; }
err()    { echo -e "${RED}[x]${NC} $1"; exit 1; }

[[ -z "$CF_API_TOKEN" ]] && err "Set your token first: export CF_API_TOKEN=cfut_xxxxx"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CRED_FILE="$HOME/server-credentials.txt"

gen_pass() { openssl rand -base64 32 | tr -dc 'a-zA-Z0-9!@#%' | head -c 24; }
gen_hex()  { openssl rand -hex 32; }

header "System Update"
sudo apt-get update -qq
sudo apt-get upgrade -y -qq
sudo apt-get install -y -qq curl wget git unzip jq

header "Docker"
if ! command -v docker &>/dev/null; then
    curl -fsSL https://get.docker.com | sh
    sudo usermod -aG docker "$USER"
    log "Docker installed — you may need to re-login for group changes"
fi

# WSL2: start Docker daemon manually (no systemd by default)
if ! docker info &>/dev/null 2>&1; then
    sudo dockerd &>/tmp/dockerd.log &
    sleep 3
fi

if ! docker compose version &>/dev/null 2>&1; then
    sudo apt-get install -y -qq docker-compose-plugin
fi
log "Docker Compose: $(docker compose version)"

header "Kernel Tuning (OpenSearch)"
# In WSL2 this applies to the shared kernel
sudo sysctl -w vm.max_map_count=262144 2>/dev/null || true
grep -qxF 'vm.max_map_count=262144' /etc/sysctl.conf 2>/dev/null || \
    echo 'vm.max_map_count=262144' | sudo tee -a /etc/sysctl.conf >/dev/null

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

log "Passwords generated"

header "Writing .env files"

cat > "$SCRIPT_DIR/wordpress-shabanejupi/.env" << EOF
MYSQL_ROOT_PASSWORD=$WP_SHABAN_ROOT
MYSQL_DATABASE=wordpress
MYSQL_USER=wordpress
MYSQL_PASSWORD=$WP_SHABAN_DB
WP_TABLE_PREFIX=wp_
EOF

cat > "$SCRIPT_DIR/wordpress-enisi/.env" << EOF
MYSQL_ROOT_PASSWORD=$WP_ENISI_ROOT
MYSQL_DATABASE=wordpress
MYSQL_USER=wordpress
MYSQL_PASSWORD=$WP_ENISI_DB
WP_TABLE_PREFIX=wp_
EOF

cat > "$SCRIPT_DIR/wordpress-halalbank/.env" << EOF
MYSQL_ROOT_PASSWORD=$WP_HALALBANK_ROOT
MYSQL_DATABASE=wordpress
MYSQL_USER=wordpress
MYSQL_PASSWORD=$WP_HALALBANK_DB
WP_TABLE_PREFIX=wp_
EOF

cat > "$SCRIPT_DIR/umami/.env" << EOF
POSTGRES_DB=umami
POSTGRES_USER=umami
POSTGRES_PASSWORD=$UMAMI_DB
APP_SECRET=$UMAMI_SECRET
EOF

cat > "$SCRIPT_DIR/meilisearch/.env" << EOF
MEILI_MASTER_KEY=$MEILI_KEY
EOF

cat > "$SCRIPT_DIR/opensearch/.env" << EOF
OPENSEARCH_ADMIN_PASSWORD=$OS_ADMIN_PASS
EOF

cat > "$SCRIPT_DIR/jitsi/.env" << EOF
PUBLIC_IP=185.174.211.45
TZ=Europe/Tirane
XMPP_DOMAIN=meet.jitsi
JICOFO_AUTH_PASSWORD=$JITSI_JICOFO
JVB_AUTH_PASSWORD=$JITSI_JVB
JIBRI_XMPP_PASSWORD=$JITSI_JIBRI
JIBRI_RECORDER_PASSWORD=$JITSI_RECORDER
EOF

log ".env files written"

header "Starting Services"

start_svc() {
    log "Starting $2..."
    cd "$SCRIPT_DIR/$1" && sudo docker compose up -d
}

start_svc "opensearch"            "OpenSearch + Dashboards"
start_svc "meilisearch"           "Meilisearch"
start_svc "umami"                 "Umami Analytics"
start_svc "wordpress-shabanejupi" "WordPress shabanejupi.tech"
start_svc "wordpress-enisi"       "WordPress enisi.tech"
start_svc "wordpress-halalbank"   "WordPress halalbankkosova.me"
start_svc "jitsi"                 "Jitsi Meet"

header "Supabase"
SUPABASE_DIR="$SCRIPT_DIR/supabase/supabase-docker"
if [[ ! -d "$SUPABASE_DIR" ]]; then
    git clone --depth 1 https://github.com/supabase/supabase "$SUPABASE_DIR"
fi
cd "$SUPABASE_DIR/docker"
SUPA_POSTGRES=$(gen_pass)
SUPA_JWT=$(gen_hex)
SUPA_ANON=$(gen_hex)
SUPA_SERVICE=$(gen_hex)
SUPA_DASH_PASS=$(gen_pass)
[[ ! -f .env ]] && cp .env.example .env
sed -i "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$SUPA_POSTGRES|" .env
sed -i "s|^JWT_SECRET=.*|JWT_SECRET=$SUPA_JWT|" .env
sed -i "s|^ANON_KEY=.*|ANON_KEY=$SUPA_ANON|" .env
sed -i "s|^SERVICE_ROLE_KEY=.*|SERVICE_ROLE_KEY=$SUPA_SERVICE|" .env
sed -i "s|^DASHBOARD_PASSWORD=.*|DASHBOARD_PASSWORD=$SUPA_DASH_PASS|" .env
sed -i "s|^SITE_URL=.*|SITE_URL=https://supabase.shabanejupi.tech|" .env
sed -i "s|^API_EXTERNAL_URL=.*|API_EXTERNAL_URL=https://supabase.shabanejupi.tech|" .env
if ! grep -q '"8000:8000"' docker-compose.yml; then
    sed -i '/container_name: supabase-kong/a\    ports:\n      - "8000:8000"' docker-compose.yml || true
fi
sudo docker compose up -d
log "Supabase started"

header "Saving Credentials"
cat > "$CRED_FILE" << EOF
=================================================================
CREDENTIALS — $(date)
Server: 185.174.211.45 (via ssh.spacecode.tech)
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

--- Meilisearch (port 8097) ---
URL              : https://search.shabanejupi.tech
Master Key       : $MEILI_KEY

--- OpenSearch (port 9200) ---
URL              : https://os.shabanejupi.tech
Dashboards       : https://dashboard.shabanejupi.tech
Username         : admin
Password         : $OS_ADMIN_PASS

--- Jitsi Meet (port 8095) ---
URL              : https://meet.shabanejupi.tech

--- Supabase (port 8000) ---
URL              : https://supabase.shabanejupi.tech
Dashboard Pass   : $SUPA_DASH_PASS
Postgres Pass    : $SUPA_POSTGRES
JWT Secret       : $SUPA_JWT
=================================================================
EOF
chmod 600 "$CRED_FILE"
log "Credentials saved to $CRED_FILE"

header "Done"
echo ""
log "All services running. Verify with: sudo docker ps"
echo ""
warn "WordPress sites need a one-time setup wizard on first visit."
warn "Umami default login: admin / umami — change it!"
echo ""
cat "$CRED_FILE"
