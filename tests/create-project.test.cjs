const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');

// Execute the browser script against a small DOM and HTTP stub to exercise
// the real event handlers without a database or third-party dependencies.
function setup() {
  const events = {};
  const requests = [];
  const elements = {};
  const element = (value = '') => ({
    value, dataset: {}, classList: { add() {}, remove() {}, toggle() {} },
    reportValidity() { return !!this.value && !this.disabled; },
    addEventListener(type, callback) { this[type] = callback; }
  });
  for (const [id, value] of Object.entries({
    'wpsm-api-status': '', coupleName: 'Tên khách không có dấu nối',
    customerEmail: 'test@example.com', customerPhone: '0912345678',
    hallSelect: '', eventDate: '2027-04-18', baseValue: '500.000.000',
    nrdAmount: '150000000', shiftOptions: '', nrdPaymentMethod: 'Tiền mặt'
  })) elements[id] = element(value);
  let selected = { value: 'Evening' };
  const document = {
    getElementById: id => elements[id] || null,
    addEventListener: (type, callback) => { events[type] = callback; },
    querySelectorAll: () => [],
    querySelector(selector) {
      if (selector.includes('shift_selection')) return selected;
      if (selector === '[data-wpsm-screen]') return { dataset: { wpsmScreen: 'M03' } };
      if (selector.includes('#hallSelect')) return elements.hallSelect;
      return null;
    }
  };
  const halls = [{ name: 'Sảnh Kiểm Thử', minCapacity: 100, maxCapacity: 500, basePrice: 100,
    slots: [
      { slotName: 'Lunch', startTime: '11:00:00', endTime: '14:00:00', available: false },
      { slotName: 'Evening', startTime: '17:30:00', endTime: '21:30:00', available: true }
    ] }];
  const window = { location: { search: '' }, setTimeout() {}, clearTimeout() {} };
  const fetch = async (url, options = {}) => {
    requests.push({ url, ...options });
    const result = url.includes('/halls') ? halls : url.endsWith('/projects')
      ? { project: { id: 'WPSM-TEST' }, temporaryPassword: 'test-only' }
      : { projects: [], project: null, policies: { nrdRatio: 30 } };
    return { ok: true, status: 200, json: async () => result };
  };
  vm.runInNewContext(fs.readFileSync('WPSM.Web/wwwroot/js/wpsm-api.js', 'utf8'), {
    document, window, fetch, URLSearchParams, Intl, console
  });
  const button = { innerText: 'Xác nhận & tạo dự án', dataset: {}, disabled: false };
  const click = () => events.click({ target: { closest: () => button }, preventDefault() {}, stopImmediatePropagation() {} });
  return { elements, requests, click, button, select: value => { selected = value; } };
}

const settle = () => new Promise(resolve => setImmediate(resolve));

test('loads availability for the selected date and renders available/occupied shifts', async () => {
  const app = setup();
  await settle();
  assert.ok(app.requests.some(r => r.url.endsWith('/halls?date=2027-04-18')));
  app.elements.hallSelect.value = 'Sảnh Kiểm Thử';
  app.elements.hallSelect.change();
  assert.match(app.elements.shiftOptions.innerHTML, /value="Lunch" disabled/);
  assert.match(app.elements.shiftOptions.innerHTML, /value="Evening" required/);
  app.elements.eventDate.value = '2027-05-20';
  await app.elements.eventDate.change();
  assert.ok(app.requests.some(r => r.url.endsWith('/halls?date=2027-05-20')));
});

test('posts exact form values, accepts plain numbers, and prevents duplicate creation', async () => {
  const app = setup();
  await settle();
  app.elements.hallSelect.value = 'Sảnh Kiểm Thử';
  await app.click();
  const posts = app.requests.filter(r => r.method === 'POST');
  assert.equal(posts.length, 1);
  assert.deepEqual(JSON.parse(posts[0].body), {
    coupleName: 'Tên khách không có dấu nối', customerEmail: 'test@example.com',
    customerPhone: '0912345678', hall: 'Sảnh Kiểm Thử', slot: 'Evening',
    eventDate: '2027-04-18', baseValue: 500000000, nrdAmount: 150000000, paymentMethod: 'Cash'
  });
  await app.click();
  assert.equal(app.requests.filter(r => r.method === 'POST').length, 1);
});

test('rejects creation without an available shift or required fields', async () => {
  const app = setup();
  await settle();
  app.elements.hallSelect.value = 'Sảnh Kiểm Thử';
  app.select(null);
  await app.click();
  assert.equal(app.requests.filter(r => r.method === 'POST').length, 0);
  app.select({ value: 'Evening' });
  app.elements.customerEmail.value = '';
  await app.click();
  assert.equal(app.requests.filter(r => r.method === 'POST').length, 0);
});
