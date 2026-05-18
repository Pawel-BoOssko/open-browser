# tools/github/

**Purpose:** Python scripts for publishing files to GitHub via the Contents API. No local git required — file upload happens directly through the API.

**Created by:** Claude Code, 2026-05-18. Based on scripts originally developed in `D:\temp\bridge-runtime\tools\`.

**Rules:**
1. All scripts take parameters from command-line arguments — no hardcoded repo names or paths.
2. Secrets are read from `config/local/github/` (git-ignored).
3. Export files are written to `tools/github/exports/`.
4. Each script has a docstring — read it for usage.
5. Never print or log the GitHub token. Never put it in a CMD command line.

**Scripts:**
| Script | Purpose | Usage |
|--------|---------|-------|
| `github_whoami.py` | Check authenticated user | `python github_whoami.py` |
| `github_find_repo.py` | Find repo and save config | `python github_find_repo.py <repo_name>` |
| `github_put_file.py` | Upload/update a single file | `python github_put_file.py <local> <repo_path> [msg]` |
| `github_check_manifest.py` | Verify all manifest files exist locally | `python github_check_manifest.py [manifest.json]` |
| `github_publish_manifest.py` | Publish all files from a manifest | `python github_publish_manifest.py [manifest.json] [--dry-run]` |

**Publishing workflow:**
1. `github_whoami.py` — confirm authentication works
2. `github_find_repo.py <name>` — locate the target repo
3. `github_check_manifest.py` — verify all local files exist
4. `github_publish_manifest.py --dry-run` — preview what will be published
5. `github_publish_manifest.py` — publish for real

**Manifest format** (`tools/github/publish-manifest.json`):
```json
{
  "repo": "owner/repo",
  "branch": "main",
  "files": [
    {"local": "path/to/file.txt", "repo": "docs/file.txt"}
  ]
}
```

**Known rules (preserved from original work):**
- Publish file by file. Do not batch silently.
- After every publish, verify by reading the result JSON.
- Only publish after dry-run succeeds.
- Do not publish secrets or tokens.
- Use `github_put_file.py` for single files, manifest for batched releases.
