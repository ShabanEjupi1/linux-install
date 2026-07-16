"""Vlerësimi i besueshmërisë.

Ky modul NUK vendos nëse një lajm është i vërtetë. Askush nuk mund ta bëjë
këtë automatikisht, dhe një sistem që pretendon se mundet është më i
rrezikshëm se asnjë sistem fare — sepse gabon me autoritet.

Ajo që mat vërtet:
  - a raportohet e njëjta ngjarje nga redaksi të pavarura (korroborim)
  - sa transparent është burimi (kontakt, about, mosha e domain-it, HTTPS)
  - a i plotëson artikulli normat bazë (autor, datë, citime)
  - a përdor gjuhë manipuluese (sensacionalizëm)

Këto janë tregues të *procesit gazetaresk*, jo të së vërtetës. Një raport i
vetëm, ekskluziv nga një gazetar hetimor do të marrë notë të ulët korroborimi
dhe mund të jetë plotësisht i saktë. Prandaj etiketat flasin për provën në
dorë, kurrë për faktin.
"""

from __future__ import annotations

import math
import re
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any

# Etiketat përshkruajnë sa e mbështetur është një histori, jo a është e vërtetë.
LABELS = [
    (80, "E korroboruar gjerësisht", "corroborated"),
    (60, "E mbështetur", "supported"),
    (40, "Burim i vetëm", "single-source"),
    (20, "E pakorroboruar", "uncorroborated"),
    (0, "Sinjale shqetësuese", "weak-signals"),
]

TIER_TRUST = {"wire": 1.0, "mainstream": 0.8, "local": 0.6, "unrated": 0.3}

# Shënjues sensacionalizmi. Të ndarë sipas gjuhës; anglishtja dhe shqipja
# nuk e ndajnë të njëjtin regjistër emocional.
CLICKBAIT_SQ = [
    "tronditëse", "e tmerrshme", "nuk do ta besoni", "shokuese", "bombë",
    "ekskluzive", "zbulohet e vërteta", "fshihet", "skandal", "u kap",
    "shikoni çfarë", "e paparë", "masakër", "dërrmuese",
]
CLICKBAIT_EN = [
    "shocking", "you won't believe", "unbelievable", "bombshell", "exposed",
    "the truth about", "they don't want you to know", "slammed", "destroyed",
    "goes viral", "this is what happens", "wake up", "hoax", "cover-up",
]

HEDGE_SQ = ["sipas", "raporton", "thotë", "deklaroi", "burime", "konfirmoi"]
HEDGE_EN = ["according to", "reported", "said", "stated", "sources", "confirmed"]


@dataclass
class Component:
    """Një dimension i vetëm i notës, me peshën dhe arsyen e vet."""
    key: str
    score: float          # 0..1
    weight: float
    reason: str


@dataclass
class Assessment:
    score: int
    label: str
    label_key: str
    components: dict[str, Any] = field(default_factory=dict)
    reasons: list[str] = field(default_factory=list)


def _clamp(x: float, lo: float = 0.0, hi: float = 1.0) -> float:
    return max(lo, min(hi, x))


def score_corroboration(independent_domains: int, tiers: list[str]) -> Component:
    """Sa redaksi të pavarura e raportojnë të njëjtën ngjarje.

    Ky është sinjali më i fortë që kemi dhe i vetmi që afrohet me verifikim
    real: fabrikimi rrallë riprodhohet nga redaksi të palidhura. Rritet me
    logaritëm — ndryshimi 1→3 burime peshon shumë më tepër se 8→10.
    """
    if independent_domains <= 1:
        return Component(
            "corroboration", 0.15, 0.40,
            "Vetëm një redaksi e raporton këtë — asnjë verifikim i pavarur.",
        )

    base = _clamp(math.log(independent_domains, 8))
    # Një agjenci lajmesh në grup peshon më shumë se tre blogje.
    best_tier = max((TIER_TRUST.get(t, 0.3) for t in tiers), default=0.3)
    val = _clamp(base * 0.75 + best_tier * 0.25)

    if independent_domains >= 4:
        reason = f"{independent_domains} redaksi të pavarura e raportojnë këtë ngjarje."
    else:
        reason = f"Vetëm {independent_domains} redaksi e raportojnë — korroborim i kufizuar."
    return Component("corroboration", val, 0.40, reason)


