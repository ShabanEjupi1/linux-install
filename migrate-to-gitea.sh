#!/usr/bin/env bash
# Phase 1 — make self-hosted Gitea the primary remote for linux-install.
#
# Run this ON THE BOX:
#   cd /home/shabanejupi/linux-install && ./migrate-to-gitea.sh
#
# What it does:
#   1. brings up Gitea (gitea/docker-compose.yml, 127.0.0.1:8140)
#   2. nginx vhost for git.spacecode.tech -> 8140
#   3. creates the admin user + an API token
#   4. tells Gitea to migrate the repo from GitHub (server-side clone: full
#      history, no keys, nothing streamed through this shell)
#   5. repoints this checkout's `origin` at Gitea, keeps GitHub as `github`
#   6. writes vault-additions-gitea.json for import into Vaultwarden
#
# Idempotent: safe to re-run. Every step checks for its own result first.
#
# NOTE: the migration copies GitHub's history verbatim, so the leaked
# Vaultwarden ADMIN_TOKEN comes across with it. Gitea is private and
# sign-in-gated, but the token is already public on GitHub and rotating it is
# the only thing that actually fixes that. See the exit notes.
set -euo pipefail

cd "$(dirname "$0")"

GITEA_HOST="git.spacecode.tech"
GITEA_LOCAL="http://127.0.0.1:8140"
GITEA_USER="shabanejupi"
GITEA_EMAIL="shaban.ejupi@student.uni-pr.edu"
GH_REPO="https://github.com/ShabanEjupi/linux-install"
REPO_NAME="linux-install"
OUT="vault-additions-gitea.json"

say()  { printf '\033[1;36m==>\033[0m %s\n' "$1"; }
warn() { printf '\033[1;33m !! \033[0m%s\n' "$1"; }
die()  { printf '\033[1;31mERR\033[0m %s\n' "$1" >&2; exit 1; }
gen()  { openssl rand -base64 24 | tr -d '/+=' | cut -c1-24; }

[ -d gitea ] || die "run this from the repo root (no gitea/ dir here)"
command -v docker >/dev/null || die "docker not found"

# ---------------------------------------------------------------------------
# 1. Gitea up
# ---------------------------------------------------------------------------
say "starting Gitea"
( cd gitea && docker compose up -d )

say "waiting for Gitea to answer on ${GITEA_LOCAL}"
for i in $(seq 1 60); do
  if curl -fsS "${GITEA_LOCAL}/api/healthz" >/dev/null 2>&1; then break; fi
  [ "$i" = 60 ] && die "Gitea did not come up — check: docker logs gitea"
  sleep 2
done
say "Gitea is up"

# ---------------------------------------------------------------------------
# 2. nginx vhost.
#    client_max_body_size is the one that bites: nginx defaults to 1m, and the
#    first push of this repo is far bigger than that (there is a 42MB .doc in
#    here). Without this the push dies with a opaque HTTP 413.
# ---------------------------------------------------------------------------
if [ -d /etc/nginx/sites-available ]; then
  NGINX_CONF=/etc/nginx/sites-available/${GITEA_HOST}
  NGINX_LINK=/etc/nginx/sites-enabled/${GITEA_HOST}
elif [ -d /etc/nginx/conf.d ]; then
  NGINX_CONF=/etc/nginx/conf.d/${GITEA_HOST}.conf
  NGINX_LINK=""
else
  NGINX_CONF=""
  warn "no nginx config dir found — skipping vhost; add it by hand"
fi

if [ -n "$NGINX_CONF" ] && [ ! -f "$NGINX_CONF" ]; then
  say "writing nginx vhost $NGINX_CONF"
  sudo tee "$NGINX_CONF" >/dev/null <<EOF
