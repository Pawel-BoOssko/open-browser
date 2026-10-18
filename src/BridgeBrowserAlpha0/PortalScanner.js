(async () => {
  if (window.__portalScannerRunning) return;
  window.__portalScannerRunning = true;

  let processed = 0;

  async function delay(ms) { return new Promise(r => setTimeout(r, ms)); }

  function log(msg) {
    console.log('[PortalScanner] ' + msg);
    try {
      chrome.webview.postMessage({
        source: 'portal_scanner',
        eventType: 'portal_scan_progress',
        status: 'ok',
        message: msg
      });
    } catch (_) {}
  }

  function getExpandButtons() {
    return Array.from(document.querySelectorAll('button[aria-label="Rozwiń"]'))
      .filter(b => b.id && b.id.startsWith('expand'));
  }

  function getDownloadButton(row) {
    // After expansion, menu items appear in the row
    const items = Array.from(row.querySelectorAll('.mat-mdc-menu-item, [role="menuitem"]'));
    return items.find(b => {
      const text = (b.textContent || '').trim();
      return text === 'Pobierz w formacie źródłowym';
    });
  }

  const expandBtns = getExpandButtons();
  log('Found ' + expandBtns.length + ' documents on this page');

  for (const btn of expandBtns) {
    const row = btn.closest('tr, [role="row"]');
    if (!row) continue;

    // Expand
    btn.click();
    await delay(1500);

    // Click download
    const dlBtn = getDownloadButton(row);
    if (dlBtn) {
      dlBtn.click();
      processed++;
      log('Download triggered: ' + processed + '/' + expandBtns.length);
      await delay(2500);
    } else {
      log('No download button in row after expand');
    }
  }

  log('Page done: ' + processed + ' downloads triggered');
  window.__portalScannerRunning = false;
})();
