#!/usr/bin/env python3
"""Generate Postgres import SQL from the shop PC's fresh BMDData CSV export.

Journals  -> append-only INSERT ... ON CONFLICT (pk) DO NOTHING  (idempotent re-run)
Lookups   -> upsert       INSERT ... ON CONFLICT (pk) DO UPDATE  (no deletes by default)

With --mirror the lookup tables additionally DELETE rows the shop PC no longer
has, making Postgres an exact mirror rather than a union of every export ever
run. Needed because the desktop deletes articles and a plain upsert can only
ever grow: after Phase 8d the live DB held 2280 articles against the shop's
1619. Only ever applied to the tables exported in FULL mode -- mirroring an
incrementally-exported journal would delete all its history.

Each CSV is staged into an all-text temp table, then cast to the real target
types. That is what lets us left()-truncate columns EF made too narrow
(e.g. Artikujt.IRregullt is varchar(1) but the source holds 'False').

Input: the CSVs from tools/export-bmddata.ps1 (run on the shop PC), plus one
schema_<Table>.tsv per table describing the TARGET Postgres columns:

    psql -U pos -d kosovapos -t -A -F$'\t' -c "SELECT column_name, data_type,
      coalesce(character_maximum_length,0), is_identity FROM information_schema.columns
      WHERE table_name='DitariD' ORDER BY ordinal_position;" > schema_DitariD.tsv

Usage:
    python3 import-bmddata.py <csv_dir> <out_dir>            # emits ROLLBACK (dry run)
    python3 import-bmddata.py <csv_dir> <out_dir> --commit   # emits COMMIT
    python3 import-bmddata.py <csv_dir> <out_dir> --mirror   # + delete rows the shop PC dropped
    psql -U pos -d kosovapos -v ON_ERROR_STOP=1 -f <out_dir>/import_<Table>.sql

Run the tables in the printed order (lookups before journals). Re-running is
safe: journals DO NOTHING on conflict, lookups re-apply the same values.

tbl_Stoku (stock ledger) and Kartela_Subjektit (partner account ledger) are
journals: append-only, exported past an ID watermark. Kartela_Subjektit.Mbeti
summed per SUBJEKTI_ID is what /partneret shows as a partner's balance.

Tatimi is imported as reference data only. Do NOT wire Artikujt.Vat to it to
derive VAT rates -- see the note on the Tatimi model in ConfigTables.cs.
"""
import csv, sys, os

CSV_DIR = sys.argv[1] if len(sys.argv) > 1 else "bmd"
OUT_DIR = sys.argv[2] if len(sys.argv) > 2 else "sql"
COMMIT = "--commit" in sys.argv
MIRROR = "--mirror" in sys.argv

# A mirror delete must never destroy inventory. Rows matching a table's guard are
# the only ones eligible for deletion; anything else the shop PC dropped is
# reported and left in place for a human to look at.
MIRROR_GUARD = {"Artikujt": 'coalesce("Sasia", 0) = 0'}

# lookups first (Artikujt before the journals that reference it), then journals
LOOKUPS = [
    "Tatimi", "Arkat", "MetodaPagese", "NjesitMatese", "LlojiShpenzimeve", "KategoriaPos",
    "Kategoria", "Qytetet", "Filiala", "Sektori", "Punetoret", "FurnitoriNew", "Artikujt",
]
JOURNALS = ["DitariH", "DitariD", "ArkaHyrjeDalje", "tbl_Stoku", "Kartela_Subjektit"]

os.makedirs(OUT_DIR, exist_ok=True)


def load_schema(t):
    cols = []
    with open(f"schema_{t}.tsv") as f:
        for line in f:
            if not line.strip():
                continue
            name, typ, maxlen, ident = line.rstrip("\n").split("\t")
            cols.append({"name": name, "type": typ, "max": int(maxlen), "ident": ident == "YES"})
    return cols


def cast_expr(col, alias):
    """SQL expression converting staged text -> the target column type."""
    t, n = col["type"], col["max"]
    if t in ("bigint", "integer", "smallint"):
        # ::numeric first so '3.0' survives
        return f"{alias}::numeric::{t}"
    if t in ("numeric", "double precision", "real"):
        return f"{alias}::{t}"
    if t == "boolean":
        return f"{alias}::boolean"
    if t.startswith("timestamp"):
        return f"{alias}::timestamp"
    if t == "date":
        return f"{alias}::date"
    if t == "character varying" and n > 0:
        return f"left({alias}, {n})"
    return alias  # text / unbounded varchar


def q(name):
    return '"' + name + '"'


