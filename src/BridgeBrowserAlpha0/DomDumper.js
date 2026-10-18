(() => {
  const report = {
    url: window.location.href,
    title: document.title,
    timestamp: new Date().toISOString(),
    links: [],
    buttons: [],
    inputs: [],
    forms: [],
    tables: [],
    headings: [],
    modals: [],
    frames: []
  };

  document.querySelectorAll('a[href]').forEach(a => {
    report.links.push({
      text: (a.textContent || '').trim().slice(0, 200),
      href: a.href || a.getAttribute('href'),
      class: a.className,
      id: a.id,
      download: a.getAttribute('download'),
      target: a.target,
      ariaLabel: a.getAttribute('aria-label')
    });
  });

  document.querySelectorAll('button, [role="button"]').forEach(btn => {
    report.buttons.push({
      text: (btn.textContent || '').trim().slice(0, 200),
      class: btn.className,
      id: btn.id,
      type: btn.getAttribute('type'),
      ariaLabel: btn.getAttribute('aria-label'),
      disabled: btn.disabled,
      tag: btn.tagName
    });
  });

  document.querySelectorAll('input, select, textarea').forEach(el => {
    report.inputs.push({
      tag: el.tagName,
      type: el.getAttribute('type'),
      name: el.getAttribute('name'),
      placeholder: el.getAttribute('placeholder'),
      class: el.className,
      id: el.id
    });
  });

  document.querySelectorAll('form').forEach(form => {
    report.forms.push({
      action: form.action,
      method: form.method,
      class: form.className,
      id: form.id
    });
  });

  document.querySelectorAll('table').forEach(table => {
    var headers = Array.from(table.querySelectorAll('th')).map(function(th) { return th.textContent.trim().slice(0, 100); });
    report.tables.push({ headers: headers, rows: table.querySelectorAll('tr').length, class: table.className, id: table.id });
  });

  document.querySelectorAll('h1, h2, h3, h4').forEach(h => {
    report.headings.push({ tag: h.tagName, text: (h.textContent || '').trim().slice(0, 200) });
  });

  document.querySelectorAll('[role="dialog"], .modal, .popup, [class*="modal"], [class*="dialog"], [class*="popup"]').forEach(m => {
    report.modals.push({ class: m.className, id: m.id, visible: m.offsetParent !== null, text: (m.textContent || '').trim().slice(0, 300) });
  });

  document.querySelectorAll('iframe').forEach(f => {
    report.frames.push({ src: f.src, class: f.className, id: f.id });
  });

  return JSON.stringify(report);
})();