def score_source(tier: str, domain_age_days: int | None,
                 has_contact: bool | None, has_about: bool | None,
                 https_valid: bool | None) -> Component:
    """Transparenca e burimit: a e dimë kush e boton këtë dhe si t'i flasim?

    Mungesa e kontaktit/about nuk e bën një faqe gënjeshtare. E bën të
    pallogaritshme — s'ka kujt t'i kërkosh korrigjim.
    """
    val = TIER_TRUST.get(tier, 0.3)
    notes: list[str] = []

    if domain_age_days is not None:
        if domain_age_days < 90:
            val -= 0.30
            notes.append(f"domain shumë i ri ({domain_age_days} ditë)")
        elif domain_age_days < 365:
            val -= 0.12
            notes.append(f"domain i ri ({domain_age_days} ditë)")
        elif domain_age_days > 365 * 5:
            val += 0.05

    if has_contact is False:
        val -= 0.10
        notes.append("pa faqe kontakti")
    if has_about is False:
        val -= 0.10
        notes.append("pa faqe 'rreth nesh'")
    if https_valid is False:
        val -= 0.10
        notes.append("HTTPS i pavlefshëm")

    val = _clamp(val)
    if notes:
        reason = "Transparencë e burimit: " + ", ".join(notes) + "."
    else:
        label = {"wire": "agjenci lajmesh", "mainstream": "media e themeluar",
                 "local": "media vendore", "unrated": "burim i pavlerësuar"}[tier]
        reason = f"Burimi është {label} me të dhëna identifikimi në rregull."
    return Component("source", val, 0.25, reason)


def score_craft(author: str | None, published_at: datetime | None,
                body: str | None, outbound_links: int) -> Component:
    """Normat bazë gazetareske: nënshkrim, datë, citime, gjatësi.

    `body is None` do të thotë se teksti NUK U LEXUA DOT (paywall, 403,
    anti-bot) — jo se artikulli është bosh. Dallimi ka rëndësi: NYT-ja kthen
    403 për çdo trup artikulli. Po t'i ndëshkonim për "tekst i shkurtër" dhe
    "pa citime", një gazetë serioze pas paywall-i do të dilte më keq se një
    fabrikë klikimesh që na e lë tekstin të lirë — pikërisht e kundërta e së
    vërtetës. Kur s'dimë, nuk ndëshkojmë; e themi që s'dimë.
    """
    val, notes = 1.0, []

    if not author or not author.strip():
        val -= 0.25
        notes.append("pa autor të nënshkruar")
    if published_at is None:
        val -= 0.20
        notes.append("pa datë botimi")

    if body is None:
        # E pamatshme, jo e keqe. Asnjë ndëshkim.
        notes.append("teksti s'u lexua dot (paywall ose bllokim) — nuk u vlerësua")
    else:
        if len(body) < 400:
            val -= 0.20
            notes.append("tekst shumë i shkurtër për të mbajtur një pretendim")
        if outbound_links == 0:
            val -= 0.15
            notes.append("nuk citon asnjë burim primar")

    val = _clamp(val)
    reason = ("Mangësi redaktuese: " + ", ".join(notes) + ".") if notes \
        else "Ka autor, datë, gjatësi dhe citime — normat bazë janë të plotësuara."
    return Component("craft", val, 0.20, reason)


def score_language(title: str, body: str | None, lang: str) -> Component:
    """Sensacionalizëm dhe atribuim.

    Gjuha manipuluese nuk e bën një histori të rreme. Por është korrelate e
    fortë e shkrimit që synon klikimin, jo saktësinë — dhe kjo është pikërisht
    ajo që një lexues meriton ta dijë përpara se ta besojë.
    """
    text = f"{title} {body or ''}".lower()
    markers = CLICKBAIT_SQ if lang == "sq" else CLICKBAIT_EN
    hedges = HEDGE_SQ if lang == "sq" else HEDGE_EN

    hits = [m for m in markers if m in text]
    attributions = sum(1 for h in hedges if h in text)

    val = 1.0 - _clamp(len(hits) * 0.22)

    # Titull krejt me shkronja të mëdha ose me shumë pikëçuditëse.
    letters = [c for c in title if c.isalpha()]
    if letters and sum(1 for c in letters if c.isupper()) / len(letters) > 0.6:
        val -= 0.20
        hits.append("titull me shkronja të mëdha")
    if title.count("!") >= 2:
        val -= 0.10
        hits.append("pikëçuditëse të shumta")

    # Atribuimi i qartë e kthen pjesërisht notën: "sipas X" është punë e mirë.
    if attributions >= 2:
        val += 0.10

    val = _clamp(val)
    reason = ("Gjuhë sensacionale: " + ", ".join(hits[:4]) + ".") if hits \
        else "Gjuha është e matur dhe pretendimet i atribuohen burimeve."
    return Component("language", val, 0.15, reason)


