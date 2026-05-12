# 08 — Deployment

Docker Compose production setup, deploy script, and Cloudflare tunnel notes.

---

## Production Environment

- **Server**: Debian 13 at `192.168.1.110`
- **Access**: `root@192.168.1.110` (password: `12345`)
- **Domain**: `attrition.hault.io.vn` → Cloudflare Tunnel → server
- **Protocol**: HTTP internally (Cloudflare handles HTTPS)

---

## Prerequisites on Server

Ensure Docker and Docker Compose are installed on the Debian 13 server:

```bash
# Docker (if not installed)
curl -fsSL https://get.docker.com | sh

# Docker Compose V2 comes with Docker Engine
docker compose version
```

---

## Deploy Script (`deploy.py`)

Place at project root. Cross-platform Python script. Requires `paramiko`:

```bash
pip install paramiko
```

```python
#!/usr/bin/env python3
"""Attrition deploy script — copies project to remote server and starts Docker stack."""

import paramiko
import os
import sys
import stat

# ── Config ──────────────────────────────────────────────
REMOTE_HOST = "192.168.1.110"
REMOTE_USER = "root"
REMOTE_PASS = "12345"
REMOTE_DIR  = "/opt/attrition"
PROJECT_DIR = os.path.dirname(os.path.abspath(__file__))

EXCLUDE = {
    "node_modules", ".next", "bin", "obj", ".git",
    "uploads", "docs", "__pycache__", ".env.local",
}
# ────────────────────────────────────────────────────────


def ssh_connect():
    """Create and return an SSH client connected to the remote server."""
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    print(f"Connecting to {REMOTE_USER}@{REMOTE_HOST}...")
    client.connect(REMOTE_HOST, username=REMOTE_USER, password=REMOTE_PASS)
    print("Connected.")
    return client


def ssh_exec(client: paramiko.SSHClient, cmd: str, check=True):
    """Execute a command on the remote server and print output."""
    print(f"  > {cmd}")
    stdin, stdout, stderr = client.exec_command(cmd)
    exit_code = stdout.channel.recv_exit_status()
    out = stdout.read().decode().strip()
    err = stderr.read().decode().strip()
    if out:
        print(out)
    if err:
        print(err, file=sys.stderr)
    if check and exit_code != 0:
        print(f"Command failed with exit code {exit_code}", file=sys.stderr)
        sys.exit(1)
    return out, err, exit_code


def should_exclude(name: str) -> bool:
    """Check if a file or directory should be excluded from upload."""
    return name in EXCLUDE


def upload_directory(sftp: paramiko.SFTPClient, local_path: str, remote_path: str):
    """Recursively upload a directory via SFTP."""
    # Create remote directory
    try:
        sftp.stat(remote_path)
    except FileNotFoundError:
        sftp.mkdir(remote_path)

    for item in os.listdir(local_path):
        if should_exclude(item):
            continue

        local_item = os.path.join(local_path, item)
        remote_item = f"{remote_path}/{item}"

        if os.path.isdir(local_item):
            upload_directory(sftp, local_item, remote_item)
        else:
            size = os.path.getsize(local_item)
            size_str = f"{size / 1024:.1f}KB" if size < 1048576 else f"{size / 1048576:.1f}MB"
            print(f"  Uploading: {os.path.relpath(local_item, PROJECT_DIR)} ({size_str})")
            sftp.put(local_item, remote_item)


def main():
    print("=" * 50)
    print("  Attrition Deploy")
    print(f"  Target: {REMOTE_USER}@{REMOTE_HOST}:{REMOTE_DIR}")
    print("=" * 50)

    client = ssh_connect()

    try:
        # Create remote directory
        ssh_exec(client, f"mkdir -p {REMOTE_DIR}")

        # Upload project files
        print("\nUploading project files...")
        sftp = client.open_sftp()
        upload_directory(sftp, PROJECT_DIR, REMOTE_DIR)
        sftp.close()
        print("Files synced.\n")

        # Check if .env exists, if not copy from example
        _, _, code = ssh_exec(
            client,
            f"test -f {REMOTE_DIR}/.env",
            check=False,
        )
        if code != 0:
            print("No .env found — copying from .env.example...")
            ssh_exec(client, f"cp {REMOTE_DIR}/.env.example {REMOTE_DIR}/.env")
            print("Created .env from example.")
            print(f"⚠  Please edit {REMOTE_DIR}/.env with real values before first run!")
            print("   Then run this script again.")
            return

        # Build and start Docker stack
        print("Stopping existing containers...")
        ssh_exec(client, f"cd {REMOTE_DIR} && docker compose down", check=False)

        print("Building and starting containers...")
        ssh_exec(client, f"cd {REMOTE_DIR} && docker compose up -d --build")

        print("\nWaiting for services to start...")
        import time
        time.sleep(10)

        ssh_exec(client, f"cd {REMOTE_DIR} && docker compose ps")

        print("\n" + "=" * 50)
        print("  Deploy complete!")
        print(f"  Web:  http://{REMOTE_HOST}:3000")
        print(f"  API:  http://{REMOTE_HOST}:5000")
        print("=" * 50)

    finally:
        client.close()


if __name__ == "__main__":
    main()
```

