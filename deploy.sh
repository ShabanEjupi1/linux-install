#!/bin/bash
set -e

GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; CYAN='\033[0;36m'; NC='\033[0m'
log() { echo -e "${GREEN}[+]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
err() { echo -e "${RED}[x]${NC} $1"; }
header() { echo -e "\n${CYAN}=== $1 ===${NC}"; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MISSING_ENVS=0

check_env() {
    local DIR="$1"
    local NAME="$2"
    if [[ ! -f "$SCRIPT_DIR/$DIR/.env" ]]; then
        warn "Missing .env in $DIR — copy .env.example to .env and fill in secrets"
        MISSING_ENVS=$((MISSING_ENVS + 1))
    fi
}

header "Pre-flight: checking .env files"
check_env "wordpress-shabanejupi" "shabanejupi WordPress"
check_env "wordpress-enisi" "enisi WordPress"
check_env "wordpress-halalbank" "halalbank WordPress"
check_env "umami" "Umami"
check_env "meilisearch" "Meilisearch"
check_env "opensearch" "OpenSearch"
check_env "jitsi" "Jitsi"

if [[ $MISSING_ENVS -gt 0 ]]; then
    echo ""
    err "$MISSING_ENVS .env file(s) missing. Fix them before deploying."
    echo ""
    echo "Quick setup — run these from $SCRIPT_DIR:"
    echo "  for d in wordpress-shabanejupi wordpress-enisi wordpress-halalbank umami meilisearch opensearch jitsi; do"
    echo "    cp \$d/.env.example \$d/.env"
    echo "  done"
    echo ""
    echo "Then edit each .env with real passwords and re-run this script."
    exit 1
fi

START_ORDER=(
    "opensearch"
    "meilisearch"
    "umami"
    "wordpress-shabanejupi"
    "wordpress-enisi"
    "wordpress-halalbank"
    "jitsi"
)

LABELS=(
    "OpenSearch + Dashboards       → os.shabanejupi.tech (:9200) + dashboard.shabanejupi.tech (:5601)"
    "Meilisearch                   → search.shabanejupi.tech (:8097)"
    "Umami Analytics               → audit.spacecode.tech (:8002)"
    "WordPress shabanejupi.tech    → shabanejupi.tech (:8080)"
    "WordPress enisi.tech          → enisi.tech (:8085)"
    "WordPress halalbankkosova.me  → halalbankkosova.me (:8090)"
    "Jitsi Meet                    → meet.shabanejupi.tech (:8095)"
)

for i in "${!START_ORDER[@]}"; do
    DIR="${START_ORDER[$i]}"
    LABEL="${LABELS[$i]}"
    header "Starting: $LABEL"
    cd "$SCRIPT_DIR/$DIR"
    docker compose up -d
    log "Started $DIR"
done

header "Supabase"
if [[ -d "$SCRIPT_DIR/supabase/supabase-docker/docker" ]]; then
    ENV_FILE="$SCRIPT_DIR/supabase/supabase-docker/docker/.env"
    if [[ -f "$ENV_FILE" ]]; then
        cd "$SCRIPT_DIR/supabase/supabase-docker/docker"
        docker compose up -d
        log "Started Supabase → supabase.shabanejupi.tech (:8000)"
    else
        warn "Supabase .env not configured. Run: bash supabase/setup.sh"
    fi
else
    warn "Supabase not set up yet. Run: bash supabase/setup.sh"
fi

header "All services status"
echo ""
printf "%-45s %-12s %s\n" "SERVICE" "PORT" "URL"
printf "%-45s %-12s %s\n" "-------" "----" "---"
printf "%-45s %-12s %s\n" "WordPress (shabanejupi.tech)" "8080" "https://shabanejupi.tech"
printf "%-45s %-12s %s\n" "WordPress (enisi.tech)" "8085" "https://enisi.tech"
printf "%-45s %-12s %s\n" "WordPress (halalbankkosova.me)" "8090" "https://halalbankkosova.me"
printf "%-45s %-12s %s\n" "Supabase" "8000" "https://supabase.shabanejupi.tech"
printf "%-45s %-12s %s\n" "Jitsi Meet" "8095" "https://meet.shabanejupi.tech"
printf "%-45s %-12s %s\n" "OpenSearch Dashboards" "5601" "https://dashboard.shabanejupi.tech"
printf "%-45s %-12s %s\n" "OpenSearch" "9200" "https://os.shabanejupi.tech"
printf "%-45s %-12s %s\n" "Meilisearch" "8097" "https://search.shabanejupi.tech"
printf "%-45s %-12s %s\n" "Umami Analytics" "8002" "https://audit.spacecode.tech"
echo ""
log "Deploy complete. Check individual logs with: docker compose -f <dir>/docker-compose.yml logs -f"
