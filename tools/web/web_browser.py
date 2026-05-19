"""Browser automation using Selenium WebDriver for sites without APIs.

Usage: python web_browser.py <action> [args...]

Actions:
  navigate <url>              — open a URL
  fill <selector> <value>     — type text into a field
  click <selector>            — click an element
  read <selector>             — extract text from elements
  screenshot                  — save a screenshot
  wait <seconds>              — pause execution

Example session:
  python web_browser.py navigate https://example.com/login
  python web_browser.py fill "#username" myuser
  python web_browser.py fill "#password" mypass
  python web_browser.py click "button[type=submit]"
  python web_browser.py read "div.result"

State persists between calls via a local session file.

Prerequisite: pip install selenium
"""
import json
import sys
import time
from pathlib import Path

try:
    from selenium import webdriver
    from selenium.webdriver.common.by import By
    from selenium.webdriver.chrome.options import Options
    from selenium.webdriver.chrome.service import Service
except ImportError:
    print("MISSING_DEPENDENCIES — run: pip install selenium")
    raise SystemExit(1)

SESSION_FILE = Path("tools/web/exports/session.json")
SCREENSHOT_DIR = Path("tools/web/exports/screenshots")
SCREENSHOT_DIR.mkdir(parents=True, exist_ok=True)

action = sys.argv[1] if len(sys.argv) > 1 else "help"


def get_driver():
    if SESSION_FILE.exists():
        session = json.loads(SESSION_FILE.read_text(encoding="utf-8"))
        options = Options()
        options.debugger_address = session["debugger_address"]
        return webdriver.Chrome(options=options)

    options = Options()
    options.add_argument("--remote-debugging-port=9222")
    options.add_argument("--no-first-run")
    options.add_argument("--user-data-dir=./tools/web/exports/chrome-profile")
    driver = webdriver.Chrome(options=options)

    SESSION_FILE.write_text(
        json.dumps({"debugger_address": "127.0.0.1:9222", "started": time.time()}),
        encoding="utf-8",
    )
    return driver


def save_screenshot(driver):
    ts = time.strftime("%Y%m%d_%H%M%S")
    path = SCREENSHOT_DIR / f"screen_{ts}.png"
    driver.save_screenshot(str(path))
    print(f"SCREENSHOT_SAVED {path}")


if action == "help":
    print(__doc__)
    raise SystemExit(0)

driver = get_driver()

if action == "navigate":
    url = sys.argv[2]
    driver.get(url)
    print(f"NAVIGATED {url}")
    print(f"Title: {driver.title}")

elif action == "fill":
    selector = sys.argv[2]
    value = " ".join(sys.argv[3:])
    el = driver.find_element(By.CSS_SELECTOR, selector)
    el.clear()
    el.send_keys(value)
    print(f"FILLED {selector}")

elif action == "click":
    selector = sys.argv[2]
    el = driver.find_element(By.CSS_SELECTOR, selector)
    el.click()
    print(f"CLICKED {selector}")

elif action == "read":
    selector = sys.argv[2]
    els = driver.find_elements(By.CSS_SELECTOR, selector)
    for el in els[:5]:
        text = el.text.strip()
        if text:
            print(text[:2000])
            print("---")
    if not els:
        print(f"NO_ELEMENTS for {selector}")

elif action == "screenshot":
    save_screenshot(driver)

elif action == "wait":
    seconds = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    time.sleep(seconds)
    print(f"WAITED {seconds}s")
