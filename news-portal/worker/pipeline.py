"""Cikli i automatizuar: merr → grupo → vlerëso → ruaj.

Nis nga cron brenda kontejnerit. Askush nuk e prek portalin me dorë.
Çdo hap është idempotent — një ekzekutim i dyfishtë nuk prodhon dublikatë,
sepse `articles.url` është UNIQUE dhe vlerësimet janë UPSERT mbi article_id.
"""

from __future__ import annotations

import json
import logging
import os
import re
import ssl
import sys
import time
from datetime import datetime, timedelta, timezone
from urllib.parse import urlparse

import feedparser
import numpy as np
import psycopg2
import psycopg2.extras
import requests
import trafilatura
import yaml
from bs4 import BeautifulSoup

import llm
import score as scoring

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)-7s %(message)s",
    stream=sys.stdout,
)
log = logging.getLogger("pipeline")

DSN = os.environ["DATABASE_URL"]
HERE = os.path.dirname(os.path.abspath(__file__))
UA = "SpaceNews/1.0 (+https://lajme.spacecode.tech; verifikim burimesh)"

# Sa i vjetër mund të jetë një artikull që ende e marrim parasysh.
MAX_AGE = timedelta(days=int(os.getenv("MAX_ARTICLE_AGE_DAYS", "3")))
# Sa i ngjashëm duhet të jetë një titull që të bjerë në të njëjtin cluster.
SIMILARITY_THRESHOLD = float(os.getenv("SIMILARITY_THRESHOLD", "0.42"))
# Prag kosinusi për bge-m3. I kalibruar te tools/calibrate.py mbi çifte reale
# shqip↔anglisht: nën ~0.70 fillojnë të bashkohen ngjarje të ndryshme që
# ndajnë vetëm temën (dy histori të pallidhura për Kosovën), mbi ~0.80 humbet
# përkthimi i lirshëm. Ngrije nëse sheh cluster-a të përzier.
EMBED_THRESHOLD = float(os.getenv("EMBED_THRESHOLD", "0.75"))


# --------------------------------------------------------------------------
# DB
# --------------------------------------------------------------------------

def connect(retries: int = 30):
    """Postgres mund të mos jetë gati kur worker-i niset."""
    for i in range(retries):
        try:
            return psycopg2.connect(DSN)
        except psycopg2.OperationalError:
            if i == retries - 1:
                raise
            time.sleep(2)


def init_schema(conn) -> None:
    with open(os.path.join(HERE, "schema.sql")) as f:
        with conn.cursor() as cur:
            cur.execute(f.read())
    conn.commit()


def sync_sources(conn) -> None:
    with open(os.path.join(HERE, "feeds.yml")) as f:
        cfg = yaml.safe_load(f)

    with conn.cursor() as cur:
        for s in cfg["sources"]:
            domain = urlparse(s["url"]).netloc.lower().removeprefix("www.")
            cur.execute(
                """INSERT INTO sources (domain, name, feed_url, tier, lang)
                   VALUES (%s, %s, %s, %s, %s)
                   ON CONFLICT (domain) DO UPDATE
                     SET name = EXCLUDED.name,
                         feed_url = EXCLUDED.feed_url,
                         tier = EXCLUDED.tier,
                         lang = EXCLUDED.lang""",
                (domain, s["name"], s["url"], s["tier"], s.get("lang", "en")),
            )
    conn.commit()


# --------------------------------------------------------------------------
# Sinjalet e burimit (të ngadalta, rifreskohen çdo 30 ditë)
# --------------------------------------------------------------------------

def rdap_domain_age(domain: str) -> int | None:
    """Mosha e domain-it nga RDAP. Falas, pa çelës, standard IETF."""
    try:
        r = requests.get(f"https://rdap.org/domain/{domain}",
                         timeout=15, headers={"User-Agent": UA})
        if r.status_code != 200:
            return None
        for ev in r.json().get("events", []):
            if ev.get("eventAction") == "registration":
                reg = datetime.fromisoformat(ev["eventDate"].replace("Z", "+00:00"))
                return (datetime.now(timezone.utc) - reg).days
    except Exception as e:
        log.debug("RDAP dështoi për %s: %s", domain, e)
    return None


