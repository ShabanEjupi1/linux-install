#!/bin/bash
set -e

GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'
log() { echo -e "${GREEN}[+]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
err() { echo -e "${RED}[x]${NC} $1"; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SUPABASE_DIR="$SCRIPT_DIR/supabase-docker"

if [[ -d "$SUPABASE_DIR" ]]; then
    log "Supabase docker already cloned. Pulling latest..."
    cd "$SUPABASE_DIR" && git pull
else
    log "Cloning Supabase self-hosted docker..."
    git clone --depth 1 https://github.com/supabase/supabase "$SUPABASE_DIR"
fi

cd "$SUPABASE_DIR/docker"

if [[ ! -f .env ]]; then
    log "Creating .env from example..."
    cp .env.example .env

    warn ""
    warn "=== IMPORTANT: Edit $SUPABASE_DIR/docker/.env ==="
    warn "You MUST change these values before starting:"
    warn ""
    warn "  POSTGRES_PASSWORD     — strong random password"
    warn "  JWT_SECRET            — openssl rand -hex 32"
    warn "  ANON_KEY              — generate at: https://supabase.com/docs/guides/self-hosting/docker#generate-api-keys"
    warn "  SERVICE_ROLE_KEY      — same generator as above"
    warn "  DASHBOARD_USERNAME    — your admin username"
    warn "  DASHBOARD_PASSWORD    — your admin password"
    warn "  SITE_URL              — https://supabase.shabanejupi.tech"
    warn "  API_EXTERNAL_URL      — https://supabase.shabanejupi.tech"
    warn ""
    warn "After editing, run: cd $SUPABASE_DIR/docker && docker compose up -d"
    warn ""
    warn "Kong (the API gateway) will be exposed on port 8000."
    warn "Add this to $SUPABASE_DIR/docker/docker-compose.yml under the kong service:"
    warn "  ports:"
    warn "    - '8000:8000'"
else
    log ".env already exists, skipping template copy."
fi

log ""
log "Patching Kong port to 8000 in docker-compose.yml..."
# Add port 8000 to kong service if not already there
if ! grep -q '"8000:8000"' "$SUPABASE_DIR/docker/docker-compose.yml"; then
    sed -i '/container_name: supabase-kong/,/networks:/{/expose:/a\    ports:\n      - "8000:8000"
}' "$SUPABASE_DIR/docker/docker-compose.yml" || true
fi

log ""
log "Supabase setup complete. Edit .env then run:"
log "  cd $SUPABASE_DIR/docker && docker compose up -d"
