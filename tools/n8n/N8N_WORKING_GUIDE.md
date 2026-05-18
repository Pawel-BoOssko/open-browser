# Working with n8n from Open Browser

This guide covers how to manage n8n workflows via the Public API using the scripts in `tools/n8n/`. All commands below are PowerShell — send them through the `PS` envelope.

## Setup check

Verify the tools are available:
```
Get-ChildItem tools\n8n\*.py
```

Check API connectivity:
```
python tools\n8n\list_workflows.py
```

Expected: a list of workflows with IDs, names, and active status.

## Common operations

### List all workflows
```
python tools\n8n\list_workflows.py
```

### Fetch a workflow for inspection
```
python tools\n8n\get_workflow.py <workflow_id>
```
Saves to `tools/n8n/exports/<id>__<name>.json`. Read it back:
```
Get-Content tools\n8n\exports\<id>__<name>.json
```

### Create a new workflow
First, prepare a JSON payload file. Minimum required fields: name, nodes, connections, settings.
Use `"settings": {}` if unsure. Then:
```
python tools\n8n\create_workflow.py <payload.json>
```

### Update an existing workflow
Edit the downloaded JSON file, then:
```
python tools\n8n\update_workflow.py <workflow_id> <updated.json>
```

### Activate / deactivate
```
python tools\n8n\activate_workflow.py <workflow_id>
python tools\n8n\deactivate_workflow.py <workflow_id>
```

### Check execution history
```
python tools\n8n\list_executions.py <workflow_id>
```

### Query Supabase directly
```
python tools\n8n\supabase_query.py "select * from schema.table limit 10"
```

## Important rules

1. **Always verify after create/update.** Run `get_workflow.py` after every change to confirm the server state matches what you expect. Never trust that a patch was applied.
2. **Workflow run by API does NOT work** (returns 405). Use webhooks for triggering.
3. **For webhook tests:** create a Webhook node in the workflow → activate workflow → call the webhook URL → deactivate when done.
4. **After creating a workflow**, the response is saved to `tools/n8n/exports/create_<id>_response.json`.
5. **JSON payloads for create must NOT contain:** id, createdAt, updatedAt, shared, triggerCount.
6. **Settings field is required** in create/update. Safe default: `"settings": {}`.

## Secrets

API keys are stored in `config/local/n8n/` (not accessible via chat for security).
The scripts read them automatically — you don't need to pass credentials.

## Exports

All API responses and execution data are saved to `tools/n8n/exports/`.
Browse them with:
```
Get-ChildItem tools\n8n\exports\
```