def transparency_signals(domain: str) -> tuple[bool | None, bool | None, bool | None]:
    """A ka faqja kontakt/about, dhe a është HTTPS-i i vlefshëm?"""
    try:
        r = requests.get(f"https://{domain}", timeout=15,
                         headers={"User-Agent": UA}, allow_redirects=True)
        https_ok = True
    except requests.exceptions.SSLError:
        return None, None, False
    except Exception:
        return None, None, None

    try:
        soup = BeautifulSoup(r.text, "html.parser")
        hrefs = " ".join(
            (a.get("href") or "") + " " + a.get_text(" ")
            for a in soup.find_all("a")
        ).lower()
        contact = any(k in hrefs for k in
                      ("contact", "kontakt", "impressum", "na-kontaktoni"))
        about = any(k in hrefs for k in
                    ("about", "rreth", "kush-jemi", "redaksia", "impressum"))
        return contact, about, https_ok
    except Exception:
        return None, None, https_ok


def refresh_source_signals(conn) -> None:
    with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cur:
        cur.execute(
            """SELECT id, domain FROM sources
               WHERE signals_checked_at IS NULL
                  OR signals_checked_at < now() - interval '30 days'
               LIMIT 5"""          # pak për ekzekutim; s'ka nxitim, nuk ndryshojnë
        )
        rows = cur.fetchall()

    for row in rows:
        age = rdap_domain_age(row["domain"])
        contact, about, https_ok = transparency_signals(row["domain"])
        with conn.cursor() as cur:
            cur.execute(
                """UPDATE sources SET domain_age_days = %s, has_contact_page = %s,
                       has_about_page = %s, https_valid = %s, signals_checked_at = now()
                   WHERE id = %s""",
                (age, contact, about, https_ok, row["id"]),
            )
        conn.commit()
        log.info("Sinjalet e burimit u rifreskuan: %s (mosha=%s ditë)", row["domain"], age)


# --------------------------------------------------------------------------
# Marrja
# --------------------------------------------------------------------------

def parse_date(entry) -> datetime | None:
    for key in ("published_parsed", "updated_parsed"):
        t = entry.get(key)
        if t:
            return datetime(*t[:6], tzinfo=timezone.utc)
    return None


def extract_body(url: str) -> tuple[str | None, int]:
    """Teksti kryesor + numri i lidhjeve dalëse (citime drejt burimeve primare)."""
    try:
        html = trafilatura.fetch_url(url)
        if not html:
            return None, 0
        body = trafilatura.extract(html, include_comments=False, include_tables=False)

        host = urlparse(url).netloc.lower().removeprefix("www.")
        soup = BeautifulSoup(html, "html.parser")
        outbound = 0
        for a in soup.find_all("a", href=True):
            h = a["href"]
            if h.startswith("http"):
                other = urlparse(h).netloc.lower().removeprefix("www.")
                if other and other != host:
                    outbound += 1
        return body, outbound
    except Exception as e:
        log.debug("Nxjerrja e tekstit dështoi për %s: %s", url, e)
        return None, 0


def fetch_all(conn, errors: list) -> int:
    with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cur:
        cur.execute("SELECT id, domain, name, feed_url, lang FROM sources WHERE feed_url IS NOT NULL")
        sources = cur.fetchall()

    cutoff = datetime.now(timezone.utc) - MAX_AGE
    added = 0

    for src in sources:
        try:
            feed = feedparser.parse(src["feed_url"], agent=UA)
            if feed.bozo and not feed.entries:
                errors.append({"source": src["name"], "error": str(feed.bozo_exception)[:200]})
                log.warning("Feed i palexueshëm: %s (%s)", src["name"], feed.bozo_exception)
                continue

            for entry in feed.entries[:25]:
                url = entry.get("link")
                title = (entry.get("title") or "").strip()
                if not url or not title:
                    continue

                published = parse_date(entry)
                if published and published < cutoff:
                    continue

                with conn.cursor() as cur:
                    cur.execute("SELECT 1 FROM articles WHERE url = %s", (url,))
                    if cur.fetchone():
                        continue

                body, outbound = extract_body(url)
                summary = re.sub(r"<[^>]+>", "", entry.get("summary", ""))[:600] or None
                author = entry.get("author") or None

                with conn.cursor() as cur:
                    cur.execute(
                        """INSERT INTO articles
                             (source_id, url, title, summary, body, author,
                              published_at, fingerprint, lang)
                           VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s)
                           ON CONFLICT (url) DO NOTHING
                           RETURNING id""",
                        (src["id"], url, title, summary, body, author, published,
                         scoring.fingerprint(title), src["lang"]),
                    )
                    if cur.fetchone():
                        added += 1
                        # Numri i lidhjeve dalëse s'ka kolonë të vetën; hyn te
                        # vlerësimi përmes rillogaritjes në score_article().
                conn.commit()
                time.sleep(0.4)      # sjellje e mirë ndaj serverëve të tjerëve

            log.info("U morën lajmet nga %s", src["name"])
        except Exception as e:
            errors.append({"source": src["name"], "error": str(e)[:200]})
            log.exception("Marrja dështoi për %s", src["name"])

    return added