def assess(*, independent_domains: int, cluster_tiers: list[str], tier: str,
           domain_age_days: int | None, has_contact: bool | None,
           has_about: bool | None, https_valid: bool | None,
           author: str | None, published_at: datetime | None,
           title: str, body: str | None, outbound_links: int,
           lang: str) -> Assessment:
    """Kombinon të gjithë përbërësit në një notë të vetme 0–100."""
    comps = [
        score_corroboration(independent_domains, cluster_tiers),
        score_source(tier, domain_age_days, has_contact, has_about, https_valid),
        score_craft(author, published_at, body, outbound_links),
        score_language(title, body, lang),
    ]

    total = sum(c.score * c.weight for c in comps)
    score = int(round(_clamp(total) * 100))

    label, label_key = next(
        ((name, key) for threshold, name, key in LABELS if score >= threshold),
        ("Sinjale shqetësuese", "weak-signals"),
    )

    return Assessment(
        score=score,
        label=label,
        label_key=label_key,
        components={
            c.key: {"score": round(c.score, 3), "weight": c.weight, "reason": c.reason}
            for c in comps
        },
        # Arsyet renditen nga më dëmtuesja: lexuesi sheh i pari atë që e ul notën.
        reasons=[c.reason for c in sorted(comps, key=lambda c: c.score * c.weight)],
    )


_WORD_RE = re.compile(r"[^\wçëÇË]+", re.UNICODE)
_STOP = {
    "the", "a", "an", "of", "in", "on", "to", "for", "and", "is", "are", "at",
    "by", "with", "from", "as", "it", "that", "this", "be", "was", "were",
    "dhe", "në", "të", "e", "i", "me", "për", "nga", "që", "një", "së", "u",
    "është", "janë", "ka", "kanë", "si", "pas", "mbi", "por", "apo", "ose",
}


def _entities(title: str) -> list[str]:
    """Emra të përveçëm dhe numra — pjesa e një titulli që i mbijeton përkthimit.

    "Kancelari gjerman Friedrich Merz paralajmëron SHBA-në" dhe "German
    Chancellor Friedrich Merz warns US" nuk ndajnë asnjë fjalë të përbashkët,
    por ndajnë 'friedrich' dhe 'merz'. Pa këtë, asnjë artikull shqip nuk do të
    korroborohej kurrë nga një burim ndërkombëtar — dhe do të dukej gjithmonë
    'burim i vetëm' edhe kur BBC-ja e mbulon të njëjtën ngjarje.

    Fjala e parë e titullit anashkalohet: shkronja e saj e madhe është
    ortografi, jo emër i përveçëm.
    """
    ents: list[str] = []
    for i, raw in enumerate(re.split(r"\s+", title.strip())):
        w = raw.strip(".,:;!?\"'()[]—–-")
        if not w:
            continue
        if any(c.isdigit() for c in w) and len(w) >= 4:
            ents.append(w.lower())            # vite, shuma: "2026", "500"
        elif i > 0 and len(w) > 2 and w[0].isupper() and w.lower() not in _STOP:
            ents.append(w.lower())
    return ents


def fingerprint(title: str) -> str:
    """Gjurmë e qëndrueshme e titullit, për të grupuar të njëjtën ngjarje.

    Format: "fjalë domethënëse | entitete". E qëllimshme se është e trashë —
    s'ka embeddings, s'ka model, vetëm mbivendosje fjalësh. Dy redaksi që
    mbulojnë një ngjarje ndajnë ose fjalorin (e njëjta gjuhë) ose emrat e
    përveçëm (gjuhë të ndryshme).
    """
    words = [w for w in _WORD_RE.split(title.lower()) if len(w) > 3 and w not in _STOP]
    ents = _entities(title)
    return " ".join(sorted(set(words))[:8]) + " | " + " ".join(sorted(set(ents))[:6])


def _split(fp: str) -> tuple[set[str], set[str]]:
    words, _, ents = fp.partition("|")
    return set(words.split()), set(ents.split())


def similarity(fp_a: str, fp_b: str) -> float:
    """Jaccard mbi fjalët, ose mbi entitetet kur gjuhët ndryshojnë.

    Merret më e larta e të dyjave. Entitetet kërkojnë të paktën 2 përputhje:
    një e vetme është shumë e lehtë — dy histori krejt të palidhura që të dyja
    përmendin "Kosova" nuk janë e njëjta ngjarje.
    """
    wa, ea = _split(fp_a)
    wb, eb = _split(fp_b)

    word_sim = len(wa & wb) / len(wa | wb) if (wa and wb) else 0.0

    shared = ea & eb
    ent_sim = 0.0
    if len(shared) >= 2:
        ent_sim = len(shared) / len(ea | eb)

    return max(word_sim, ent_sim)
