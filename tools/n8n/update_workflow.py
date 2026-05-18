"""Update an existing n8n workflow from a local JSON payload file.

Usage: python update_workflow.py <workflow_id> <payload_file>
Example: python update_workflow.py XiHVp7y3Pptm9cfW workflows/my_update.json
"""
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

secrets = Path("config/local/n8n/n8n_config.json").read_text(encoding="utf-8")
cfg = json.loads(secrets)
base = cfg["base_url"].rstrip("/")

wid = sys.argv[1]
payload_path = Path(sys.argv[2])
payload = json.loads(payload_path.read_text(encoding="utf-8"))

body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
req = urllib.request.Request(
    f"{base}/api/v1/workflows/{wid}",
    data=body,
    headers={
        "X-N8N-API-KEY": cfg.get("api_key"),
        "Accept": "application/json",
        "Content-Type": "application/json",
    },
    method="PUT",
)

try:
    data = urllib.request.urlopen(req, timeout=60).read().decode("utf-8")
except urllib.error.HTTPError as e:
    data = e.read().decode("utf-8", errors="replace")
    print("http_error", e.code)
    print(data[:2000])
    raise SystemExit(1)

obj = json.loads(data)
print("updated_id", obj.get("id"))
print("name", obj.get("name"))
print("active", obj.get("active"))
print("nodes", len(obj.get("nodes") or []))

out = Path(f"tools/n8n/exports/update_{wid}_response.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8")
print("saved", out)
