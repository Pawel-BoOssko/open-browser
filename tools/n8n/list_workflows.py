"""List all workflows from n8n API.

Usage: python list_workflows.py

Saves to: tools/n8n/exports/workflows_list.json
"""
import json
import urllib.request
from pathlib import Path

secrets = Path("config/local/n8n/n8n_config.json").read_text(encoding="utf-8")
cfg = json.loads(secrets)
base = cfg["base_url"].rstrip("/")

req = urllib.request.Request(
    f"{base}/api/v1/workflows",
    headers={"X-N8N-API-KEY": cfg.get("api_key"), "Accept": "application/json"},
)
data = urllib.request.urlopen(req, timeout=20).read().decode("utf-8")

out = Path("tools/n8n/exports/workflows_list.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(data, encoding="utf-8")

obj = json.loads(data)
items = obj.get("data", obj if isinstance(obj, list) else [])
print("workflows_count", len(items))
for w in items[:80]:
    print(w.get("id"), "|", w.get("active"), "|", w.get("name"))
