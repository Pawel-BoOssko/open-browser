"""Check the authenticated GitHub user.

Usage: python github_whoami.py

Config: config/local/github/github_token.txt
Output: tools/github/exports/whoami.json
"""
import json
import urllib.error
import urllib.request
from pathlib import Path

token = Path("config/local/github/github_token.txt").read_text(encoding="utf-8").strip()

headers = {
    "Authorization": "Bearer " + token,
    "Accept": "application/vnd.github+json",
    "X-GitHub-Api-Version": "2022-11-28",
    "User-Agent": "open-browser-tools",
}

req = urllib.request.Request("https://api.github.com/user", headers=headers)

try:
    with urllib.request.urlopen(req, timeout=20) as resp:
        data = json.loads(resp.read().decode("utf-8"))
except urllib.error.HTTPError as e:
    print("HTTP_ERROR", e.code)
    raise SystemExit(1)

safe = {"login": data.get("login"), "id": data.get("id"), "html_url": data.get("html_url")}

out = Path("tools/github/exports/whoami.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(safe, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("GITHUB_USER_OK", safe.get("login"))