Run with:
```bash
python deploy.py
```

---

## Production `.env`

On the server, edit `/opt/attrition/.env`:

```env
POSTGRES_USER=attrition
POSTGRES_PASSWORD=<generate-a-strong-random-password>
POSTGRES_DB=attrition
REDIS_URL=redis:6379
JWT_SECRET=<generate-a-random-64-char-string>
JWT_ISSUER=attrition-api
JWT_AUDIENCE=attrition-web
JWT_ACCESS_TOKEN_EXPIRY_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRY_DAYS=7
API_URL=http://api:5000
MAX_AVATAR_SIZE_MB=5
UPLOAD_PATH=/app/uploads
```

Generate secrets:
```bash
# JWT Secret
openssl rand -base64 48

# Postgres password
openssl rand -base64 24
```

---

## Cloudflare Tunnel

The user manages the Cloudflare Tunnel separately. The tunnel should point to `http://192.168.1.110:3000` (the Next.js frontend). The Next.js app proxies `/api/*` to the backend internally within Docker.

No nginx or reverse proxy is needed. No SSL configuration is needed on the server.

---

## Useful Commands

```bash
# View logs
docker compose logs -f
docker compose logs -f web
docker compose logs -f api

# Restart a specific service
docker compose restart api

# Rebuild after code changes
docker compose up -d --build

# Enter a container
docker compose exec api bash
docker compose exec db psql -U attrition -d attrition

# Check database
docker compose exec db psql -U attrition -d attrition -c "SELECT * FROM \"Users\";"

# Backup database
docker compose exec db pg_dump -U attrition attrition > backup.sql

# Restore database
cat backup.sql | docker compose exec -T db psql -U attrition attrition
```

---

## Health Check

After deployment, verify:

1. `curl http://192.168.1.110:5000/health` → `{"status":"healthy"}`
2. `curl http://192.168.1.110:3000` → HTML page loads
3. Open browser to `http://192.168.1.110:3000` → site loads with glassmorphism UI
4. Login with `admin123` / `admin123` → should work and prompt password change
5. Music plays at 30% volume on first interaction

---

## Troubleshooting

| Issue | Fix |
|---|---|
| API can't connect to DB | Check `docker compose ps` — db must be healthy. Check connection string in `.env` |
| Next.js shows API errors | Check `API_URL` env var. Inside Docker it must be `http://api:5000` |
| Music doesn't autoplay | Expected — browsers require user interaction first. Click anywhere on the page. |
| Avatars not loading | Check the `uploads` volume is mounted. Check `FileUpload:UploadPath` in API config. |
| 502 on Cloudflare | Tunnel not pointing to `192.168.1.110:3000`, or containers are down. |
