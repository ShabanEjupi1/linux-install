#!/usr/bin/env bash
# Rotate the credentials that never made it into the vault, and emit them in
# Bitwarden import format so they land in Vaultwarden instead of a text file
# that the next reset eats.
#
# Run this ON THE BOX (it talks to running containers):
#   cd /home/shabanejupi/linux-install && ./rotate-credentials.sh
#
# Covers the four that rotate cleanly. Jitsi (prosody re-registration) and the
# Supabase service_role key (derived from JWT_SECRET — rotating it invalidates
# ANON_KEY and every app holding either) are deliberately NOT here: they need a
# maintenance window, not a script run between two other things.
#
# Output: vault-additions.json — gitignored, import it into Vaultwarden, then
# delete it. It holds live secrets in plaintext.
set -euo pipefail

cd "$(dirname "$0")"
OUT="vault-additions.json"

gen() { openssl rand -base64 24 | tr -d '/+=' | cut -c1-24; }
say() { printf '\033[1;36m==>\033[0m %s\n' "$1"; }
warn() { printf '\033[1;33m !! \033[0m%s\n' "$1"; }

# name|compose dir|db container|wp container|site url
SITES="
enisi|wordpress-enisi|wp_enisi_db|wp_enisi|https://enisi.tech/wp-admin
halalbank|wordpress-halalbank|wp_halalbank_db|wp_halalbank|https://halalbankkosova.me/wp-admin
shabanejupi|wordpress-shabanejupi|wp_shabanejupi_db|wp_shabanejupi|https://shabanejupi.tech/wp-admin
"

# Accumulates JSON objects; joined at the end.
ENTRIES=()

json_note() {
  # $1 name, $2 folder id, $3 notes body
  python3 - "$1" "$2" "$3" <<'PY'
import json, sys
print(json.dumps({
    "type": 2,
    "name": sys.argv[1],
    "folderId": sys.argv[2],
    "favorite": False,
    "notes": sys.argv[3],
    "secureNote": {"type": 0},
}))
PY
}

FOLDER_WEB="a6717076-551f-4060-bd79-1e01d933d28e"   # Web Apps & Admin
FOLDER_DB="63542760-9980-4d69-bd9e-c3210b04a8fb"    # Backend & DB

# ---------------------------------------------------------------------------
# 1 + 2. WordPress DB app-user, and the dashboard admin, per site.
# ---------------------------------------------------------------------------
for row in $SITES; do
  IFS='|' read -r site dir dbc wpc url <<<"$row"
  [ -d "$dir" ] || { warn "$dir missing — skipping $site"; continue; }

  if ! docker ps --format '{{.Names}}' | grep -qx "$dbc"; then
    warn "$dbc not running — skipping $site"
    continue
  fi

  say "$site: rotating WordPress DB app-user"
  root_pw="$(grep -E '^MYSQL_ROOT_PASSWORD=' "$dir/.env" | cut -d= -f2-)"
  db_user="$(grep -E '^MYSQL_USER=' "$dir/.env" | cut -d= -f2- || echo wordpress)"
  db_name="$(grep -E '^MYSQL_DATABASE=' "$dir/.env" | cut -d= -f2- || echo wordpress)"
  db_user="${db_user:-wordpress}"
  db_name="${db_name:-wordpress}"
  new_db_pw="$(gen)"

  # The ALTER and the .env rewrite must both land, or WordPress can't reach its
  # own database. ALTER first: if it fails, set -e stops before .env is touched.
  docker exec -i "$dbc" mysql -uroot -p"$root_pw" -e \
    "ALTER USER '${db_user}'@'%' IDENTIFIED BY '${new_db_pw}'; FLUSH PRIVILEGES;"

  sed -i.bak "s|^MYSQL_PASSWORD=.*|MYSQL_PASSWORD=${new_db_pw}|" "$dir/.env"
  ( cd "$dir" && docker compose up -d --force-recreate wordpress )

  ENTRIES+=("$(json_note "WordPress DB app-user — ${site}" "$FOLDER_DB" \
"db: ${db_name}
user: ${db_user}
password: ${new_db_pw}
container: ${dbc}
env: ${dir}/.env (MYSQL_PASSWORD)
rotated: $(date -I) by rotate-credentials.sh")")

  say "$site: rotating WordPress dashboard admin"
  # wordpress:latest ships no wp-cli, so borrow the cli image against the same
  # volumes and network.
  admin_login="$(docker run --rm --network "container:${wpc}" \
    --volumes-from "$wpc" -u 33:33 wordpress:cli \
    wp user list --role=administrator --field=user_login --allow-root 2>/dev/null | head -1 || true)"

  if [ -z "$admin_login" ]; then
    warn "$site: could not read an administrator login — rotate by hand at ${url}"
  else
    new_admin_pw="$(gen)"
    docker run --rm --network "container:${wpc}" --volumes-from "$wpc" -u 33:33 \
      wordpress:cli wp user update "$admin_login" --user_pass="$new_admin_pw" --allow-root >/dev/null
    ENTRIES+=("$(json_note "WordPress admin — ${site}" "$FOLDER_WEB" \
"${url}
user: ${admin_login}
password: ${new_admin_pw}
rotated: $(date -I) by rotate-credentials.sh")")
  fi
