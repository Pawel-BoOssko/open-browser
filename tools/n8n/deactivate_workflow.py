"""Deactivate an n8n workflow by ID.

Usage: python deactivate_workflow.py <workflow_id>
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

req = urllib.request.Request(
    f"{base}/api/v1/workflows/{wid}/deactivate",
    data=b"{}",
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
print("deactivated_id", obj.get("id"))
print("name", obj.get("name"))
print("active", obj.get("active"))
