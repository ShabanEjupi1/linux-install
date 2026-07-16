"""Shtresa e arsyetimit (Ollama, lokale).

LLM-ja NUK e cakton notën. Nota vjen nga score.py, e cila është
deterministike dhe e auditueshme. LLM-ja vetëm e shpjegon me fjalë atë që
sinjalet e kanë vendosur tashmë.

Ky ndarje është e qëllimshme. Një model 7B që nuk ka qasje në internet nuk
mund të verifikojë asgjë; kujtesa e tij është e vjetruar dhe halucinon me
vetëbesim. Nëse do ta linim të jepte notën, do të shpikte fakte. Duke e
kufizuar te "shpjego këto sinjale", modeli bën atë që di të bëjë vërtet —
gjuhë — dhe jo atë që s'di.

Nëse Ollama nuk përgjigjet, portali funksionon njësoj, thjesht pa narrativë.
Arsyetimi është zbukurim; nota nuk varet prej tij.
"""

from __future__ import annotations

import json
import logging
import os

import urllib.error
import urllib.request

log = logging.getLogger(__name__)

OLLAMA_URL = os.getenv("OLLAMA_URL", "http://news-ollama:11434")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "qwen2.5:3b-instruct-q4_K_M")
TIMEOUT = int(os.getenv("OLLAMA_TIMEOUT", "180"))

# bge-m3 është shumëgjuhësh dhe i ndërtuar posaçërisht për krahasim
# ndërgjuhësor: "Toronton" shqip dhe "Toronto" anglisht bien pranë njëri-tjetrit
# në të njëjtën hapësirë vektoriale. Kjo është ARSYEJA e vetme pse korroborimi
# funksionon mes një burimi shqip dhe BBC-së. Krahasimi me fjalë nuk e bën dot:
# gjuhët nuk ndajnë fjalor, dhe shqipja i lakon edhe emrat e përveçëm.
EMBED_MODEL = os.getenv("EMBED_MODEL", "bge-m3")

SYSTEM = """Je asistent që shpjegon vlerësimin e besueshmërisë së lajmeve.

Rregulla absolute:
- NUK e vendos ti nëse lajmi është i vërtetë apo i rremë. Nuk e di. Nuk ke
  qasje në internet dhe kujtesa jote është e vjetruar.
- NUK shpik fakte, emra, data apo ngjarje. Nëse s'e ke te të dhënat, hesht.
- Vetëm shpjego çfarë tregojnë sinjalet e dhëna, me gjuhë të thjeshtë.
- Nëse sinjalet janë të mira, thuaj se historia është e mbështetur mirë —
  jo se është "e vërtetë".
- Nëse janë të dobëta, thuaj se s'ka prova të mjaftueshme — jo se është
  "gënjeshtër".

Përgjigju me 2–3 fjali, shqip, pa lista, pa tituj."""

PROMPT = """Titulli: {title}
Burimi: {source} (kategori: {tier})
Nota e llogaritur: {score}/100 — {label}

Sinjalet e matura:
{signals}

Shpjego lexuesit, në 2–3 fjali, pse kjo histori mori këtë notë."""


def _post(path: str, payload: dict, timeout: int) -> dict | None:
    req = urllib.request.Request(
        f"{OLLAMA_URL}{path}",
        data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read())
    except (urllib.error.URLError, TimeoutError, OSError, json.JSONDecodeError) as e:
        log.warning("Ollama nuk u përgjigj (%s): %s", path, e)
        return None


def _have(model: str) -> bool:
    try:
        with urllib.request.urlopen(f"{OLLAMA_URL}/api/tags", timeout=10) as r:
            names = [m["name"] for m in json.loads(r.read()).get("models", [])]
            return any(n == model or n.startswith(model + ":") for n in names)
    except Exception:
        return False


def available() -> bool:
    return _have(OLLAMA_MODEL)


def ensure_model(model: str | None = None) -> bool:
    """Tërheq modelin nëse mungon. Nisja e parë zgjat disa minuta."""
    model = model or OLLAMA_MODEL
    if _have(model):
        return True
    log.info("Po tërhiqet modeli %s — kjo zgjat disa minuta herën e parë.", model)
    # Pull-i transmeton progres; s'na duhet, por duhet konsumuar deri në fund.
    res = _post("/api/pull", {"name": model, "stream": False}, timeout=1800)
    return res is not None and _have(model)


def embed(text: str) -> list[float] | None:
    """Vektor shumëgjuhësh i titullit. None nëse Ollama s'përgjigjet."""
    res = _post("/api/embed", {"model": EMBED_MODEL, "input": text[:1000]}, timeout=60)
    if not res:
        return None
    vecs = res.get("embeddings") or []
    return vecs[0] if vecs else None


def rationale(*, title: str, source: str, tier: str, score: int, label: str,
              reasons: list[str]) -> tuple[str | None, str | None]:
    """Kthen (narrativë, model) ose (None, None) nëse LLM-ja s'është e disponueshme."""
    signals = "\n".join(f"- {r}" for r in reasons)
    res = _post("/api/chat", {
        "model": OLLAMA_MODEL,
        "stream": False,
        "messages": [
            {"role": "system", "content": SYSTEM},
            {"role": "user", "content": PROMPT.format(
                title=title, source=source, tier=tier,
                score=score, label=label, signals=signals)},
        ],
        "options": {
            # Temperaturë e ulët: duam parafrazim besnik, jo krijimtari.
            "temperature": 0.2,
            "num_predict": 220,
            "num_ctx": 2048,
        },
    }, timeout=TIMEOUT)

    if not res:
        return None, None
    text = (res.get("message") or {}).get("content", "").strip()
    return (text, OLLAMA_MODEL) if text else (None, None)
