"""Upload or update a single file in a GitHub repository via the Contents API.

Usage: python github_put_file.py <local_path> <repo_path> [commit_message]

Config: config/local/github/github_token.txt
        config/local/github/github_repo.json
Output: tools/github/exports/put_file_result.json
"""
import base64
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(".")
TOKEN_PATH = ROOT / "config" / "local" / "github" / "github_token.txt"
CONFIG_PATH = ROOT / "config" / "local" / "github" / "github_repo.json"
RESULT = ROOT / "tools" / "github" / "exports" / "put_file_result.json"

token = TOKEN_PATH.read_text(encoding="utf-8").strip()
config = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))

owner = config["owner"]
repo = config["repo"]
branch = config.get("branch", "main")

if len(sys.argv) < 3:
    print("USAGE: python tools/github/github_put_file.py LOCAL_PATH REPO_PATH [MESSAGE]")
    raise SystemExit(2)

local_path = Path(sys.argv[1])
repo_path = sys.argv[2].lstrip("/")
message = sys.argv[3] if len(sys.argv) > 3 else "Bridge publish file"

if not local_path.exists():
    print("LOCAL_FILE_NOT_FOUND", str(local_path))
    raise SystemExit(3)

data_bytes = local_path.read_bytes()
content_b64 = base64.b64encode(data_bytes).decode("ascii")

headers = {
    "Authorization": "Bearer " + token,
    "Accept": "application/vnd.github+json",
    "X-GitHub-Api-Version": "2022-11-28",
    "User-Agent": "open-browser-tools",
}


def request_json(method, url, body=None):
    payload = None
    h = dict(headers)
    if body is not None:
        payload = json.dumps(body).encode("utf-8")
        h["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=payload, headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            raw = resp.read().decode("utf-8")
            return resp.status, json.loads(raw) if raw else {}
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", errors="replace")
        if e.code == 404:
            return 404, {"message": "not found"}
        print("HTTP_ERROR", e.code)
        print(raw[:800])
        raise SystemExit(1)


encoded = urllib.parse.quote(repo_path, safe="")
url = f"https://api.github.com/repos/{owner}/{repo}/contents/{encoded}"

get_status, existing = request_json("GET", url + "?ref=" + urllib.parse.quote(branch))
body = {"message": message, "content": content_b64, "branch": branch}
if get_status == 200 and existing.get("sha"):
    body["sha"] = existing["sha"]

put_status, result = request_json("PUT", url, body)
content = result.get("content", {})
commit = result.get("commit", {})

safe = {
    "status": put_status,
    "owner": owner,
    "repo": repo,
    "branch": branch,
    "local_path": str(local_path),
    "repo_path": repo_path,
    "local_bytes": len(data_bytes),
    "content_sha": content.get("sha"),
    "commit_sha": commit.get("sha"),
    "html_url": content.get("html_url"),
}
RESULT.parent.mkdir(parents=True, exist_ok=True)
RESULT.write_text(json.dumps(safe, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("GITHUB_PUT_FILE_OK", owner + "/" + repo, repo_path, "status=" + str(put_status))
