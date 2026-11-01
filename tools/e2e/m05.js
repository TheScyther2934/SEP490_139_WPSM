const puppeteer = require("puppeteer-core");
const BASE = "http://localhost:5301";
const { execSync } = require("child_process");
const sql = q => execSync(`sqlcmd -S "(localdb)\\MSSQLLocalDB" -d WPSM_E2E_Test -h -1 -W -Q "set nocount on; ${q}"`).toString().trim();
const results = [];
const check = (name, ok, extra = "") => { results.push(ok); console.log(`${ok ? "PASS" : "FAIL"} ${name} ${extra}`); };

(async () => {
  const browser = await puppeteer.launch({ headless: "new", executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 1000 });
  const errors = [];
  page.on("pageerror", e => errors.push(e.message));

  await page.goto(`${BASE}/G01`, { waitUntil: "domcontentloaded" });
  const api = (path, method, body) => page.evaluate(async (path, method, body) => {
    const r = await fetch(`/api/wpsm/${path}`, { method, headers: { "Content-Type": "application/json" }, body: body ? JSON.stringify(body) : undefined });
    return { status: r.status, body: await r.json().catch(() => null) };
  }, path, method, body);
  const login = await api("auth/login", "POST", { email: "manager@test.local", password: process.env.TEST_PW });
  check("login", login.status === 200, String(login.status));
  const created = await api("projects", "POST", { coupleName: "E2E <A> & B", customerEmail: `e2e${Date.now()}@example.com`, customerPhone: "0900000001", hall: "Sảnh Hoàng Gia", slot: "Evening", eventDate: process.env.EVENT_DATE, baseValue: 100000000, nrdAmount: 30000000, paymentMethod: "Cash" });
  check("create project", created.status === 201, JSON.stringify(created.body?.message || ""));
  const id = created.body.project.id;

  const open = async () => {
    await page.goto(`${BASE}/M05?id=${id}`, { waitUntil: "domcontentloaded" });
    await page.waitForFunction(() => window.wpsm?.project);
  };
  const text = sel => page.$eval(sel, el => el.textContent.trim());
  const field = name => text(`[data-field="${name}"]`);
  const rows = () => page.$$eval("tr[data-spec-item-id]", r => r.length);
  const visibleRows = () => page.$$eval("tr[data-spec-item-id]", r => r.filter(x => !x.classList.contains("hidden")).length);
  const settle = () => new Promise(r => setTimeout(r, 400));
  const addByName = async name => {
    await page.$eval("#btnOpenCatalog", b => b.click());
    await page.$$eval("#catalogPickerList button", (bs, name) => bs.find(b => b.dataset.catalogName === name).click(), name);
    await settle();
  };
  const setInput = async (itemName, attr, value) => {
    await page.evaluate((itemName, attr, value) => {
      const input = document.querySelector(`tr[data-spec-name="${itemName}"] [${attr}]`);
      input.value = value; input.dispatchEvent(new Event("input", { bubbles: true }));
    }, itemName, attr, value);
  };

  await open();
  check("real couple name, not mock", (await field("coupleName")) === "E2E <A> & B", await field("coupleName"));
  while (await rows()) { await page.$eval('[data-action="remove-spec-item"]', b => b.click()); await settle(); }
  check("no spec rows", (await rows()) === 0);
  check("combo 0/5", (await field("comboCount")).startsWith("0/5"));
  check("no double escape in combo note", (await text('[data-combo-category="Âm thanh & ánh sáng"] [data-combo-note]')).includes("Âm thanh & ánh sáng"));
  check("NRD bound", (await field("nrdAmount")).startsWith("30.000.000") && (await field("nrdPercent")) === "30%", await field("nrdAmount"));
  check("M05 nav link carries id", await page.$$eval('a[href*="/M05"]', (l, id) => l.every(a => a.href.includes(id)), id));

  await page.$eval("#btnOpenCatalog", b => b.click());
  await page.type("#catalogSearch", "mc");
  await settle();
  check("catalog search filters", (await page.$$eval("#catalogPickerList button", b => b.length)) === 1);
  await page.$eval("#catalogSearch", e => { e.value = ""; e.dispatchEvent(new Event("input", { bubbles: true })); });
  await page.select("#catalogCategoryFilter", "Trang trí");
  await settle();
  check("catalog category filter", (await page.$$eval("#catalogPickerList button", b => b.length)) === 1);
  await page.select("#catalogCategoryFilter", "");
  await settle();
  await page.$eval("#catalogModal", m => m.classList.add("hidden"));

  for (const n of ["Thuê sảnh tiệc", "Thực đơn tiệc cưới", "Trang trí sân khấu", "MC chương trình"]) await addByName(n);
  check("4 rows", (await rows()) === 4);
  check("combo 4/5", (await field("comboCount")).startsWith("4/5"));

  await setInput("Thực đơn tiệc cưới", "data-spec-quantity", "10");
  check("live line total", (await text('tr[data-spec-name="Thực đơn tiệc cưới"] [data-spec-line-total]')).startsWith("35.000.000"));
  await addByName("Âm thanh và ánh sáng");
  check("unsaved quantity survives add", (await page.$eval('tr[data-spec-name="Thực đơn tiệc cưới"] [data-spec-quantity]', e => e.value)) === "10");
  check("combo 5/5", (await field("comboCount")).startsWith("5/5"));

  await setInput("MC chương trình", "data-spec-price", "9.000.000");
  await page.click("#btnSaveDraft");
  await settle();
  check("MSG06 shown on draft", (await text("#wpsm-api-status")).includes("MSG06"), await text("#wpsm-api-status"));
  await setInput("MC chương trình", "data-spec-price", "4.000.000");
  await page.click("#btnSaveDraft");
  await settle();
  check("draft saved", (await field("lastSaved")).startsWith("Đã lưu"), await field("lastSaved"));

  await page.evaluate(() => document.querySelector('[data-action="filter-spec-category"][data-category="MC"]').click());
  await settle();
  check("sidebar filter", (await visibleRows()) === 1);
  await page.evaluate(() => document.querySelector('[data-action="filter-spec-category"][data-category=""]').click());
  await page.type("#specSearch", "thực đơn");
  await settle();
  check("sidebar search", (await visibleRows()) === 1);
  await page.$eval("#specSearch", e => { e.value = ""; e.dispatchEvent(new Event("input", { bubbles: true })); });

  check("payment tiers from policy", (await field("tierT30")) === "40%" && (await field("tierFinal")) === "30%", (await field("tierT30")) + "/" + (await field("tierFinal")));
  const longDesc = await api(`projects/${id}/specification`, "PUT", { summary: "", guestCount: 0, items: [{ catalogItemId: 1, description: "x".repeat(501), quantity: 1, unitPrice: 25000000 }] });
  check("description 501 rejected", longDesc.status === 400, longDesc.body?.message);
  sql(`update WeddingProjects set ManagerId=1 where ProjectCode='${id}'`);
  const other1 = await api(`projects/${id}/specification`, "PUT", { summary: "", guestCount: 0, items: [] });
  const other2 = await api(`projects/${id}/freeze`, "POST", {});
  check("other manager blocked GBR-12", other1.status === 404 && other2.status === 404, `${other1.status}/${other2.status}`);
  sql(`update WeddingProjects set ManagerId=2 where ProjectCode='${id}'`);
  await page.$eval('tr[data-spec-name="MC chương trình"] [data-action="remove-spec-item"]', b => b.click());
  await settle();
  check("remove", (await rows()) === 4);
  await page.click("#btnOpenFreeze");
  await page.click("#freezeConsent");
  await page.click("#confirmFreezeBtn");
  await settle();
  check("freeze blocked without combo (Vietnamese label)", (await text("#wpsm-api-status")).includes("MSG12"), await text("#wpsm-api-status"));
  await addByName("MC chương trình");

  await setInput("Thực đơn tiệc cưới", "data-spec-quantity", "12");
  await page.click("#btnOpenFreeze");
  check("consent reset", await page.$eval("#confirmFreezeBtn", b => b.disabled));
  await page.click("#freezeConsent");
  await page.click("#confirmFreezeBtn");
  await settle(); await settle();
  check("frozen status", (await page.evaluate(() => window.wpsm.project.status)) === "Baseline Frozen", await text("#wpsm-api-status"));
  check("unsaved edit included in freeze", (await page.evaluate(() => window.wpsm.project.specification.find(i => i.category === "Ẩm thực").quantity)) === 12);

  await open();
  check("banner after reload", !(await page.$eval("#freezeBanner", e => e.classList.contains("hidden"))));
  check("inputs locked", await page.$$eval("#specTableBody input", i => i.length > 0 && i.every(x => x.disabled)));
  check("buttons locked", await page.$$eval("#btnOpenCatalog,#btnSaveDraft,#btnOpenFreeze", b => b.every(x => x.disabled)));
  check("filter usable when frozen", !(await page.$eval("#specSearch", e => e.disabled)));
  const blocked = await api(`projects/${id}/specification`, "PUT", { summary: "", guestCount: 0, items: [] });
  check("API rejects edit after freeze", blocked.status >= 400, String(blocked.status));
  check("M-05 table not cropped at 1440px", await page.$eval("#specTableBody", b => { const w = b.closest(".overflow-x-auto"); return w.scrollWidth <= w.clientWidth + 1; }), await page.$eval("#specTableBody", b => { const w = b.closest(".overflow-x-auto"); return w.scrollWidth + "/" + w.clientWidth; }));
  await page.screenshot({ path: process.env.SHOT, fullPage: false });
  await Promise.all([page.waitForNavigation({ waitUntil: "domcontentloaded" }), page.click("#btnBackToProject")]);
  check("back goes to project", page.url().includes(`/M04?id=${id}`), page.url());
  await page.waitForFunction(() => window.wpsm?.project); await settle();
  check("hub links to M05 with id", await page.$$eval('a[href*="/M05"]', (l, id) => l.length > 1 && l.every(a => a.href.includes(id)), id));
  check("roadmap 11 SDS tasks", sql(`select count(*) from RoadmapTasks r join WeddingProjects w on w.ProjectId=r.ProjectId where w.ProjectCode='${id}'`) === "11");
  check("customer notified", sql(`select count(*) from Notifications n join WeddingProjects w on w.CustomerId=n.RecipientId where w.ProjectCode='${id}'`) === "1");
  check("no page errors", errors.length === 0, errors.join(" | "));

  await browser.close();
  console.log(`${results.filter(Boolean).length}/${results.length} passed`);
  process.exit(results.every(Boolean) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
