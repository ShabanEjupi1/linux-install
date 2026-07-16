"""Shtresa Ollama — VETËM embeddings.

Pse vetëm embeddings, dhe pse KURRË gjenerim teksti shqip:

U provuan `qwen2.5:3b-instruct` dhe `qwen2.5:7b-instruct` më 2026-07-16 mbi
Ampere (ARM, 2 core të kufizuar). Të dy dështuan në dy mënyra të pavarura,
dhe secila veç e veç është vdekjeprurëse:

  1. CILËSIA. Shqipja e prodhuar ishte e pakuptimtë, jo thjesht e ngathët.
     Nga 3b: "mbërshkona për herë të parë", "nga shqipunetar",
     "produkt të dhënues së vendoreve". Nga 7b: "Raporti i mënyrës së morimit
     në lindja e vjetër". Këto nuk janë fjali. Në një faqe që shet
     besueshmëri, tekst i prishur pikërisht te shpjegimi shkatërron atë që
     synon të mbështesë.
  2. SHPEJTËSIA. 7b-ja harxhoi 257 sekonda për NJË artikull. Me ~300 artikuj
     në ditë kjo do të ishin 21 orë llogaritje çdo ditë — s'do të mbaronte
     kurrë, edhe sikur teksti të ishte i përsosur.

Shqipja është gjuhë me pak burime; asnjë model që hyn në 2 core ARM nuk e
flet. Arsyet deterministike te score.py janë shqip i saktë, të
menjëhershme, falas dhe të auditueshme — më të mira në çdo dimension.

Ajo që Ollama E BËN mirë këtu është bge-m3: vektorë shumëgjuhësh, të shpejtë,
dhe e vetmja gjë që lejon një titull shqip të korroborohet nga BBC-ja. Kaq.
Nëse ndonjëherë shtohet përsëri gjenerim teksti, kërkohet një model që
vërtet flet shqip DHE një buxhet kohe që përballon volumin — matni të dyja
para se ta besoni.
"""

from __future__ import annotations

import json
import logging
import os
import urllib.error
import urllib.request

log = logging.getLogger(__name__)

OLLAMA_URL = os.getenv("OLLAMA_URL", "http://news-ollama:11434")

# bge-m3 është shumëgjuhësh dhe i ndërtuar për krahasim ndërgjuhësor:
# "Toronton" shqip dhe "Toronto" anglisht bien pranë njëri-tjetrit në të
# njëjtën hapësirë vektoriale. Krahasimi me fjalë nuk e bën dot këtë —
# gjuhët s'ndajnë fjalor, dhe shqipja i lakon edhe emrat e përveçëm.
EMBED_MODEL = os.getenv("EMBED_MODEL", "bge-m3")
EMBED_TIMEOUT = int(os.getenv("EMBED_TIMEOUT", "120"))


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


def have(model: str) -> bool:
    try:
        with urllib.request.urlopen(f"{OLLAMA_URL}/api/tags", timeout=10) as r:
            names = [m["name"] for m in json.loads(r.read()).get("models", [])]
            return any(n == model or n.startswith(model + ":") for n in names)
    except Exception:
        return False


def ensure_model(model: str | None = None) -> bool:
    """Tërheq modelin nëse mungon. Nisja e parë zgjat disa minuta."""
    model = model or EMBED_MODEL
    if have(model):
        return True
    log.info("Po tërhiqet modeli %s — kjo zgjat disa minuta herën e parë.", model)
    res = _post("/api/pull", {"name": model, "stream": False}, timeout=1800)
    return res is not None and have(model)


def embed(text: str) -> list[float] | None:
    """Vektor shumëgjuhësh i titullit. None nëse Ollama s'përgjigjet.

    None nuk është fatale: grupimi bie te krahasimi me fjalë (score.similarity),
    i cili punon brenda së njëjtës gjuhë. Humbet vetëm korroborimi ndërgjuhësor.
    """
    res = _post("/api/embed", {"model": EMBED_MODEL, "input": text[:1000]},
                timeout=EMBED_TIMEOUT)
    if not res:
        return None
    vecs = res.get("embeddings") or []
    return vecs[0] if vecs else None