# --------------------------------------------------------------------------
# Grupimi i tregimeve
# --------------------------------------------------------------------------

def embed_missing(conn) -> int:
    """Vektorizo titujt e rinj. Kalohet në heshtje nëse Ollama s'është gati."""
    if not llm.ensure_model(llm.EMBED_MODEL):
        log.warning("Modeli i embedding-ut mungon — grupimi bie te krahasimi me fjalë.")
        return 0

    with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cur:
        cur.execute(
            """SELECT id, title FROM articles
               WHERE embedding IS NULL
                 AND fetched_at > now() - interval '48 hours'"""
        )
        rows = cur.fetchall()

    done = 0
    for r in rows:
        vec = llm.embed(r["title"])
        if not vec:
            continue
        with conn.cursor() as cur:
            cur.execute("UPDATE articles SET embedding = %s WHERE id = %s",
                        (json.dumps(vec), r["id"]))
        conn.commit()
        done += 1

    log.info("U vektorizuan %d tituj", done)
    return done


def cluster_recent(conn) -> None:
    """Bashko artikujt që mbulojnë të njëjtën ngjarje.

    Dy mënyra krahasimi, sipas asaj që kemi në dorë:
      - vektorë bge-m3 (kosinus) — funksionon edhe mes shqipes dhe anglishtes
      - mbivendosje fjalësh — rezervë kur Ollama s'ka përgjigjur

    O(n²) mbi një dritare 48-orëshe. Me disa qindra artikuj kjo është
    milisekonda; nëse portali rritet 10×, kjo bëhet vendi i parë që dhemb.
    """
    with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cur:
        cur.execute(
            """SELECT a.id, a.title, a.fingerprint, a.cluster_id, a.embedding
               FROM articles a
               WHERE a.fetched_at > now() - interval '48 hours'
               ORDER BY a.published_at DESC NULLS LAST"""
        )
        arts = cur.fetchall()

    def vec(a) -> np.ndarray | None:
        if not a["embedding"]:
            return None
        v = np.asarray(a["embedding"], dtype=np.float32)
        n = np.linalg.norm(v)
        return v / n if n else None

    # (embedding, fingerprint, cluster_id)
    assigned: list[tuple[np.ndarray | None, str, int]] = []

    for art in arts:
        v = vec(art)

        if art["cluster_id"]:
            assigned.append((v, art["fingerprint"], art["cluster_id"]))
            continue

        match = None
        for other_v, other_fp, cid in assigned:
            if v is not None and other_v is not None:
                if float(v @ other_v) >= EMBED_THRESHOLD:
                    match = cid
                    break
            elif scoring.similarity(art["fingerprint"], other_fp) >= SIMILARITY_THRESHOLD:
                match = cid
                break

        with conn.cursor() as cur:
            if match is None:
                cur.execute(
                    "INSERT INTO clusters (lead_title) VALUES (%s) RETURNING id",
                    (art["title"],),
                )
                match = cur.fetchone()[0]
            else:
                cur.execute("UPDATE clusters SET last_seen_at = now() WHERE id = %s", (match,))
            cur.execute("UPDATE articles SET cluster_id = %s WHERE id = %s", (match, art["id"]))
        conn.commit()
        assigned.append((v, art["fingerprint"], match))


# --------------------------------------------------------------------------
# Vlerësimi
# --------------------------------------------------------------------------

