#!/usr/bin/env python3
"""Monitor i pavarur i shërbimeve — rri në `watchtower`, JO në Ampere.

Kjo është e gjithë poenta e kutisë. Një monitor që rri në të njëjtën makinë
me atë që monitoron nuk vlen asgjë: kur bie makina, bie edhe monitori, dhe
askush s'merr vesh. `watchtower` është një VM tjetër, në një shape tjetër —
prandaj një Ampere i vdekur ende raportohet.

Pa Docker, pa Node, pa varësi jashtë bibliotekës standarde. Kutia ka 946MB
dhe kjo është zgjedhje, jo kursim: Uptime Kuma (Node) ha ~200MB dhe demoni i
Docker-it edhe ~100MB, dhe një monitor që vetë lëngon nga mungesa e memories
është pikërisht ajo që s'duhet. Ky skript ha ~15MB dhe zgjohet çdo 5 minuta.

Njofton VETËM kur gjendja NDRYSHON (lart→poshtë ose poshtë→lart). Një email
çdo 5 minuta për të njëjtin dështim është mënyra më e sigurt për ta mësuar
veten t'i shpërfillësh alarmet.
"""

from __future__ import annotations

import json
import os
import smtplib
import socket
import ssl
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from email.message import EmailMessage

STATE_FILE = "/var/lib/watchtower/state.json"
CONFIG_FILE = "/etc/watchtower/targets.json"
LOG_FILE = "/var/log/watchtower.log"

TIMEOUT = 20
# Sa dështime radhazi para se ta shpallim poshtë. 2 e jo 1: një timeout i
# vetëm rrjeti është zhurmë, jo incident.
FAIL_THRESHOLD = 2


def log(msg: str) -> None:
    line = f"{datetime.now(timezone.utc).isoformat(timespec='seconds')} {msg}"
    print(line, flush=True)
    try:
        with open(LOG_FILE, "a") as f:
            f.write(line + "\n")
    except OSError:
        pass


def load(path: str, default):
    try:
        with open(path) as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError):
        return default


def save_state(state: dict) -> None:
    os.makedirs(os.path.dirname(STATE_FILE), exist_ok=True)
    tmp = STATE_FILE + ".tmp"
    with open(tmp, "w") as f:
        json.dump(state, f, indent=1)
    os.replace(tmp, STATE_FILE)      # atomik: s'lë gjendje gjysmake pas rënieje


def check_http(target: dict) -> tuple[bool, str]:
    url = target["url"]
    want = target.get("expect_status", 200)
    req = urllib.request.Request(url, headers={"User-Agent": "watchtower/1.0"})
    # Disa endpoint-e janë me certifikatë të brendshme; lejo shmangie kur kërkohet.
    ctx = ssl._create_unverified_context() if target.get("insecure") else None
    t0 = time.time()
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT, context=ctx) as r:
            ms = int((time.time() - t0) * 1000)
            if r.status != want:
                return False, f"HTTP {r.status} (pritej {want})"
            if (needle := target.get("expect_text")):
                body = r.read(60000).decode("utf-8", "replace")
                if needle not in body:
                    return False, f"teksti '{needle}' mungon në përgjigje"
            return True, f"HTTP {r.status} · {ms}ms"
    except urllib.error.HTTPError as e:
        return (True, f"HTTP {e.code} · siç pritej") if e.code == want else \
               (False, f"HTTP {e.code} (pritej {want})")
    except (urllib.error.URLError, TimeoutError, socket.timeout, OSError) as e:
        return False, f"i paarritshëm: {str(e)[:90]}"


def check_tcp(target: dict) -> tuple[bool, str]:
    host, port = target["host"], target["port"]
    t0 = time.time()
    try:
        with socket.create_connection((host, port), timeout=TIMEOUT):
            return True, f"porta e hapur · {int((time.time() - t0) * 1000)}ms"
    except (OSError, socket.timeout) as e:
        return False, f"porta e mbyllur: {str(e)[:70]}"


def notify(subject: str, body: str) -> None:
    """Dërgo me Mailcow-in tonë (submission 587).

    Ironi e qëllimshme: alarmi për Ampere-n kalon nëpër Mailcow-in që RRI në
    Ampere. Nëse bie krejt kutia, ky email s'niset dot — prandaj çdo dështim
    dërgimi shkruhet në log, dhe logu është dëshmia e fundit. Për një zinxhir
    alarmi vërtet të pavarur do të duhej një dërgues jashtë Oracle-it.
    """
    host = os.getenv("SMTP_HOST", "mail.spacecode.tech")
    port = int(os.getenv("SMTP_PORT", "587"))
    user = os.getenv("SMTP_USER", "")
    pw = os.getenv("SMTP_PASS", "")
    to = os.getenv("ALERT_TO", "")
    if not (user and pw and to):
        log("ALARM I PADËRGUAR: mungon konfigurimi SMTP")
        return

    msg = EmailMessage()
    msg["From"] = user
    msg["To"] = to
    msg["Subject"] = subject
    msg.set_content(body)
    try:
        with smtplib.SMTP(host, port, timeout=30) as s:
            s.starttls(context=ssl.create_default_context())
            s.login(user, pw)
            s.send_message(msg)
        log(f"alarmi u dërgua: {subject}")
    except Exception as e:
        log(f"DËRGIMI I ALARMIT DËSHTOI ({e}): {subject}")


def main() -> int:
    targets = load(CONFIG_FILE, {}).get("targets", [])
    if not targets:
        log("asnjë objektiv i konfiguruar — dil")
        return 1

    state = load(STATE_FILE, {})
    changes: list[str] = []

    for t in targets:
        name = t["name"]
        ok, detail = check_tcp(t) if t.get("type") == "tcp" else check_http(t)

        prev = state.get(name, {"up": True, "fails": 0})
        fails = 0 if ok else prev.get("fails", 0) + 1
        # Poshtë vetëm pas FAIL_THRESHOLD dështimesh; lart menjëherë sapo kthehet.
        now_up = ok or fails < FAIL_THRESHOLD

        if now_up != prev.get("up", True):
            arrow = "U KTHYE" if now_up else "RA"
            changes.append(f"{arrow}: {name} — {detail}")
            log(f"NDRYSHIM {arrow}: {name} — {detail}")

        state[name] = {
            "up": now_up,
            "fails": fails,
            "last_detail": detail,
            "checked_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        }

    save_state(state)

    if changes:
        down = [n for n, s in state.items() if not s["up"]]
        subject = f"[watchtower] {len(down)} poshtë" if down else "[watchtower] gjithçka u kthye"
        body = "\n".join(changes) + "\n\nGjendja e plotë:\n" + "\n".join(
            f"  {'UP  ' if s['up'] else 'DOWN'} {n} — {s['last_detail']}"
            for n, s in sorted(state.items())
        )
        notify(subject, body)

    up_n = sum(1 for s in state.values() if s["up"])
    log(f"kontrolli përfundoi: {up_n}/{len(state)} lart")
    return 0


if __name__ == "__main__":
    sys.exit(main())
