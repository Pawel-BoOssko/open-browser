"""List recent executions from n8n API.

Usage:
  python list_executions.py                          # all workflows, last 100
  python list_executions.py <workflow_id>            # filter by workflow
  python list_executions.py <workflow_id> <limit>    # filter + custom limit

Saves to: tools/n8n/exports/executions.json
"""
import json
import sys
import urllib.request
from pathlib import Path

secrets = Path("config/local/n8n/n8n_config.json").read_text(encoding="utf-8")
cfg = json.loads(secrets)
base = cfg["base_url"].rstrip("/")

wid = sys.argv[1] if len(sys.argv) > 1 else None
limit = int(sys.argv[2]) if len(sys.argv) > 2 else 100
include_data = "true" if len(sys.argv) > 3 else "false"

url = f"{base}/api/v1/executions?limit={limit}&includeData={include_data}"
if wid:
    url += f"&workflowId={wid}"

req = urllib.request.Request(
    url,
    headers={"X-N8N-API-KEY": cfg.get("api_key"), "Accept": "application/json"},
)
data = urllib.request.urlopen(req, timeout=60).read().decode("utf-8")

out = Path("tools/n8n/exports/executions.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(data, encoding="utf-8")

obj = json.loads(data)
items = obj.get("data", obj if isinstance(obj, list) else [])
print("executions_count", len(items))
if items:
    print("keys", sorted(items[0].keys()))
for e in items[:30]:
    print(
        e.get("id"), "|",
        e.get("workflowId"), "|",
        e.get("status"), "|",
        e.get("startedAt"), "|",
        e.get("stoppedAt"),
    )
