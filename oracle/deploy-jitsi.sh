#!/bin/bash
# Oracle Cloud VM — deploy standalone Jitsi Meet (public HTTPS + WebRTC media).
# Works on Oracle Linux (dnf/firewalld) and Ubuntu (apt/iptables).
# Run ON the Oracle instance:  PUBLIC_IP=<ip> sudo -E bash deploy-jitsi.sh
#
# Prerequisites in the OCI console (cannot be done over SSH):
#   VCN has an Internet Gateway + route 0.0.0.0/0 -> IGW on the subnet, and the
#   subnet Security List / NSG allows ingress: TCP 22, TCP 80, TCP 443,
#   UDP 10000, UDP 3478 from 0.0.0.0/0.
# DNS: meet.shabanejupi.tech = A record -> this VM's public IP, DNS-only (grey cloud).
set -e

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'
log()    { echo -e "${GREEN}[+]${NC} $1"; }
warn()   { echo -e "${YELLOW}[!]${NC} $1"; }
err()    { echo -e "${RED}[x]${NC} $1"; exit 1; }
header() { echo -e "\n${CYAN}========== $1 ==========${NC}"; }

[[ $EUID -ne 0 ]] && err "Must run as root (use: sudo -E bash deploy-jitsi.sh)"
APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

header "Swap (1 GB RAM box needs lots of headroom for dnf + image pulls)"
# Ensure at least ~4.5 GB total swap. dnf on OL9 balloons parsing repo metadata
# and will OOM-kill on a 1 GB box without generous swap.
CUR_SWAP=$(free -m | awk '/Swap:/{print $2}'); CUR_SWAP=${CUR_SWAP:-0}
if [[ $CUR_SWAP -lt 4500 ]]; then
    SF=/swapfile-jitsi; ADD=$((4608 - CUR_SWAP))
    if ! swapon --show=NAME --noheadings 2>/dev/null | grep -qx "$SF"; then
        rm -f "$SF"
        fallocate -l ${ADD}M "$SF" || dd if=/dev/zero of="$SF" bs=1M count=$ADD
        chmod 600 "$SF"; mkswap "$SF"; swapon "$SF"
        grep -qxF "$SF none swap sw 0 0" /etc/fstab || echo "$SF none swap sw 0 0" >> /etc/fstab
    fi
fi
log "Swap total: $(free -h | awk '/Swap:/{print $2}')"

header "Docker"
if ! command -v docker &>/dev/null; then
    if command -v dnf &>/dev/null; then
        # Oracle Linux / RHEL 9: get.docker.com does not support 'ol'. Use the
        # official Docker CE repo (CentOS build is RHEL9-compatible).
        # Disable the huge ol9 "OCI Included" repo (~233 MB metadata) during these
        # installs — parsing it OOM-kills dnf on a 1 GB box.
        DNF="dnf -y --disablerepo=*oci*"
        dnf clean all
        $DNF install dnf-plugins-core
        dnf config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
        $DNF install docker-ce docker-ce-cli containerd.io docker-compose-plugin \
          || $DNF --allowerasing install docker-ce docker-ce-cli containerd.io docker-compose-plugin
    else
        curl -fsSL https://get.docker.com | sh          # Ubuntu/Debian
        apt-get install -y -qq docker-compose-plugin 2>/dev/null || true
    fi
    systemctl enable --now docker
fi
docker compose version &>/dev/null 2>&1 || warn "docker compose plugin missing"
log "Docker: $(docker --version)"

header "Instance firewall"
if systemctl is-active --quiet firewalld; then
    firewall-cmd --permanent --add-port=80/tcp
    firewall-cmd --permanent --add-port=443/tcp
    firewall-cmd --permanent --add-port=10000/udp
    firewall-cmd --permanent --add-port=3478/udp
    firewall-cmd --reload
    log "firewalld: opened 80,443/tcp + 10000,3478/udp"
else
    # Oracle images ship an iptables INPUT chain that REJECTs most traffic;
    # insert ACCEPT rules ahead of that reject.
    open() { iptables -C INPUT -p "$1" --dport "$2" -j ACCEPT 2>/dev/null || iptables -I INPUT -p "$1" --dport "$2" -j ACCEPT; }
    open tcp 80; open tcp 443; open udp 10000; open udp 3478
    if command -v netfilter-persistent &>/dev/null; then netfilter-persistent save
    elif [[ -f /etc/sysconfig/iptables ]]; then iptables-save > /etc/sysconfig/iptables
    elif [[ -d /etc/iptables ]]; then iptables-save > /etc/iptables/rules.v4; fi
    log "iptables: opened 80,443/tcp + 10000,3478/udp"
fi
warn "Also confirm these ports are open in the OCI Security List / NSG."

header "Configuration"
if [[ ! -f "$APP_DIR/.env" ]]; then
    [[ -z "$PUBLIC_IP" ]] && err "PUBLIC_IP not set and no .env present"
    gen() { openssl rand -hex 16; }
    cat > "$APP_DIR/.env" << EOF
PUBLIC_IP=$PUBLIC_IP
LETSENCRYPT_DOMAIN=${LETSENCRYPT_DOMAIN:-meet.shabanejupi.tech}
LETSENCRYPT_EMAIL=${LETSENCRYPT_EMAIL:-shaban.ejupi@student.uni-pr.edu}
TZ=Europe/Tirane
XMPP_DOMAIN=meet.jitsi
JICOFO_AUTH_PASSWORD=$(gen)
JVB_AUTH_PASSWORD=$(gen)
JIBRI_XMPP_PASSWORD=$(gen)
JIBRI_RECORDER_PASSWORD=$(gen)
EOF
    log "Wrote $APP_DIR/.env"
else
    warn "Keeping existing $APP_DIR/.env"
fi

header "Starting Jitsi"
cd "$APP_DIR"
docker compose up -d
echo ""; docker compose ps; echo ""
log "URL: https://$(grep '^LETSENCRYPT_DOMAIN=' "$APP_DIR/.env" | cut -d= -f2)"
warn "First load takes 1-2 min while the Let's Encrypt cert is issued. Watch: docker compose logs -f web"