# git.spacecode.tech -> Gitea. Reached via the 'ampere' Cloudflare tunnel's
# *.spacecode.tech rule, which lands on this nginx.
server {
    listen 80;
    server_name ${GITEA_HOST};

    # Git pushes are large. nginx's 1m default rejects the initial push with a
    # 413 that git reports as an unhelpful RPC failure.
    client_max_body_size 0;

    location / {
        proxy_pass http://127.0.0.1:8140;
        proxy_set_header Host              \$host;
        proxy_set_header X-Real-IP         \$remote_addr;
        proxy_set_header X-Forwarded-For   \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
        proxy_read_timeout 300s;
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
# 3. admin user + token
# ---------------------------------------------------------------------------
gitea_cli() { docker exec -u git gitea gitea "$@"; }

ADMIN_PW=""
if gitea_cli admin user list 2>/dev/null | awk 'NR>1{print $2}' | grep -qx "$GITEA_USER"; then
  say "admin user '${GITEA_USER}' already exists — not touching the password"
else
  ADMIN_PW="$(gen)"
  say "creating admin user '${GITEA_USER}'"
  gitea_cli admin user create \
    --username "$GITEA_USER" \
    --password "$ADMIN_PW" \
    --email "$GITEA_EMAIL" \
    --admin --must-change-password=false >/dev/null
fi

say "minting an API token"
TOKEN="$(gitea_cli admin user generate-access-token \
  --username "$GITEA_USER" \
  --token-name "migrate-$(date +%s)" \
  --scopes write:repository,write:user \
  --raw)" || die "could not mint a token"

api() {
  local method="$1" path="$2"; shift 2
  curl -fsS -X "$method" "${GITEA_LOCAL}/api/v1${path}" \
    -H "Authorization: token ${TOKEN}" \
    -H "Content-Type: application/json" "$@"
}

# ---------------------------------------------------------------------------
# 4. migrate from GitHub, server-side
# ---------------------------------------------------------------------------
if api GET "/repos/${GITEA_USER}/${REPO_NAME}" >/dev/null 2>&1; then
  say "repo already on Gitea — skipping migration"
else
  say "migrating ${GH_REPO} into Gitea (full history; this takes a minute)"
  api POST /repos/migrate -d "$(python3 - "$GH_REPO" "$GITEA_USER" "$REPO_NAME" <<'PY'
import json, sys
print(json.dumps({
    "clone_addr": sys.argv[1],
    "repo_owner": sys.argv[2],
    "repo_name":  sys.argv[3],
    "service":    "git",
    "mirror":     False,   # a real move, not a mirror that keeps pulling
    "private":    True,
    "description": "Self-hosted primary. Moved off public GitHub 2026-07-16.",
}))
PY
)" >/dev/null || die "migration call failed"
  say "migrated"
fi

# ---------------------------------------------------------------------------
# 5. repoint this checkout
# ---------------------------------------------------------------------------
GITEA_REMOTE="https://${GITEA_HOST}/${GITEA_USER}/${REPO_NAME}.git"

if git remote | grep -qx github; then
  say "'github' remote already set"
else
  say "keeping GitHub as the 'github' remote"
  git remote add github "$GH_REPO" 2>/dev/null || true
fi
say "pointing 'origin' at Gitea"
git remote set-url origin "$GITEA_REMOTE"

cat <<EOF

  origin -> ${GITEA_REMOTE}
  github -> ${GH_REPO}

  Gitea pushes over HTTPS want the token as the password (git-over-SSH does
  not survive the Cloudflare tunnel). To stop it asking every push:
      git config credential.helper store
  then push once and enter: username ${GITEA_USER}, password <the token below>.

EOF

# ---------------------------------------------------------------------------
# 6. record into the vault
# ---------------------------------------------------------------------------
say "writing $OUT"
python3 - "$OUT" "$GITEA_USER" "$TOKEN" "${ADMIN_PW:-}" "$GITEA_HOST" <<'PY'
import json, sys, datetime
out, user, token, pw, host = sys.argv[1:6]
today = datetime.date.today().isoformat()
items = [{
    "type": 2, "name": "Gitea API token (git push)",
    "folderId": "a6717076-551f-4060-bd79-1e01d933d28e",
    "favorite": False,
    "notes": (f"https://{host}\nuser: {user}\ntoken: {token}\n"
              f"scopes: write:repository, write:user\n"
              f"use as the HTTPS password when pushing\ncreated: {today}"),
    "secureNote": {"type": 0},
}]
if pw:
    items.append({
        "type": 1, "name": "Gitea admin",
        "folderId": "a6717076-551f-4060-bd79-1e01d933d28e",
        "favorite": False,
        "notes": f"Self-hosted Gitea admin. Created {today} by migrate-to-gitea.sh",
        "login": {"uris": [{"uri": f"https://{host}"}],
                  "username": user, "password": pw},
    })
json.dump({"encrypted": False, "items": items}, open(out, "w"), indent=1, ensure_ascii=False)
print(f"{len(items)} entries -> {out}")
PY
chmod 600 "$OUT"

cat <<EOF

Next:
  1. Open https://${GITEA_HOST} and confirm the repo and its history are there.
  2. Vaultwarden -> Tools -> Import data -> Bitwarden (json) -> ${OUT}
     then: shred -u ${OUT}
  3. Only once you have verified Gitea has everything:
     make the GitHub repo private (Settings -> General -> Danger Zone).
     It has no forks, stars or Pages, so nothing downstream breaks.

  Going private does NOT unleak the ADMIN_TOKEN — it is already public and
  scraped. Rotate it on this box; that is the only real fix:
       openssl rand -base64 48   -> vaultwarden/.env  (ADMIN_TOKEN)
       set SIGNUPS_ALLOWED=false while you are in there
       cd vaultwarden && docker compose up -d --force-recreate

  Then Phase 2: ./setup-cline.sh
EOF
