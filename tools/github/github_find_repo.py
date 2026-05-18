"""Find a repository by name among the user's accessible repos and save its config.

Usage: python github_find_repo.py <repo_name>
Example: python github_find_repo.py bridge-poc

Config: config/local/github/github_token.txt
Output: config/local/github/github_repo.json (owner, repo, branch)
"""
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

token = Path("config/local/github/github_token.txt").read_text(encoding="utf-8").strip()
search_name = sys.argv[1].lower() if len(sys.argv) > 1 else "bridge-poc"

headers = {
    "Authorization": "Bearer " + token,
    "Accept": "application/vnd.github+json",
    "X-GitHub-Api-Version": "2022-11-28",
    "User-Agent": "open-browser-tools",
}

url = "https://api.github.com/user/repos?per_page=100&affiliation=owner,collaborator,organization_member"
req = urllib.request.Request(url, headers=headers)

try:
    with urllib.request.urlopen(req, timeout=20) as resp:
        repos = json.loads(resp.read().decode("utf-8"))
except urllib.error.HTTPError as e:
    print("HTTP_ERROR", e.code)
    raise SystemExit(1)

matches = [r for r in repos if r.get("name", "").lower() == search_name]
print("REPO_COUNT", len(repos))
print(f"MATCHES_FOR_{search_name.upper()}", len(matches))

if len(matches) != 1:
    out = Path("tools/github/exports/repos_safe.json")
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(
        json.dumps(
            [
                {
                    "full_name": r.get("full_name"),
                    "name": r.get("name"),
                    "default_branch": r.get("default_branch"),
                }
                for r in repos
            ],
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    raise SystemExit(2)

r = matches[0]
config = {
    "owner": r.get("owner", {}).get("login"),
    "repo": r.get("name"),
    "branch": r.get("default_branch", "main"),
}
out = Path("config/local/github/github_repo.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(config, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("REPO_CONFIG_WRITTEN", config["owner"] + "/" + config["repo"], "branch=" + config["branch"])