done

# ---------------------------------------------------------------------------
# 3. Watchtower's "notification secret" is an SMTP mailbox password. Mailcow
#    owns it, so this script cannot mint it — it only records what you set.
# ---------------------------------------------------------------------------
say "watchtower: SMTP_PASS must be rotated in Mailcow first"
suggested="$(gen)"
cat <<EOF

  Watchtower alerts authenticate as a real Mailcow mailbox. Steps:
    1. Mailcow admin UI -> Mailboxes -> the alert sender -> set password to:
         ${suggested}
    2. Update SMTP_PASS in the watchtower env on this box.
    3. Restart watchtower.

EOF
read -rp "  Did you set that password in Mailcow? [y/N] " ack
if [[ "${ack:-n}" =~ ^[Yy]$ ]]; then
  smtp_user="$(grep -rhE '^SMTP_USER=' watchtower/.env 2>/dev/null | cut -d= -f2- || true)"
  ENTRIES+=("$(json_note "Watchtower alert SMTP" "$FOLDER_WEB" \
"host: mail.spacecode.tech:587
user: ${smtp_user:-<the alert mailbox>}
password: ${suggested}
used by: watchtower/monitor.py notify() via SMTP_PASS
rotated: $(date -I)")")
else
  warn "skipped — watchtower SMTP not recorded"
fi

# ---------------------------------------------------------------------------
# 4. POS platform super-admin. EnsureSeedAdminAsync only seeds when the admin
#    is ABSENT, so changing POS_PLATFORM_ADMIN_PASSWORD in .env does nothing to
#    an account that already exists. This has to go through the UI.
# ---------------------------------------------------------------------------
say "pos-blazor: platform super-admin"
pos_pw="$(gen)"
pos_user="$(grep -E '^POS_PLATFORM_ADMIN_USER=' pos-blazor/.env 2>/dev/null | cut -d= -f2- || echo platform)"
cat <<EOF

  EnsureSeedAdminAsync only seeds when no admin exists — editing .env will NOT
  change the existing account. Sign in at /admin/login and set:
       user: ${pos_user:-platform}
       password: ${pos_pw}
  Then set POS_PLATFORM_ADMIN_PASSWORD in pos-blazor/.env to match, so a
  rebuild onto an empty database seeds the same credential.

EOF
read -rp "  Did you change it in the UI? [y/N] " ack
if [[ "${ack:-n}" =~ ^[Yy]$ ]]; then
  ENTRIES+=("$(json_note "POS platform super-admin" "$FOLDER_WEB" \
"/admin/login
user: ${pos_user:-platform}
password: ${pos_pw}
env: pos-blazor/.env (POS_PLATFORM_ADMIN_PASSWORD) — seed-only, see StartupProvisioning.cs
rotated: $(date -I)")")
else
  warn "skipped — POS super-admin not recorded"
fi

# ---------------------------------------------------------------------------
say "writing $OUT"
python3 - "$OUT" "${ENTRIES[@]}" <<'PY'
import json, sys
out, items = sys.argv[1], [json.loads(a) for a in sys.argv[2:]]
json.dump({"encrypted": False, "items": items}, open(out, "w"), indent=1, ensure_ascii=False)
print(f"{len(items)} entries -> {out}")
PY
chmod 600 "$OUT"

cat <<EOF

Next:
  1. Vaultwarden -> Tools -> Import data -> Bitwarden (json) -> ${OUT}
  2. Verify the entries are there, then: shred -u ${OUT}
  3. Still outstanding, on purpose (not in this script):
       - Jitsi component secrets (prosody re-registration)
       - Supabase service_role / JWT_SECRET (breaks every holder of ANON_KEY)
       - Vaultwarden ADMIN_TOKEN rotation + SIGNUPS_ALLOWED=false
       - Fineract default mifos/password
EOF
