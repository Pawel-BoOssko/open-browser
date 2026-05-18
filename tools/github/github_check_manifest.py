"""Check that all files listed in a publish manifest exist locally.

Usage: python github_check_manifest.py [manifest.json]
"""
import json
import sys
from pathlib import Path

MANIFEST = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("tools/github/publish-manifest.json")
RESULT = Path("tools/github/exports/manifest_check_result.json")

data = json.loads(MANIFEST.read_text(encoding="utf-8"))
files = data.get("files", [])
rows = []
missing = []

for item in files:
    local = Path(item["local"])
    repo = item.get("repo", item.get("repo_path", ""))
    exists = local.exists()
    size = local.stat().st_size if exists else 0
    rows.append({"local": str(local), "repo": repo, "exists": exists, "bytes": size})
    if not exists:
        missing.append(str(local))

RESULT.parent.mkdir(parents=True, exist_ok=True)
RESULT.write_text(
    json.dumps(
        {
            "manifest": str(MANIFEST),
            "repo": data.get("repo"),
            "branch": data.get("branch"),
            "count": len(files),
            "missing": missing,
            "files": rows,
        },
        ensure_ascii=False,
        indent=2,
    )
    + "\n",
    encoding="utf-8",
)

print("MANIFEST_CHECK", "OK" if not missing else "MISSING", "count=" + str(len(files)), "missing=" + str(len(missing)))
if missing:
    raise SystemExit(2)
