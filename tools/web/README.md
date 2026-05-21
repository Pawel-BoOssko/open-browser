<!-- LEGACY_DOCUMENT_NOTICE -->
> Legacy document notice: this is an older documentation file. Its current operational content has been migrated or superseded by: docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references.
>
> Before using this file as source of truth, read: docs/agent/READ_BEFORE_WORK_new.md.
>
> Treat this file as historical or secondary unless the new documentation explicitly points back to it as an active operational reference.
<!-- /LEGACY_DOCUMENT_NOTICE -->

# tools/web/

**Purpose:** Web automation tools for interacting with websites that don't have APIs — government portals, forms, data extraction, content fetching.

**Created by:** Claude Code, 2026-05-19.

**Rules:**
1. Three levels of automation: stateless HTTP (`web_fetch`, `web_form`), stateful browser (`web_browser`)
2. For sites with simple forms: use `web_form.py`
3. For JavaScript-heavy sites or login flows: use `web_browser.py` (Selenium)
4. Screenshots and HTML exports go to `tools/web/exports/`
5. Never commit credentials or cookies from `exports/`

**Scripts:**
| Script | Approach | Use for |
|--------|----------|---------|
| `web_fetch.py` | HTTP requests + BeautifulSoup | Reading pages, extracting data by CSS selector |
| `web_form.py` | HTTP requests with session | Submitting simple forms, basic auth |
| `web_browser.py` | Selenium WebDriver | Full browser automation — login, clicks, JavaScript, screenshots |

**Prerequisites:**
- `pip install requests beautifulsoup4` (for web_fetch, web_form)
- `pip install selenium` (for web_browser)
- Chrome or Chromium browser installed (for web_browser)
