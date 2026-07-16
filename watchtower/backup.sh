#!/usr/bin/env bash
# Rezervë e bazave nga Ampere → watchtower. Tërhiqet, nuk shtyhet.
#
# Drejtimi ka rëndësi dhe është zgjedhje sigurie: watchtower-i hyn te Ampere,
# jo e kundërta. Kështu Ampere-ja NUK ka kredenciale për te rezervat. Nëse
# Ampere-ja komprometohet, sulmuesi s'i fshin dot kopjet — pikërisht skenari
# për të cilin ekzistojnë rezervat.
#
# Rezervat e paverifikuara nuk janë rezerva: çdo dump kontrollohet me gzip -t
# dhe një dump i zbrazët ose i cunguar raportohet si dështim, jo si sukses.

set -uo pipefail

AMPERE="opc@143.47.190.37"
KEY="$HOME/.ssh/ampere.key"
DEST="/var/backups/spacecode"
KEEP_DAYS=14
LOG="/var/log/watchtower-backup.log"

log() { echo "$(date -u +%FT%TZ) $*" | tee -a "$LOG"; }

mkdir -p "$DEST"
STAMP=$(date -u +%Y%m%d-%H%M)
FAILED=0

# emër_i_kopjes:kontejner:përdorues:bazë
#
# `kosovapos` është baza e dyqanit (jo "pos" — emri i kontejnerit gënjen).
# `pos_control` është regjistri shumë-biznesor: pa të, `kosovapos` vetëm nuk
# rikthehet dot në një sistem që di ç'biznes është.
DBS=(
  "kosovapos:pos-blazor-db:pos:kosovapos"
  "pos_control:pos-blazor-db:pos:pos_control"
  "news:news-db:news:news"
)

for spec in "${DBS[@]}"; do
  IFS=: read -r label container user db <<< "$spec"
  out="$DEST/${label}-${STAMP}.sql.gz"

  if ! ssh -i "$KEY" -o StrictHostKeyChecking=no -o ConnectTimeout=20 "$AMPERE" \
        "sudo docker exec $container pg_dump -U $user -d $db" 2>>"$LOG" | gzip > "$out"; then
    log "DËSHTOI dump: $label"
    rm -f "$out"; FAILED=1; continue
  fi

  # Një dump i cunguar është më i rrezikshëm se asnjë: duket si rezervë.
  if ! gzip -t "$out" 2>/dev/null; then
    log "DËSHTOI (gzip i prishur): $label"
    rm -f "$out"; FAILED=1; continue
  fi

  # Kontrolli i plotësisë bëhet me markerin e mbylljes së pg_dump, JO me
  # madhësinë: `pos_control` mban një biznes dhe zë 1.6KB krejt ligjërisht —
  # një prag madhësie do ta shpallte të prishur çdo natë dhe do ta FSHINTE.
  #
  # tail -12, jo -3: pg_dump ≥16 shton rreshta `\unrestrict <token>` PAS
  # markerit, ndaj ai s'është më i fundit. Me -3 çdo rezervë do të dështonte.
  if ! zcat "$out" 2>/dev/null | tail -12 | grep -q "PostgreSQL database dump complete"; then
    log "DËSHTOI (dump i cunguar — mungon markeri i mbylljes): $label"
    rm -f "$out"; FAILED=1; continue
  fi

  log "OK $label — $(numfmt --to=iec "$(stat -c%s "$out")")"
done

# Pastro të vjetrat vetëm PASI të ketë pasur sukses: një dështim s'duhet ta
# lërë kutinë pa asnjë kopje të vlefshme.
if [ "$FAILED" -eq 0 ]; then
  find "$DEST" -name "*.sql.gz" -mtime +$KEEP_DAYS -delete 2>/dev/null
fi

log "gjendja: $(ls -1 "$DEST"/*.sql.gz 2>/dev/null | wc -l) kopje, $(du -sh "$DEST" 2>/dev/null | cut -f1) gjithsej"
exit $FAILED
