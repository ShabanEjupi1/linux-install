FROM python:3.12-slim

# supercronic: cron që punon si duhet në kontejner (log në stdout, PID 1 i sjellshëm,
# pa nevojë për root). Cron-i i zakonshëm i Debian-it i gëlltit logjet.
ARG SUPERCRONIC_VERSION=v0.2.29
RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && ARCH="$(dpkg --print-architecture)" \
    && case "$ARCH" in \
         arm64) SC_ARCH=linux-arm64 ;; \
         amd64) SC_ARCH=linux-amd64 ;; \
         *) echo "arkitekturë e pambështetur: $ARCH" >&2; exit 1 ;; \
       esac \
    && curl -fsSL -o /usr/local/bin/supercronic \
         "https://github.com/aptible/supercronic/releases/download/${SUPERCRONIC_VERSION}/supercronic-${SC_ARCH}" \
    && chmod +x /usr/local/bin/supercronic

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Cikli: çdo 2 orë marrja + vlerësimi. Ollama zgjohet vetëm brenda tij.
RUN printf '0 */2 * * * cd /app && python pipeline.py\n' > /app/crontab

# Një cikël në nisje që portali të mos jetë bosh derisa të bjerë ora e parë,
# pastaj kalon te orari. `|| true`: një dështim i ciklit të parë (p.sh. rrjeti
# ende jo gati) s'duhet ta fusë kontejnerin në loop rinisjeje.
CMD ["sh", "-c", "python pipeline.py || true; exec supercronic /app/crontab"]
