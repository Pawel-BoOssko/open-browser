"""Fetch a web page and extract content using requests + BeautifulSoup.

Usage: python web_fetch.py <url> [css_selector]
Example: python web_fetch.py https://example.com "div.content"

Prerequisite: pip install requests beautifulsoup4
"""
import sys
from pathlib import Path

try:
    import requests
    from bs4 import BeautifulSoup
except ImportError:
    print("MISSING_DEPENDENCIES — run: pip install requests beautifulsoup4")
    raise SystemExit(1)

url = sys.argv[1]
selector = sys.argv[2] if len(sys.argv) > 2 else "body"

resp = requests.get(url, timeout=30, headers={"User-Agent": "open-browser-tools/1.0"})
resp.raise_for_status()

soup = BeautifulSoup(resp.text, "html.parser")
elements = soup.select(selector)

if not elements:
    print(f"NO_MATCHES for selector: {selector}")
    print(f"Page title: {soup.title.string if soup.title else 'N/A'}")
    raise SystemExit(2)

for el in elements[:10]:
    text = el.get_text(strip=True)
    if text:
        print(text[:2000])
        print("---")

out = Path("tools/web/exports")
out.mkdir(parents=True, exist_ok=True)
(Path("tools/web/exports/last_fetch.html")).write_text(resp.text, encoding="utf-8")
print(f"Full HTML saved to tools/web/exports/last_fetch.html ({len(resp.text)} bytes)")
