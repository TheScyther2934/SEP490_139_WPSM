// EVENT-TERMS (docs/report/EVENT-TERMS.md): a policy saved by the Admin applies to contracts signed from its effective date only.
// Signed events keep their terms (lock date, decrease floor, supplier deadline, settlement due date).
// Runs against the React build served by an API with the development seed (manager@test.local / admin@test.local, password in TEST_PW),
// on a throwaway PostgreSQL database: it changes the policy in force.
const puppeteer = require("puppeteer-core");
const BASE = process.env.REACT_BASE || "http://localhost:5300";
const PW = process.env.TEST_PW;
let bad = 0;
const check = (name, ok, detail = "") => { if (!ok) bad++; console.log(`${ok ? "PASS" : "FAIL"} ${name} ${ok ? "" : detail}`); };

async function api(token, p, method = "GET", body) {
  const r = await fetch(`${BASE}/api/wpsm/${p}`, {
    method,
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body ? { "Content-Type": "application/json" } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await r.text();
  let json = null;
  try { json = JSON.parse(text); } catch { /* empty body */ }
  return { status: r.status, body: json, text };
}
const token = async (email) => (await api(null, "auth/token", "POST", { email, password: PW })).body?.accessToken;
const waitText = (page, text, timeout = 10000) =>
  page.waitForFunction((t) => document.body.innerText.includes(t), { timeout }, text).then(() => true).catch(() => false);
const click = async (page, xpath) => (await page.waitForSelector(`::-p-xpath(${xpath})`, { visible: true, timeout: 10000 })).click();
const field = (page, name) => page.$eval(`[data-field="${name}"]`, (e) => e.textContent.trim()).catch(() => null);
const isoDay = (days) => { const d = new Date(Date.now() + 7 * 3600e3 + days * 864e5); return d.toISOString().slice(0, 10); };

async function signIn(browser, email, errors) {
  const ctx = await browser.createBrowserContext();
  const page = await ctx.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto(`${BASE}/login`, { waitUntil: "networkidle2" });
  await page.type("#email", email);
  await page.type("#password", PW);
  await click(page, '//button[normalize-space(.)="Đăng nhập"]');
  await page.waitForFunction(() => location.pathname !== "/login", { timeout: 10000 }).catch(() => {});
  return { ctx, page };
}

// A contract body that passes every check before the policy stamp and after it (free hall slot, services within their frames).
async function signing(manager, stamp, daysAway) {
  const date = isoDay(daysAway);
  const halls = (await api(manager, `halls?date=${date}`)).body ?? [];
  const hall = halls.find((h) => h.slots.some((s) => s.available));
  const slot = hall?.slots.find((s) => s.available);
  const catalog = ((await api(manager, "catalog")).body ?? []).filter((c) => c.active);
  const frames = (await api(manager, "price-frames")).body ?? [];
  const price = (c) => { const f = frames.find((x) => x.catalogItemId === Number(c.id)); return f ? f.minPrice : c.basePrice; };
  const items = catalog.slice(0, 2).map((c) => ({ catalogItemId: Number(c.id), description: "", quantity: 1, unitPrice: price(c) }));
  return {
    coupleName: "Kiểm thử Điều khoản", customerEmail: `terms.${Date.now()}@test.local`, customerPhone: "0912345678",
    hall: hall?.name, slot: slot?.slotName, eventDate: date, baseValue: 0, depositAmount: 0, paymentMethod: "Cash", items, policyStamp: stamp,
  };
}

(async () => {
  const manager = await token("manager@test.local");
  const admin = await token("admin@test.local");
  check("manager and admin sign in", !!manager && !!admin);
  const browser = await puppeteer.launch({ executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe", headless: true, defaultViewport: { width: 1400, height: 1000 } });
  const errors = [];
  try {
    const before = (await api(manager, "policies")).body;
    check("policy in force has a stamp", !!before?.stamp, JSON.stringify(before));
    const projects = (await api(manager, "projects")).body ?? [];
    const running = projects.find((p) => p.status === "Adjusting" && p.terms);
    check("a seeded adjusting event carries its signed terms", !!running, JSON.stringify(projects.map((p) => [p.id, p.status, !!p.terms])));
    const code = running?.id;
    const newLock = before.lockDays + 2;

    // 1. Admin saves a new lock policy through the policy screen.
    {
      const { ctx, page } = await signIn(browser, "admin@test.local", errors);
      await page.goto(`${BASE}/admin/policies`, { waitUntil: "networkidle2" });
      check("1 UI: the policy screen says changes apply to new contracts only", await waitText(page, "chỉ áp dụng cho hợp đồng ký từ ngày hiệu lực"));
      await click(page, '//button[normalize-space(.)="Chỉnh sửa chính sách"]');
      await page.waitForSelector("#lockDays", { visible: true });
      await new Promise((r) => setTimeout(r, 300));
      await page.click("#lockDays", { clickCount: 3 });
      await page.type("#lockDays", String(newLock));
      await click(page, '//div[@role="dialog"]//button[normalize-space(.)="Lưu"]');
      check("1 UI: the policy is saved", await waitText(page, "Lưu thành công."));
      await page.waitForSelector('[data-field="events-on-older-terms"]', { timeout: 10000 }).catch(() => null);
      const older = Number(await field(page, "events-on-older-terms"));
      check("1 UI: running events on older terms are counted", older >= 1, String(older));
      await ctx.close();
    }
    const after = (await api(manager, "policies")).body;
    check("1 API: the new lock days are in force with a new stamp", after.lockDays === newLock && after.stamp !== before.stamp, JSON.stringify(after));

    // 2. The signed event keeps its lock date and terms.
    const same = (await api(manager, `projects/${code}`)).body;
    check("2 API: the lock date of the signed event did not move", same?.lockDate === running.lockDate, `${running.lockDate} -> ${same?.lockDate}`);
    check("2 API: its terms keep the signed lock days", same?.terms?.lockDays === before.lockDays && same?.terms?.differsFromCurrent === true, JSON.stringify(same?.terms));
    {
      const { ctx, page } = await signIn(browser, "manager@test.local", errors);
      await page.goto(`${BASE}/manager/projects/${encodeURIComponent(code)}`, { waitUntil: "networkidle2" });
      await page.waitForSelector("#eventTerms", { timeout: 10000 }).catch(() => null);
      check("2 UI: the manager sees the signed lock days", (await field(page, "terms-lock")) === `${before.lockDays} ngày`, await field(page, "terms-lock"));
      check("2 UI: the manager is told the policy changed for new contracts", !!(await page.$('[data-field="terms-older"]')));
      await ctx.close();
    }
    {
      const { ctx, page } = await signIn(browser, "admin@test.local", errors);
      await page.goto(`${BASE}/admin/projects/${encodeURIComponent(code)}`, { waitUntil: "networkidle2" });
      await page.waitForSelector("#eventTerms", { timeout: 10000 }).catch(() => null);
      check("2 UI: the admin event detail shows the signed terms", (await field(page, "terms-lock")) === `${before.lockDays} ngày`, await field(page, "terms-lock"));
      await ctx.close();
    }

    // 3. A form opened before the change cannot sign with the old terms; with the new stamp it signs under the new policy.
    const stale = await api(manager, "projects", "POST", await signing(manager, before.stamp, 40));
    check("3 API: signing with the stamp of the old policy is refused", stale.status === 400 && stale.text.includes("vừa được thay đổi"), `${stale.status} ${stale.text.slice(0, 200)}`);
    const fresh = await api(manager, "projects", "POST", await signing(manager, after.stamp, 41));
    const created = fresh.body?.project;
    check("3 API: signing with the current stamp works", fresh.status === 201 && !!created, `${fresh.status} ${fresh.text.slice(0, 300)}`);
    check("3 API: the new event uses the new lock days", created?.terms?.lockDays === newLock && created?.terms?.differsFromCurrent === false, JSON.stringify(created?.terms));
    check("3 API: its lock date follows the new policy", created?.lockDate === isoDay(41 - newLock), `${created?.lockDate} vs ${isoDay(41 - newLock)}`);

    // 4. Only the Admin changes the policy.
    const denied = await api(manager, "policies", "PUT", { ...after, lockDays: before.lockDays });
    check("4 API: a manager cannot change the policy", denied.status === 403, String(denied.status));
  } finally {
    await browser.close();
  }
  check("no page errors", errors.length === 0, errors.join(" | "));
  console.log(bad ? `${bad} FAILED` : "ALL PASSED");
  process.exit(bad ? 1 : 0);
})();
