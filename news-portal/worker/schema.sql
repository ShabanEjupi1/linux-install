-- Skema e portalit të lajmeve. Idempotente: çdo nisje e worker-it e ekzekuton.

CREATE TABLE IF NOT EXISTS sources (
    id           SERIAL PRIMARY KEY,
    domain       TEXT UNIQUE NOT NULL,
    name         TEXT NOT NULL,
    feed_url     TEXT,
    tier         TEXT NOT NULL DEFAULT 'unrated',
    lang         TEXT NOT NULL DEFAULT 'en',
    -- Sinjale të burimit, rifreskohen rrallë (RDAP/HTTP), jo për çdo artikull.
    domain_age_days   INT,
    has_contact_page  BOOLEAN,
    has_about_page    BOOLEAN,
    https_valid       BOOLEAN,
    signals_checked_at TIMESTAMPTZ,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS articles (
    id           SERIAL PRIMARY KEY,
    source_id    INT NOT NULL REFERENCES sources(id) ON DELETE CASCADE,
    url          TEXT UNIQUE NOT NULL,
    title        TEXT NOT NULL,
    summary      TEXT,
    body         TEXT,
    author       TEXT,
    published_at TIMESTAMPTZ,
    fetched_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- Gjurma e normalizuar e titullit, për grupim tregimesh.
    fingerprint  TEXT,
    cluster_id   INT,
    lang         TEXT,
    -- Vektor bge-m3 i titullit. JSONB dhe jo pgvector: numri i artikujve në
    -- dritaren e grupimit është qindra, ndaj krahasimi në numpy zgjat
    -- milisekonda dhe s'ia vlen një imazh tjetër i Postgres-it.
    embedding    JSONB
);

-- Migrim për baza që ekzistojnë para se embedding-u të shtohej.
ALTER TABLE articles ADD COLUMN IF NOT EXISTS embedding JSONB;

CREATE INDEX IF NOT EXISTS articles_cluster_idx    ON articles(cluster_id);
CREATE INDEX IF NOT EXISTS articles_published_idx  ON articles(published_at DESC NULLS LAST);
CREATE INDEX IF NOT EXISTS articles_fingerprint_idx ON articles(fingerprint);

-- Një "cluster" = një ngjarje e raportuar nga një ose më shumë burime.
-- Korroborimi matet mbi domain-e të pavarur, jo mbi numrin e artikujve.
CREATE TABLE IF NOT EXISTS clusters (
    id            SERIAL PRIMARY KEY,
    lead_title    TEXT NOT NULL,
    first_seen_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS assessments (
    id            SERIAL PRIMARY KEY,
    article_id    INT NOT NULL UNIQUE REFERENCES articles(id) ON DELETE CASCADE,
    score         INT NOT NULL,               -- 0..100
    label         TEXT NOT NULL,              -- shih score.py: LABELS
    -- Përbërësit ruhen veçmas që nota të mos jetë kurrë një numër i pashpjegueshëm.
    components    JSONB NOT NULL,
    reasons       JSONB NOT NULL,             -- listë arsyesh të lexueshme
    scored_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Hequr më 2026-07-16: kolonat llm_rationale/llm_model. Modelet lokale
-- prodhuan shqip të pakuptimtë (shih llm.py) dhe u fshinë nga faqja. Nëse
-- ekzistojnë nga një bazë e vjetër, hidhi — përmbajtja e tyre ishte e prishur.
ALTER TABLE assessments DROP COLUMN IF EXISTS llm_rationale;
ALTER TABLE assessments DROP COLUMN IF EXISTS llm_model;

CREATE INDEX IF NOT EXISTS assessments_score_idx ON assessments(score);

-- Regjistri i ekzekutimeve; portali e tregon që lexuesi ta dijë nëse
-- automatizimi ka rënë (ndryshe një portal i vjetruar duket i shëndetshëm).
CREATE TABLE IF NOT EXISTS runs (
    id           SERIAL PRIMARY KEY,
    started_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    finished_at  TIMESTAMPTZ,
    ok           BOOLEAN,
    fetched      INT DEFAULT 0,
    scored       INT DEFAULT 0,
    errors       JSONB DEFAULT '[]'::jsonb
);