def emit(t, mode):
    path = os.path.join(CSV_DIR, f"{t}.csv")
    if not os.path.exists(path):
        # A partial export (e.g. backfilling only the tables a new release added)
        # is legitimate; a missing CSV for a table you meant to export is not.
        print(f"{t:18} {'':6}            SKIPPED -- no {t}.csv in {CSV_DIR}")
        return False

    cols = load_schema(t)
    pk = next(c["name"] for c in cols if c["ident"])

    with open(path, encoding="utf-8-sig", newline="") as f:
        rdr = csv.reader(f)
        header = next(rdr)
        rows = list(rdr)

    # keep only target columns, in target order.
    # bytea (Artikujt.Foto) is skipped: the exporter drops blobs, so importing it
    # would only overwrite existing images with NULL.
    idx = {c: i for i, c in enumerate(header)}
    use = [c for c in cols if c["name"] in idx and c["type"] != "bytea"]

    stg_cols = ", ".join(f"c{i} text" for i in range(len(use)))
    copy_cols = ", ".join(f"c{i}" for i in range(len(use)))
    tgt_cols = ", ".join(q(c["name"]) for c in use)
    sel = ", ".join(cast_expr(c, f"c{i}") for i, c in enumerate(use))

    out = os.path.join(OUT_DIR, f"import_{t}.sql")
    with open(out, "w", encoding="utf-8", newline="\n") as o:
        o.write("BEGIN;\n")
        o.write(f"CREATE TEMP TABLE stg ({stg_cols}) ON COMMIT DROP;\n")
        o.write(f"COPY stg ({copy_cols}) FROM STDIN WITH (FORMAT csv, NULL '\\N');\n")

        w = csv.writer(o, lineterminator="\n", quoting=csv.QUOTE_MINIMAL)
        for r in rows:
            vals = []
            for c in use:
                v = r[idx[c["name"]]]
                vals.append("\\N" if v == "\\N" else v)
            w.writerow(vals)
        o.write("\\.\n")

        o.write(f"SELECT count(*) AS staged FROM stg;\n")
        o.write(f"INSERT INTO {q(t)} ({tgt_cols})\nSELECT {sel} FROM stg\n")
        if mode == "append":
            o.write(f"ON CONFLICT ({q(pk)}) DO NOTHING;\n")
        else:
            sets = ", ".join(
                f"{q(c['name'])} = EXCLUDED.{q(c['name'])}" for c in use if c["name"] != pk
            )
            o.write(f"ON CONFLICT ({q(pk)}) DO UPDATE SET {sets};\n")

        # Mirror: drop rows the shop PC no longer has. Only for full exports
        # (mode == upsert) -- the journals are exported incrementally, so "not in
        # stg" there would mean "all of history".
        if MIRROR and mode == "upsert":
            pk_i = next(i for i, c in enumerate(use) if c["name"] == pk)
            keep = f"SELECT {cast_expr(use[pk_i], f'c{pk_i}')} FROM stg"
            stale = f"{q(pk)} NOT IN ({keep})"
            guard = MIRROR_GUARD.get(t)

            if guard:
                # Surface anything the guard protects rather than deleting it.
                o.write(
                    f"SELECT '{t}' AS tbl, count(*) AS stale_but_kept FROM {q(t)}\n"
                    f"  WHERE {stale} AND NOT ({guard});\n"
                )
                o.write(f"DELETE FROM {q(t)} WHERE {stale} AND {guard};\n")
            else:
                o.write(f"DELETE FROM {q(t)} WHERE {stale};\n")

        # setval is NOT transactional -- it survives ROLLBACK. Two consequences:
        #   1. never emit it on a dry run, or the "harmless" rehearsal leaves the
        #      sequence moved while the rows it was computed from are rolled back;
        #   2. never let it LOWER the sequence. A mirror delete can remove the row
        #      holding max(pk); dropping the sequence to the new max would hand the
        #      next web sale an id that already exists.
        if COMMIT:
            o.write(
                "DO $$\n"
                "DECLARE seq text; hi bigint; cur bigint;\n"
                "BEGIN\n"
                f"  seq := pg_get_serial_sequence('{q(t)}', '{pk}');\n"
                f"  SELECT max({q(pk)}) INTO hi FROM {q(t)};\n"
                "  EXECUTE format('SELECT last_value FROM %s', seq) INTO cur;\n"
                "  PERFORM setval(seq, GREATEST(coalesce(hi, 1), coalesce(cur, 1)));\n"
                "END $$;\n"
            )
        o.write(f"SELECT '{t}' AS tbl, count(*) AS rows_now FROM {q(t)};\n")
        o.write("COMMIT;\n" if COMMIT else "ROLLBACK;\n")

    flag = "+mirror" if (MIRROR and mode == "upsert") else ""
    print(f"{t:16} {len(rows):6} csv rows -> {out}  [{mode}{flag}]  pk={pk} cols={len(use)}")
    return True


emitted = []
for t in LOOKUPS:
    if emit(t, "upsert"):
        emitted.append(t)
for t in JOURNALS:
    if emit(t, "append"):
        emitted.append(t)

print("\nRun order:", " ".join(emitted))
print("COMMIT" if COMMIT else "DRY RUN (rollback)")
