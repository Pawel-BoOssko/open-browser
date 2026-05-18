"""Publish multiple files to GitHub from a manifest JSON file.

Usage: python github_publish_manifest.py [manifest.json] [--dry-run]

Manifest format:
{
  "repo": "owner/repo",
  "branch": "main",
  "files": [
    {"local": "path/to/file.txt", "repo": "docs/file.txt"},
    ...
  ]
}

Each file is uploaded via github_put_file.py.
"""
import json
import subprocess
import sys
from pathlib import Path

MANIFEST = Path(sys.argv[1]) if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else Path("tools/github/publish-manifest.json")
DRY_RUN = "--dry-run" in sys.argv
RESULT = Path("tools/github/exports/publish_manifest_result.json")

if not MANIFEST.exists():
    print("MANIFEST_NOT_FOUND", str(MANIFEST))
    raise SystemExit(2)

data = json.loads(MANIFEST.read_text(encoding="utf-8"))
files = data.get("files", [])
results = []

PUT_FILE_SCRIPT = str(Path("tools/github/github_put_file.py"))

for item in files:
    local = item["local"]
    repo = item.get("repo", item.get("repo_path", ""))
    if not Path(local).exists():
        results.append({"local": local, "repo": repo, "status": "missing-local"})
        print("MISSING_LOCAL", local)
        continue

    if DRY_RUN:
        size = Path(local).stat().st_size
        results.append({"local": local, "repo": repo, "status": "dry-run", "bytes": size})
        print("DRY_RUN", local, "=>", repo, "bytes=" + str(size))
        continue

    cmd = [sys.executable, PUT_FILE_SCRIPT, local, repo, "Publish from manifest"]
    proc = subprocess.run(cmd, text=True, capture_output=True)
    results.append(
        {
            "local": local,
            "repo": repo,
            "returncode": proc.returncode,
            "stdout": proc.stdout.strip(),
            "stderr": proc.stderr.strip(),
        }
    )
    print(proc.stdout.strip())
    if proc.returncode != 0:
        print(proc.stderr.strip())
        break

RESULT.parent.mkdir(parents=True, exist_ok=True)
RESULT.write_text(
    json.dumps(
        {"manifest": str(MANIFEST), "dry_run": DRY_RUN, "count": len(results), "results": results},
        ensure_ascii=False,
        indent=2,
    )
    + "\n",
    encoding="utf-8",
)
print("MANIFEST_PUBLISH_DONE", "dry_run=" + str(DRY_RUN), "count=" + str(len(results)))
