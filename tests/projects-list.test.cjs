const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

test('project pagination and filters use the assigned database snapshot', async () => {
  const element = () => {
    const classes = new Set();
    return { value: '', textContent: '', innerHTML: '', disabled: false,
      classList: { add: x => classes.add(x), remove: x => classes.delete(x), contains: x => classes.has(x) },
      addEventListener(type, callback) { this[type] = callback; } };
  };
  const ids = Object.fromEntries(['wpsm-api-status', 'search-input', 'status-filter', 'hall-filter', 'month-filter', 'reset-btn', 'clear-search-btn', 'projects-pagination-info', 'projects-nav-badge', 'projects-scope-pill', 'empty-state'].map(id => [id, element()]));
  const tbody = element();
  const table = element(); table.querySelector = () => tbody; ids['projects-table'] = table;
  const prev = element(), next = element(), label = element();
  const root = { dataset: { wpsmScreen: 'M02' } };
  const document = {
    getElementById: id => ids[id] || null,
    addEventListener() {}, querySelectorAll: () => [],
    querySelector: selector => ({ '[data-wpsm-screen="M02"]': root, '[data-wpsm-screen]': root, '[data-page-prev]': prev, '[data-page-next]': next, '[data-page-label]': label })[selector] || null
  };
  const projects = Array.from({ length: 12 }, (_, i) => ({
    id: `WPSM-${i}`, coupleName: i === 0 ? '<script>alert(1)</script>' : `Couple ${i}`,
    customerEmail: `customer${i}@test.local`, hall: i % 2 ? 'Hall B' : 'Hall A',
    eventDate: i < 6 ? '2035-04-18' : '2035-05-20', slot: 'Evening', status: 'Active Baseline'
  }));
  const fetch = async url => ({ ok: true, json: async () => url.endsWith('/halls') ? [] : { projects, project: null } });
  vm.runInNewContext(fs.readFileSync('WPSM.Web/wwwroot/js/wpsm-api.js', 'utf8'), {
    document, window: { location: { search: '' }, setTimeout() {} }, fetch, URLSearchParams, Intl
  });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal((tbody.innerHTML.match(/<tr /g) || []).length, 5);
  assert.equal(label.textContent, '1 / 3');
  assert.equal(prev.disabled, true);
  assert.equal(next.disabled, false);
  assert.match(tbody.innerHTML, /&lt;script&gt;/);
  assert.match(ids['hall-filter'].innerHTML, /Hall B/);
  assert.match(ids['month-filter'].innerHTML, /2035-05/);
  next.click(); next.click();
  assert.equal(label.textContent, '3 / 3');
  assert.equal((tbody.innerHTML.match(/<tr /g) || []).length, 2);
  assert.equal(next.disabled, true);
  ids['search-input'].value = 'customer11@test.local'; ids['search-input'].input();
  assert.equal(label.textContent, '1 / 1');
  assert.match(tbody.innerHTML, /WPSM-11/);
  assert.equal((tbody.innerHTML.match(/<tr /g) || []).length, 1);
  ids['search-input'].value = 'not-found'; ids['search-input'].input();
  assert.equal(table.classList.contains('hidden'), true);
  assert.equal(ids['empty-state'].classList.contains('hidden'), false);
  assert.match(ids['projects-pagination-info'].textContent, /0–0 trên 0/);
  ids['reset-btn'].click();
  assert.equal(label.textContent, '1 / 3');
  assert.equal(ids['projects-nav-badge'].textContent, 12);
});
