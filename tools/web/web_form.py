"""Submit a web form using requests.

Usage: python web_form.py <url> <form_data.json>
Example: python web_form.py https://example.com/login login.json

The JSON file should contain form field names and values:
{
  "username": "myuser",
  "password": "mypass"
}

Prerequisite: pip install requests
"""
import json
import sys
from pathlib import Path

try:
    import requests
except ImportError:
    print("MISSING_DEPENDENCIES — run: pip install requests")
    raise SystemExit(1)

url = sys.argv[1]
data_path = Path(sys.argv[2])

form_data = json.loads(data_path.read_text(encoding="utf-8"))

session = requests.Session()
resp = session.post(
    url,
    data=form_data,
    timeout=30,
    headers={"User-Agent": "open-browser-tools/1.0"},
    allow_redirects=True,
)

print(f"HTTP {resp.status_code}")
print(f"Final URL: {resp.url}")
print(f"Response length: {len(resp.text)} bytes")

out = Path("tools/web/exports")
out.mkdir(parents=True, exist_ok=True)
(Path("tools/web/exports/last_form_response.html")).write_text(resp.text, encoding="utf-8")
print("Response saved to tools/web/exports/last_form_response.html")
