"""Create a new workflow in n8n from a local JSON payload file.

Usage: python create_workflow.py <payload_file>
Example: python create_workflow.py workflows/my_draft.json

The JSON must contain at minimum: name, nodes, connections, settings.
Use "settings": {} for safe defaults.
Do not include: id, createdAt, updatedAt, shared, triggerCount.
"""
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

secrets = Path("config/local/n8n/n8n_config.json").read_text(encoding="utf-8")
cfg = json.loads(secrets)
base = cfg["base_url"].rstrip("/")

payload_path = Path(sys.argv[1])
payload = json.loads(payload_path.read_text(encoding="utf-8"))

body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
req = urllib.request.Request(
    f"{base}/api/v1/workflows",
    data=body,
    headers={
        "X-N8N-API-KEY": cfg.get("api_key"),
        "Accept": "application/json",
        "Content-Type": "application/json",
    },
    method="POST",
)

try:
    data = urllib.request.urlopen(req, timeout=60).read().decode("utf-8")
except urllib.error.HTTPError as e:
    data = e.read().decode("utf-8", errors="replace")
    print("http_error", e.code)
    print(data[:2000])
    raise SystemExit(1)

obj = json.loads(data)
print("created_id", obj.get("id"))
print("name", obj.get("name"))
print("active", obj.get("active"))
print("nodes", len(obj.get("nodes") or []))

# Save response
out = Path(f"tools/n8n/exports/create_{obj.get('id')}_response.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8")
print("saved", out)
