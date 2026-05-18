# tools/n8n/

**Purpose:** Python scripts for managing n8n workflows via the n8n Public API, plus Supabase/Postgres queries. Designed to be called from Open Browser via PowerShell (`python tools/n8n/<script>.py`).

**Created by:** Claude Code, 2026-05-18. Based on scripts originally developed by a previous agent in `D:\temp\bridge-runtime\n8n\tools\`.

**Rules:**
1. All scripts take parameters from command-line arguments — no hardcoded workflow IDs, payload paths, or schema names.
2. Secrets are read from `config/local/n8n/` (git-ignored).
3. Export files are written to `tools/n8n/exports/`.
4. Each script has a docstring — read it for usage: `python <script>.py` (no args shows help).

**Scripts:**
| Script | Purpose | Usage |
|--------|---------|-------|
| `list_workflows.py` | List all workflows | `python list_workflows.py` |
| `get_workflow.py` | Fetch one workflow by ID | `python get_workflow.py <id>` |
| `create_workflow.py` | Create workflow from local JSON | `python create_workflow.py <payload.json>` |
| `update_workflow.py` | Update workflow from local JSON | `python update_workflow.py <id> <payload.json>` |
| `activate_workflow.py` | Activate a workflow | `python activate_workflow.py <id>` |
| `deactivate_workflow.py` | Deactivate a workflow | `python deactivate_workflow.py <id>` |
| `list_executions.py` | List execution history | `python list_executions.py [workflow_id] [limit]` |
| `supabase_query.py` | Run SQL against Supabase | `python supabase_query.py "select ..."` |

**Prerequisites:**
- Python 3 with `pg8000` for supabase_query: `pip install pg8000`
- n8n API key configured in `config/local/n8n/n8n_config.json`
- Supabase URL in `config/local/n8n/supabase_config.env`

**Known n8n API limitations (preserved from original work):**
- `settings` is required in create/update payloads. Use `"settings": {}` for safe defaults.
- Do NOT include `id`, `createdAt`, `updatedAt`, `shared`, `triggerCount` in create payloads.
- `POST /api/v1/workflows/{id}/run` does NOT work on this instance (returns 405).
- Use webhooks for automated testing: add webhook node → activate → call webhook → deactivate.
- After every create/update, verify with `get_workflow.py` — never trust the local patch was applied.
