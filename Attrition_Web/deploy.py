#!/usr/bin/env python3
"""Attrition deploy script — nukes remote, uploads fresh, rebuilds Docker stack."""

import paramiko
import os
import sys

# ── Config ──────────────────────────────────────────────
REMOTE_HOST = "192.168.1.110"
REMOTE_USER = "root"
REMOTE_PASS = "12345"
REMOTE_DIR  = "/opt/attrition"
PROJECT_DIR = os.path.dirname(os.path.abspath(__file__))

EXCLUDE = {
    "node_modules", ".next", "bin", "obj", ".git",
    "uploads", "docs", "reflect", "__pycache__", ".env.local",
    ".env.example", "web-game-test.sln", ".gitignore",
    "tsconfig.tsbuildinfo", ".eslintrc.json", "README.md",
    "Attrition.API.http", ".system_generated", "scratch",
    "brain", ".gemini", "client_secret*",
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
    out = stdout.read().decode('utf-8', errors='replace').strip()
    err = stderr.read().decode('utf-8', errors='replace').strip()
    if out:
        print(out.encode('ascii', errors='replace').decode('ascii'))
    if err:
        print(err, file=sys.stderr)
    if check and exit_code != 0:
        print(f"Command failed with exit code {exit_code}", file=sys.stderr)
        sys.exit(1)
    return out, err, exit_code


def should_exclude(name: str) -> bool:
    """Check if a file or directory should be excluded from upload."""
    if name in EXCLUDE:
        return True
    # Wildcard match for client_secret* files
    if name.startswith("client_secret"):
        return True
    return False


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
    print("  Attrition Deploy (FULL NUKE)")
    print(f"  Target: {REMOTE_USER}@{REMOTE_HOST}:{REMOTE_DIR}")
    print("=" * 50)

    client = ssh_connect()

    try:
        # ── Step 1: Stop containers ──
        print("\n[1/5] Stopping existing containers...")
        ssh_exec(client, f"cd {REMOTE_DIR} && docker compose down 2>/dev/null || true", check=False)

        # ── Step 2: Nuke remote directory (except Docker volumes) ──
        print("\n[2/5] Nuking remote directory...")
        ssh_exec(client, f"rm -rf {REMOTE_DIR}", check=False)
        ssh_exec(client, f"mkdir -p {REMOTE_DIR}")
        print("Remote directory wiped clean.")

        # ── Step 3: Upload all project files ──
        print("\n[3/5] Uploading project files...")
        sftp = client.open_sftp()
        upload_directory(sftp, PROJECT_DIR, REMOTE_DIR)
        sftp.close()
        print("All files uploaded.\n")

        # ── Step 4: Ensure .env exists on remote ──
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

        # ── Step 5: Build and start Docker stack ──
        print("[4/5] Building and starting containers (this takes a minute)...")
        ssh_exec(client, f"cd {REMOTE_DIR} && docker compose up -d --build")

        print("\n[5/5] Waiting for services to start...")
        import time
        time.sleep(10)

        ssh_exec(client, f"cd {REMOTE_DIR} && docker compose ps")

        print("\n" + "=" * 50)
        print("  Deploy complete!")
        print(f"  Web:  http://{REMOTE_HOST}:3000")
        print(f"  API:  via nginx on :3000/api")
        print("=" * 50)

    finally:
        client.close()


if __name__ == "__main__":
    main()