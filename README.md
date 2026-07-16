# Server Setup — 185.174.211.45

Full automated infrastructure setup from a Windows PC.

## Services

| URL | Port | Stack |
|-----|------|-------|
| shabanejupi.tech | 8080 | WordPress |
| enisi.tech | 8085 | WordPress |
| halalbankkosova.me | 8090 | WordPress |
| supabase.shabanejupi.tech | 8000 | Supabase |
| meet.shabanejupi.tech | 8095 | Jitsi Meet |
| dashboard.shabanejupi.tech | 5601 | OpenSearch Dashboards |
| os.shabanejupi.tech | 9200 | OpenSearch |
| search.shabanejupi.tech | 8097 | Meilisearch |
| audit.spacecode.tech | 8002 | Umami Analytics |
| ssh.spacecode.tech | 22 | SSH via Cloudflare tunnel |

## Deploy from Windows (one script)

1. Open **PowerShell** (right-click → Run as Administrator)
2. Navigate to this folder:
   ```
   cd C:\path\to\linux-install
   ```
3. Allow scripts to run (once):
   ```
   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
   ```
4. Run:
   ```
   .\windows-launch.ps1
   ```
5. Enter when prompted:
   - Root password for 185.174.211.45 (from your VPS provider panel)
   - Cloudflare API token

That's it. The script uploads everything and configures the server fully automatically (~10 min).

All generated passwords are saved at `/root/server-credentials.txt` on the server.

## What happens automatically

- Installs Docker, cloudflared, UFW firewall
- Generates secure random passwords for every service
- Writes all `.env` files
- Fetches Cloudflare tunnel tokens via API
- Starts all Docker services
- Registers 4 Cloudflare tunnels as systemd services
- Clones and starts Supabase

## After deployment

**WordPress sites** need a one-time wizard (just your site name + admin email):
- https://shabanejupi.tech/wp-admin/install.php
- https://enisi.tech/wp-admin/install.php
- https://halalbankkosova.me/wp-admin/install.php

**Umami default login:** `admin` / `umami` — change it immediately.

## SSH via Cloudflare tunnel (from Windows)

To SSH via `ssh.spacecode.tech` without exposing port 22 publicly:

1. Install cloudflared on Windows: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/
2. Add to `C:\Users\YourName\.ssh\config`:
   ```
   Host ssh.spacecode.tech
       ProxyCommand cloudflared access ssh --hostname %h
   ```
3. Connect: `ssh root@ssh.spacecode.tech`

## Useful server commands

```bash
# View all running containers
docker ps

# View credentials
cat /root/server-credentials.txt

# Logs for a service
docker compose -f /opt/apps/linux-install/wordpress-shabanejupi/docker-compose.yml logs -f

# Cloudflare tunnel status
systemctl status cloudflared-*

# Restart a service
docker compose -f /opt/apps/linux-install/umami/docker-compose.yml restart
```
