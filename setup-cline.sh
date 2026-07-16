#!/usr/bin/env bash
# Phase 2 — move dev work off Codespaces onto code-server + Cline on this box.
#
# Run this ON THE BOX, after ./migrate-to-gitea.sh:
#   cd /home/shabanejupi/linux-install && ./setup-cline.sh
#
# What it does:
#   1. generates a real code-server password (the compose default is "admin")
#   2. nginx vhost for cline.spacecode.tech -> 8100, with WebSocket upgrade
#   3. builds + starts code-server
#   4. installs the Cline and Continue extensions if missing
#   5. points Cline at the local Ollama the news-portal already uses
#   6. writes vault-additions-cline.json for import into Vaultwarden
#
# Idempotent: safe to re-run.
set -euo pipefail

cd "$(dirname "$0")"

CLINE_HOST="cline.spacecode.tech"
ENV_FILE="code-server/.env"
OUT="vault-additions-cline.json"

say()  { printf '\033[1;36m==>\033[0m %s\n' "$1"; }
warn() { printf '\033[1;33m !! \033[0m%s\n' "$1"; }
die()  { printf '\033[1;31mERR\033[0m %s\n' "$1" >&2; exit 1; }
gen()  { openssl rand -base64 24 | tr -d '/+=' | cut -c1-24; }

[ -d code-server ] || die "run this from the repo root (no code-server/ dir here)"
command -v docker >/dev/null || die "docker not found"

# ---------------------------------------------------------------------------
# 1. password.
#    code-server/docker-compose.yml falls back to PASSWORD:-admin. On a
#    hostname the whole internet can reach, that is an open IDE with a shell
#    in it. Generate one and keep it in a gitignored .env.
# ---------------------------------------------------------------------------
PASSWORD=""
if [ -f "$ENV_FILE" ] && grep -qE '^PASSWORD=.+' "$ENV_FILE"; then
  say "code-server password already set in $ENV_FILE — reusing it"
else
  PASSWORD="$(gen)"
  say "generating a code-server password -> $ENV_FILE"
  umask 077
  printf 'PASSWORD=%s\n' "$PASSWORD" > "$ENV_FILE"
  chmod 600 "$ENV_FILE"
fi

# The data dir is bind-mounted, so it must exist and be owned by the image's
# coder user (uid 1000) or code-server cannot write its own state.
say "ensuring /opt/code-server/data"
sudo mkdir -p /opt/code-server/data
sudo chown -R 1000:1000 /opt/code-server/data

# ---------------------------------------------------------------------------
# 2. nginx vhost.
#    code-server is a WebSocket app. Without the Upgrade/Connection headers
#    the UI loads and then hangs retrying to reconnect, which looks like a
#    broken IDE rather than a broken proxy.
# ---------------------------------------------------------------------------
if [ -d /etc/nginx/sites-available ]; then
  NGINX_CONF=/etc/nginx/sites-available/${CLINE_HOST}
  NGINX_LINK=/etc/nginx/sites-enabled/${CLINE_HOST}
elif [ -d /etc/nginx/conf.d ]; then
  NGINX_CONF=/etc/nginx/conf.d/${CLINE_HOST}.conf
  NGINX_LINK=""
else
  NGINX_CONF=""
  warn "no nginx config dir found — skipping vhost; add it by hand"
fi

if [ -n "$NGINX_CONF" ] && [ ! -f "$NGINX_CONF" ]; then
  say "writing nginx vhost $NGINX_CONF"
  sudo tee "$NGINX_CONF" >/dev/null <<EOF
# cline.spacecode.tech -> code-server. Reached via the 'ampere' Cloudflare
# tunnel's *.spacecode.tech rule, which lands on this nginx.
server {
    listen 80;
    server_name ${CLINE_HOST};

    # Extension installs and file uploads go through here.
    client_max_body_size 0;

    location / {
        proxy_pass http://127.0.0.1:8100;
        proxy_set_header Host              \$host;
        proxy_set_header X-Real-IP         \$remote_addr;
        proxy_set_header X-Forwarded-For   \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;

        # code-server is WebSocket-based; without these it loads and then
        # hangs forever trying to reconnect.
        proxy_http_version 1.1;
        proxy_set_header Upgrade    \$http_upgrade;
        proxy_set_header Connection upgrade;
        proxy_read_timeout 86400s;
    }
}
EOF
  [ -n "$NGINX_LINK" ] && sudo ln -sfn "$NGINX_CONF" "$NGINX_LINK"
  sudo nginx -t || die "nginx config test failed — vhost written but NOT loaded"
  sudo systemctl reload nginx
  say "nginx reloaded"