def score_pending(conn, use_llm: bool, errors: list) -> int:
    with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cur:
        cur.execute(
            """SELECT a.id, a.url, a.title, a.body, a.author, a.published_at,
                      a.lang, a.cluster_id,
                      s.name AS source_name, s.tier, s.domain,
                      s.domain_age_days, s.has_contact_page, s.has_about_page,
                      s.https_valid
               FROM articles a
               JOIN sources s ON s.id = a.source_id
               LEFT JOIN assessments v ON v.article_id = a.id
               WHERE v.id IS NULL
                 AND a.fetched_at > now() - interval '7 days'
               ORDER BY a.published_at DESC NULLS LAST"""
        )
        pending = cur.fetchall()

    log.info("%d artikuj presin vlerësim", len(pending))
    done = 0

    for art in pending:
        try:
            # Korroborimi numëron DOMAIN-E të pavarur, jo artikuj: dhjetë
            # ripostime brenda të njëjtit portal nuk janë dhjetë dëshmi.
            with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cur:
                cur.execute(
                    """SELECT COUNT(DISTINCT s.domain) AS domains,
                              array_agg(DISTINCT s.tier) AS tiers
                       FROM articles a JOIN sources s ON s.id = a.source_id
                       WHERE a.cluster_id = %s""",
                    (art["cluster_id"],),
                )
                cl = cur.fetchone()

            _, outbound = extract_body(art["url"]) if art["body"] is None else (None, 0)
            if art["body"]:
                # Rillogaritje e lirë: numëro citimet nga teksti i ruajtur.
                outbound = len(re.findall(r"https?://", art["body"] or ""))

            result = scoring.assess(
                independent_domains=cl["domains"] or 1,
                cluster_tiers=cl["tiers"] or [art["tier"]],
                tier=art["tier"],
                domain_age_days=art["domain_age_days"],
                has_contact=art["has_contact_page"],
                has_about=art["has_about_page"],
                https_valid=art["https_valid"],
                author=art["author"],
                published_at=art["published_at"],
                title=art["title"],
                body=art["body"],
                outbound_links=outbound,
                lang=art["lang"] or "en",
            )

            narrative, model = (None, None)
            if use_llm:
                narrative, model = llm.rationale(
                    title=art["title"], source=art["source_name"], tier=art["tier"],
                    score=result.score, label=result.label, reasons=result.reasons,
                )

            with conn.cursor() as cur:
                cur.execute(
                    """INSERT INTO assessments
                         (article_id, score, label, components, reasons,
                          llm_rationale, llm_model)
                       VALUES (%s,%s,%s,%s,%s,%s,%s)
                       ON CONFLICT (article_id) DO UPDATE
                         SET score = EXCLUDED.score, label = EXCLUDED.label,
                             components = EXCLUDED.components,
                             reasons = EXCLUDED.reasons,
                             llm_rationale = EXCLUDED.llm_rationale,
                             llm_model = EXCLUDED.llm_model,
                             scored_at = now()""",
                    (art["id"], result.score, result.label_key,
                     json.dumps(result.components), json.dumps(result.reasons),
                     narrative, model),
                )
            conn.commit()
            done += 1
        except Exception as e:
            errors.append({"article": art["url"][:120], "error": str(e)[:200]})
            log.exception("Vlerësimi dështoi për %s", art["url"])
            conn.rollback()

    return done


# --------------------------------------------------------------------------

def prune(conn) -> None:
    """Mbaj 60 ditë. Pa këtë, disku mbushet dhe portali bie në heshtje."""
    with conn.cursor() as cur:
        cur.execute("DELETE FROM articles WHERE fetched_at < now() - interval '60 days'")
        cur.execute(
            """DELETE FROM clusters c WHERE NOT EXISTS
               (SELECT 1 FROM articles a WHERE a.cluster_id = c.id)"""
        )
    conn.commit()


def main() -> int:
    conn = connect()
    init_schema(conn)
    sync_sources(conn)

    with conn.cursor() as cur:
        cur.execute("INSERT INTO runs DEFAULT VALUES RETURNING id")
        run_id = cur.fetchone()[0]
    conn.commit()

    errors: list = []
    ok = True
    fetched = scored = 0

    try:
        refresh_source_signals(conn)
        fetched = fetch_all(conn, errors)
        embed_missing(conn)
        cluster_recent(conn)

        use_llm = os.getenv("USE_LLM", "1") == "1" and llm.ensure_model()
        if not use_llm:
            log.warning("LLM-ja nuk është e disponueshme — vlerësimi vazhdon pa narrativë.")

        scored = score_pending(conn, use_llm, errors)
        prune(conn)
    except Exception as e:
        ok = False
        errors.append({"fatal": str(e)[:400]})
        log.exception("Cikli dështoi")
    finally:
        with conn.cursor() as cur:
            cur.execute(
                """UPDATE runs SET finished_at = now(), ok = %s, fetched = %s,
                       scored = %s, errors = %s WHERE id = %s""",
                (ok and not errors, fetched, scored, json.dumps(errors[:50]), run_id),
            )
        conn.commit()
        conn.close()

    log.info("Cikli përfundoi: %d të marrë, %d të vlerësuar, %d gabime",
             fetched, scored, len(errors))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
