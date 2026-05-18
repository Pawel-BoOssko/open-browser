# Working with GitHub from Open Browser

This guide covers publishing files to GitHub via the Contents API using the scripts in `tools/github/`.

## Setup check

Confirm you can authenticate:
```
python tools\github\github_whoami.py
```

Find the target repo:
```
python tools\github\github_find_repo.py bridge-poc
```
This saves the repo config to `config/local/github/github_repo.json`.

## Publishing a single file

```
python tools\github\github_put_file.py <local_file> <path_in_repo> "commit message"
```

Example:
```
python tools\github\github_put_file.py docs\README.md docs\README.md "Update docs"
```

After the command, verify the result:
```
Get-Content tools\github\exports\put_file_result.json
```

## Publishing multiple files (manifest)

1. Create or edit `tools/github/publish-manifest.json`:
```json
{
  "repo": "owner/repo",
  "branch": "main",
  "files": [
    {"local": "docs/README.md", "repo": "docs/README.md"},
    {"local": "CHANGELOG.md", "repo": "CHANGELOG.md"}
  ]
}
```

2. Check all files exist:
```
python tools\github\github_check_manifest.py
```

3. Dry run to preview:
```
python tools\github\github_publish_manifest.py --dry-run
```

4. Publish for real:
```
python tools\github\github_publish_manifest.py
```

## Important rules

1. **Always verify after each publish.** Read the result JSON file — never assume success.
2. **Never print the token.** It lives in `config/local/github/` and the scripts read it automatically.
3. **Publish file by file.** For multiple files, use the manifest — it handles each file sequentially.
4. **Dry-run first.** Always `--dry-run` before real publish.
5. **Do not publish secrets or tokens** to GitHub. The `config/local/` directory is git-ignored for a reason.
6. **Check authentication first.** Run `github_whoami.py` — if it fails, nothing else will work.

## Secrets

GitHub token and repo config are stored in `config/local/github/` (not accessible via chat).
The scripts read them automatically — you don't need to pass credentials.