elif [ -n "$NGINX_CONF" ]; then
  say "nginx vhost already present — leaving it alone"
fi

# ---------------------------------------------------------------------------
# 3. build + start
# ---------------------------------------------------------------------------
say "building and starting code-server"
( cd code-server && docker compose up -d --build )

say "waiting for code-server on 127.0.0.1:8100"
for i in $(seq 1 60); do
  if curl -fsS -o /dev/null "http://127.0.0.1:8100/healthz" 2>/dev/null \
     || curl -fsS -o /dev/null "http://127.0.0.1:8100/" 2>/dev/null; then break; fi
  [ "$i" = 60 ] && die "code-server did not come up — check: docker logs code-server"
  sleep 2
done
say "code-server is up"

# ---------------------------------------------------------------------------
# 4. extensions
# ---------------------------------------------------------------------------
install_ext() {
  local id="$1"
  if docker exec code-server code-server --list-extensions 2>/dev/null | grep -qix "$id"; then
    say "extension $id already installed"
  else
    say "installing extension $id"
    docker exec code-server code-server --install-extension "$id" >/dev/null \
      || warn "could not install $id — install it from the UI"
  fi
}
install_ext saoudrizwan.claude-dev   # Cline
install_ext continue.continue

# ---------------------------------------------------------------------------
# 5. point Cline at the local Ollama (same one news-portal uses — no API key,
#    nothing leaves the box).
# ---------------------------------------------------------------------------
SETTINGS_DIR=/opt/code-server/data/User
say "seeding Cline settings"
sudo mkdir -p "$SETTINGS_DIR"
if [ -f "$SETTINGS_DIR/settings.json" ]; then
  say "settings.json exists — leaving your settings alone"
else
  sudo tee "$SETTINGS_DIR/settings.json" >/dev/null <<'EOF'
{
  "cline.apiProvider": "ollama",
  "cline.ollamaBaseUrl": "http://host.docker.internal:11434",
  "git.confirmSync": false,
  "git.autofetch": true
}
EOF
  sudo chown -R 1000:1000 "$SETTINGS_DIR"
fi

if ! docker exec code-server sh -c 'getent hosts host.docker.internal' >/dev/null 2>&1; then
  warn "host.docker.internal does not resolve in the container."
  warn "Add to code-server/docker-compose.yml under the service:"
  warn '    extra_hosts: ["host.docker.internal:host-gateway"]'
  warn "then: cd code-server && docker compose up -d --force-recreate"
fi

# ---------------------------------------------------------------------------
# 6. record into the vault
# ---------------------------------------------------------------------------
if [ -n "$PASSWORD" ]; then
  say "writing $OUT"
  python3 - "$OUT" "$PASSWORD" "$CLINE_HOST" <<'PY'
import json, sys, datetime
out, pw, host = sys.argv[1:4]
items = [{
    "type": 1, "name": "code-server / Cline IDE",
    "folderId": "a6717076-551f-4060-bd79-1e01d933d28e",
    "favorite": False,
    "notes": ("code-server on the Ampere box.\n"
              "env: code-server/.env (PASSWORD) — gitignored\n"
              "Replaces the compose default of 'admin'.\n"
              f"set: {datetime.date.today().isoformat()} by setup-cline.sh"),
    "login": {"uris": [{"uri": f"https://{host}"}],
              "username": "coder", "password": pw},
}]
json.dump({"encrypted": False, "items": items}, open(out, "w"), indent=1, ensure_ascii=False)
print(f"{len(items)} entries -> {out}")
PY
  chmod 600 "$OUT"
else
  say "password unchanged — nothing new to vault"
fi

cat <<EOF

Next:
  1. https://${CLINE_HOST} — sign in with the password above.
  2. Vaultwarden -> Tools -> Import data -> Bitwarden (json) -> ${OUT}
     then: shred -u ${OUT}
  3. The workspace at /home/coder/project is this repo, and after
     ./migrate-to-gitea.sh its 'origin' is Gitea. Codespaces is now optional.
EOF
