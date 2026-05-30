import sys, json, base64, os, urllib.request, time

def api(url):
    req = urllib.request.Request(url, headers={"User-Agent": "skill-dl", "Accept": "application/vnd.github+json"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.load(r)

def dl_repo(owner, repo, branch, subpath, dest_root):
    """Download files under subpath from repo into dest_root (skill folder)."""
    tree = api(f"https://api.github.com/repos/{owner}/{repo}/git/trees/{branch}?recursive=1")["tree"]
    blobs = [t for t in tree if t["type"] == "blob"]
    if subpath:
        blobs = [t for t in blobs if t["path"].startswith(subpath + "/") or t["path"] == subpath]
    for t in blobs:
        rel = t["path"]
        if subpath:
            rel = rel[len(subpath):].lstrip("/")
        out = os.path.join(dest_root, rel)
        os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
        node = api(f"https://api.github.com/repos/{owner}/{repo}/contents/{t['path']}?ref={branch}")
        data = base64.b64decode(node["content"])
        with open(out, "wb") as f:
            f.write(data)
        print("WROTE", out, len(data))
        time.sleep(0.05)

if __name__ == "__main__":
    base = os.path.dirname(os.path.abspath(__file__))
    skills = os.path.join(base, "skills")
    # impeccable
    dl_repo("watchi666", "impeccable_claude_code", "main", "", os.path.join(skills, "impeccable"))
    # emil design eng
    dl_repo("emilkowalski", "skill", "main", "skills/emil-design-eng", os.path.join(skills, "emil-design-eng"))
    print("DONE")
