"""Portali. Vetëm-lexim: nuk ka forma, nuk ka login, nuk ka admin.

Kjo është me qëllim. Përdoruesi kërkoi një portal që nuk e prek kurrë; një
panel administrimi do të ishte pikërisht gjëja që s'duhet të ekzistojë.
Gjithçka që sheh faqja vjen nga cikli i automatizuar.
"""

from __future__ import annotations

import os
from datetime import datetime, timezone

import psycopg2
import psycopg2.extras
from flask import Flask, abort, render_template, request

app = Flask(__name__)
DSN = os.environ["DATABASE_URL"]

LABEL_TEXT = {
    "corroborated":   ("E korroboruar gjerësisht", "good"),
    "supported":      ("E mbështetur", "ok"),
    "single-source":  ("Burim i vetëm", "warn"),
    "uncorroborated": ("E pakorroboruar", "bad"),
    "weak-signals":   ("Sinjale shqetësuese", "bad"),
}


def q(sql: str, args: tuple = ()) -> list[dict]:
    with psycopg2.connect(DSN) as conn:
        with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cur:
            cur.execute(sql, args)
            return cur.fetchall()


@app.template_filter("kur")
def kur(dt: datetime | None) -> str:
    """Kohë relative, shqip."""
    if not dt:
        return "pa datë"
    secs = (datetime.now(timezone.utc) - dt).total_seconds()
    if secs < 3600:
        return f"para {max(1, int(secs // 60))} min"
    if secs < 86400:
        return f"para {int(secs // 3600)} orësh"
    return f"para {int(secs // 86400)} ditësh"


@app.route("/")
def index():
    label = request.args.get("filtri")
    where, args = "", []
    if label in LABEL_TEXT:
        where = "AND v.label = %s"
        args.append(label)

    rows = q(
        f"""SELECT a.id, a.url, a.title, a.published_at, a.summary,
                   s.name AS source_name, s.domain, s.tier,
                   v.score, v.label, v.llm_rationale, v.reasons,
                   (SELECT COUNT(DISTINCT s2.domain)
                      FROM articles a2 JOIN sources s2 ON s2.id = a2.source_id
                     WHERE a2.cluster_id = a.cluster_id) AS corroborators
            FROM articles a
            JOIN sources s ON s.id = a.source_id
            JOIN assessments v ON v.article_id = a.id
            WHERE a.published_at > now() - interval '3 days' {where}
            ORDER BY a.published_at DESC NULLS LAST
            LIMIT 60""",
        tuple(args),
    )

    run = q("""SELECT started_at, finished_at, ok, fetched, scored
               FROM runs ORDER BY id DESC LIMIT 1""")
    stats = q("""SELECT COUNT(*) AS total,
                        COUNT(*) FILTER (WHERE v.score >= 60) AS strong,
                        COUNT(*) FILTER (WHERE v.score < 40) AS weak
                 FROM assessments v JOIN articles a ON a.id = v.article_id
                 WHERE a.published_at > now() - interval '3 days'""")

    return render_template("index.html", articles=rows, labels=LABEL_TEXT,
                           active=label, run=run[0] if run else None,
                           stats=stats[0] if stats else None)


@app.route("/artikulli/<int:aid>")
def article(aid: int):
    rows = q(
        """SELECT a.id, a.url, a.title, a.summary, a.author, a.published_at,
                  a.cluster_id, s.name AS source_name, s.domain, s.tier,
                  s.domain_age_days, s.has_contact_page, s.has_about_page,
                  v.score, v.label, v.components, v.reasons,
                  v.llm_rationale, v.llm_model, v.scored_at
           FROM articles a
           JOIN sources s ON s.id = a.source_id
           JOIN assessments v ON v.article_id = a.id
           WHERE a.id = %s""",
        (aid,),
    )
    if not rows:
        abort(404)
    art = rows[0]

    others = q(
        """SELECT a.id, a.title, a.url, a.published_at, s.name AS source_name,
                  s.tier, v.score
           FROM articles a
           JOIN sources s ON s.id = a.source_id
           LEFT JOIN assessments v ON v.article_id = a.id
           WHERE a.cluster_id = %s AND a.id <> %s
           ORDER BY a.published_at DESC NULLS LAST""",
        (art["cluster_id"], aid),
    )

    return render_template("article.html", a=art, others=others, labels=LABEL_TEXT)


@app.route("/metoda")
def method():
    return render_template("method.html")


@app.route("/healthz")
def healthz():
    try:
        q("SELECT 1")
        return {"ok": True}, 200
    except Exception as e:
        return {"ok": False, "error": str(e)}, 503


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=8000)
