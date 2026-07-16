#!/usr/bin/env bash
# Ngre watchtower-in mbi një OL9 të pastër. Idempotent — riekzekutimi s'prish gjë.
#
# Pa Docker me qëllim: shih komentin te monitor.py. Kutia ka 946MB dhe
# gjithçka këtu është bibliotekë standarde e Python-it.

set -euo pipefail

: "${SMTP_USER:?SMTP_USER mungon}"
: "${SMTP_PASS:?SMTP_PASS mungon}"
: "${ALERT_TO:?ALERT_TO mungon}"

sudo mkdir -p /opt/watchtower /etc/watchtower /var/lib/watchtower /var/backups/spacecode
sudo cp monitor.py /opt/watchtower/monitor.py
sudo cp backup.sh  /opt/watchtower/backup.sh
sudo cp targets.json /etc/watchtower/targets.json
sudo chmod +x /opt/watchtower/monitor.py /opt/watchtower/backup.sh

# Kredencialet jashtë git-it dhe jasht syve: vetëm root.
sudo tee /etc/watchtower/env >/dev/null <<EOF
SMTP_HOST=mail.spacecode.tech
SMTP_PORT=587
SMTP_USER=${SMTP_USER}
SMTP_PASS=${SMTP_PASS}
ALERT_TO=${ALERT_TO}
EOF
sudo chmod 600 /etc/watchtower/env

sudo tee /etc/systemd/system/watchtower.service >/dev/null <<'EOF'
[Unit]
Description=Watchtower — kontroll i shërbimeve
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
EnvironmentFile=/etc/watchtower/env
ExecStart=/usr/bin/python3 /opt/watchtower/monitor.py
# Kutia ka 946MB; mos e lër kurrë monitorin ta rrezikojë vetë kutinë.
MemoryMax=128M
Nice=10
EOF

sudo tee /etc/systemd/system/watchtower.timer >/dev/null <<'EOF'
[Unit]
Description=Kontrollo shërbimet çdo 5 minuta

[Timer]
OnBootSec=2min
OnUnitActiveSec=5min
# Pa këtë, të gjitha kontrollet do të binin në sekondën e njëjtë të çdo cikli.
RandomizedDelaySec=20s
Persistent=true

[Install]
WantedBy=timers.target
EOF

sudo tee /etc/systemd/system/watchtower-backup.service >/dev/null <<'EOF'
[Unit]
Description=Tërhiq rezervat e bazave nga Ampere
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
ExecStart=/opt/watchtower/backup.sh
MemoryMax=256M
Nice=15
EOF

sudo tee /etc/systemd/system/watchtower-backup.timer >/dev/null <<'EOF'
[Unit]
Description=Rezervë e bazave çdo natë

[Timer]
OnCalendar=*-*-* 03:30:00
Persistent=true

[Install]
WantedBy=timers.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable --now watchtower.timer watchtower-backup.timer

echo "watchtower u instalua. Kontroll i menjëhershëm:"
sudo systemctl start watchtower.service
sudo systemctl list-timers 'watchtower*' --no-pager | head -4
