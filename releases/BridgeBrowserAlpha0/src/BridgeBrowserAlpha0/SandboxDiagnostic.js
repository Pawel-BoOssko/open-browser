// Collects sandbox click event data into window.__sandboxDiag
// Data read back by C# via ExecuteScriptAsync("JSON.stringify(window.__sandboxDiag)")
(function() {
  var log = [];
  window.__sandboxDiag = log;

  var links = document.querySelectorAll('a[href*="sandbox:"]');
  log.push({step: 'init', links: links.length});

  if (links.length === 0) return;

  var target = links[0];
  log.push({step: 'target', href: target.href, tag: target.tagName,
    parentTag: target.parentElement.tagName,
    parentClass: target.parentElement.className.substring(0, 80)});

  ['click','mousedown','mouseup','pointerdown','pointerup'].forEach(function(evName) {
    target.addEventListener(evName, function(e) {
      log.push({step: 'event', name: evName, isTrusted: e.isTrusted,
        defaultPrevented: e.defaultPrevented, button: e.button});
    }, true);
  });

  var origFetch = window.fetch;
  window.fetch = function() {
    var url = arguments[0];
    if (typeof url === 'string' && (url.indexOf('sandbox')>=0 || url.indexOf('estuary')>=0 || url.indexOf('mnt/data')>=0)) {
      log.push({step: 'fetch', url: url.substring(0, 200)});
    }
    return origFetch.apply(this, arguments);
  };

  var origXhrOpen = XMLHttpRequest.prototype.open;
  XMLHttpRequest.prototype.open = function(method, url) {
    if (url && (url.indexOf('sandbox')>=0 || url.indexOf('estuary')>=0 || url.indexOf('mnt/data')>=0)) {
      log.push({step: 'xhr', method: method, url: url.substring(0, 200)});
    }
    return origXhrOpen.apply(this, arguments);
  };

  log.push({step: 'ready'});
})();
