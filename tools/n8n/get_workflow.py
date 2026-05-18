"""Fetch a workflow from n8n API by ID and save to local JSON.

Usage: python get_workflow.py <workflow_id>
Example: python get_workflow.py XiHVp7y3Pptm9cfW

Saves to: tools/n8n/exports/<id>__<name>.json
"""
import json
import sys
import urllib.request
from pathlib import Path

secrets = Path("config/local/n8n/n8n_config.json").read_text(encoding="utf-8")
cfg = json.loads(secrets)
base = cfg["base_url"].rstrip("/")
wid = sys.argv[1]

req = urllib.request.Request(
    f"{base}/api/v1/workflows/{wid}",
    headers={"X-N8N-API-KEY": cfg.get("api_key"), "Accept": "application/json"},
)
data = urllib.request.urlopen(req, timeout=30).read().decode("utf-8")
obj = json.loads(data)

name = obj.get("name", "workflow")
safe = "".join(c if c.isalnum() or c in ".- " else "" for c in name)[:80]
out = Path(f"tools/n8n/exports/{wid}__{safe}.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8")

print("saved", out)
print("name", name)
print("nodes", len(obj.get("nodes", [])))
print("active", obj.get("active"))
