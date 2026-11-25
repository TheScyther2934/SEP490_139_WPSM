// Bugs reported by testers on 22–24/11/2026 (Discord #sep-code, docs/report/BUG-DISCORD-1125.md).
// Runs against the React build served by an API with the development seed (manager@test.local / admin@test.local, password in TEST_PW),
// on a throwaway PostgreSQL database. Data is prepared through the API, the new buttons are clicked in Chrome.
const puppeteer = require("puppeteer-core");
const BASE = process.env.REACT_BASE || "http://localhost:5300";
const PW = process.env.TEST_PW;
const EVENT = process.env.LOCKED_EVENT || "WPSM-DEMO-02"; // seeded event in the Locked status
let bad = 0;
const check = (name, ok, detail = "") => { if (!ok) bad++; console.log(`${ok ? "PASS" : "FAIL"} ${name} ${ok ? "" : detail}`); };
const PNG = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==", "base64");

async function api(token, p, method = "GET", body, raw = false) {
  const isForm = body instanceof FormData;
  const r = await fetch(`${BASE}/api/wpsm/${p}`, {
    method,
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body && !isForm ? { "Content-Type": "application/json" } : {}) },
    body: body ? (isForm ? body : JSON.stringify(body)) : undefined,
  });
  if (raw) return { status: r.status, bytes: Buffer.from(await r.arrayBuffer()) };
  const text = await r.text();
  let json = null;
  try { json = JSON.parse(text); } catch { /* empty body */ }
  return { status: r.status, body: json };
}
const token = async (email) => (await api(null, "auth/token", "POST", { email, password: PW })).body?.accessToken;
const waitText = (page, text, timeout = 10000) =>
  page.waitForFunction((t) => document.body.innerText.includes(t), { timeout }, text).then(() => true).catch(() => false);
const click = async (page, xpath) => (await page.waitForSelector(`::-p-xpath(${xpath})`, { visible: true, timeout: 10000 })).click();
// MUI dialogs fade in: a click during the transition is lost, so wait until the dialog is fully shown.
const confirmInDialog = async (page, label) => {
  await page.waitForFunction(() => { const d = document.querySelector('[role="dialog"]'); return d && getComputedStyle(d.closest(".MuiDialog-container") ?? d).opacity === "1"; }, { timeout: 8000 });
  await new Promise((r) => setTimeout(r, 300));
  await click(page, `//div[@role="dialog"]//button[normalize-space(.)="${label}"]`);
};

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

(async () => {
  const manager = await token("manager@test.local");
  const admin = await token("admin@test.local");
  check("manager and admin sign in", !!manager && !!admin);
  const browser = await puppeteer.launch({ executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe", headless: true, defaultViewport: { width: 1400, height: 1000 } });
  const errors = [];
  try {
    // 1. Admin sets "needs an outside supplier" on a catalog service.
    const catalog = (await api(admin, "catalog")).body ?? [];
    const item = catalog.find((c) => c.active && !c.requiresSupplier) ?? catalog[0];
    const on = await api(admin, `catalog/${item.id}`, "PUT", { name: item.name, category: item.category, unit: item.unit, basePrice: item.basePrice, active: true, requiresSupplier: true });
    check("1 API: catalog edit saves requiresSupplier", on.status === 200 && on.body?.requiresSupplier === true, JSON.stringify(on.body));
    const kept = await api(admin, `catalog/${item.id}`, "PUT", { name: item.name, category: item.category, unit: item.unit, basePrice: item.basePrice, active: true });
    check("1 API: an edit without the field keeps it", kept.body?.requiresSupplier === true, JSON.stringify(kept.body));
    {
      const { ctx, page } = await signIn(browser, "admin@test.local", errors);
      await page.goto(`${BASE}/admin/catalog`, { waitUntil: "networkidle2" });
      check("1 UI: the catalog list marks services needing a supplier", await waitText(page, "Cần nhà cung cấp"));
      await click(page, `(//tr[@data-item])[1]//button[normalize-space(.)="Sửa"]`);
      check("1 UI: the edit dialog has the supplier checkbox", !!(await page.waitForSelector("#catalogRequiresSupplier", { timeout: 8000 }).catch(() => null)));
      await ctx.close();
    }
    await api(admin, `catalog/${item.id}`, "PUT", { name: item.name, category: item.category, unit: item.unit, basePrice: item.basePrice, active: true, requiresSupplier: item.requiresSupplier });

    // 2. Supplier evidence uploaded by mistake can be removed (locked event).
    const form = new FormData();
    form.append("supplierName", "Nhà cung cấp kiểm thử");
    form.append("evidenceType", "Contract");
    form.append("note", "tải nhầm");
    form.append("file", new Blob([PNG], { type: "image/png" }), "nham.png");
    const uploaded = await api(manager, `projects/${EVENT}/suppliers`, "POST", form);
    check("2 setup: evidence uploaded on the locked event", uploaded.status === 200 && !!uploaded.body?.id, JSON.stringify(uploaded.body));
    {
      const { ctx, page } = await signIn(browser, "manager@test.local", errors);
      await page.goto(`${BASE}/manager/projects/${EVENT}/suppliers`, { waitUntil: "networkidle2" });
      await waitText(page, "Nhà cung cấp kiểm thử");
      await click(page, '//button[@data-action="remove-evidence"]');
      await confirmInDialog(page, "Xóa");
      check("2 UI: delete removes the evidence from the list", await page.waitForFunction(() => !document.body.innerText.includes("Nhà cung cấp kiểm thử"), { timeout: 10000 }).then(() => true).catch(() => false));
      await ctx.close();
    }
    const board = await api(manager, `projects/${EVENT}/suppliers`);
    check("2 API: the evidence is gone from the board", !board.body?.evidences?.some((e) => e.id === uploaded.body?.id));
    check("2 API: deleting it again is 404", (await api(manager, `projects/${EVENT}/suppliers/${uploaded.body?.id}`, "DELETE")).status === 404);
    const settled = "WPSM-2025-011";
    const settledBoard = await api(manager, `projects/${settled}/suppliers`);
    check("2 API: a settled event does not offer delete (canUpload false)", settledBoard.body?.canUpload === false, JSON.stringify(settledBoard.body?.status));

    // 5. A coordination task completed by mistake is reopened.
    let tasks = (await api(manager, `projects/${EVENT}/day-of`)).body?.tasks ?? [];
    if (!tasks.length) tasks = (await api(manager, `projects/${EVENT}/day-of/load`, "POST")).body?.tasks ?? [];
    const target = tasks.find((t) => t.status !== "Done" && !t.photoRequired) ?? tasks.find((t) => t.status !== "Done");
    const doneForm = new FormData();
    if (target?.photoRequired) doneForm.append("photos", new Blob([PNG], { type: "image/png" }), "p.png");
    const done = await api(manager, `projects/${EVENT}/day-of/${target?.taskId}/complete`, "POST", doneForm);
    check("5 setup: task completed", done.status === 200, JSON.stringify(done.body));
    {
      const { ctx, page } = await signIn(browser, "manager@test.local", errors);
      await page.goto(`${BASE}/manager/projects/${EVENT}/day-of`, { waitUntil: "networkidle2" });
      await page.waitForSelector(`#task-${target.taskId}`, { timeout: 10000 }).catch(() => {});
      const onBoard = !!(await page.$(`#task-${target.taskId}`));
      if (!onBoard) { // preparation tasks live on the department sheets
        await page.goto(`${BASE}/manager/projects/${EVENT}/beo/department-sheets`, { waitUntil: "networkidle2" });
        await page.waitForSelector(`#task-${target.taskId}`, { timeout: 10000 }).catch(() => {});
      }
      await click(page, `//*[@id="task-${target.taskId}"]//button[@aria-label="Tùy chọn nhiệm vụ"]`);
      await click(page, '//li[@data-action="reopen-task"]');
      await confirmInDialog(page, "Mở lại");
      check("5 UI: reopen shows the task waiting again", await waitText(page, `Đã mở lại "${target.taskName}"`));
      await ctx.close();
    }
    const after = (await api(manager, `projects/${EVENT}/day-of`)).body?.tasks?.find((t) => t.taskId === target.taskId);
    check("5 API: task is Pending, completion cleared", after?.status === "Pending" && !after?.completedAt, JSON.stringify(after));
    check("5 API: reopening a task that is not done is refused", (await api(manager, `projects/${EVENT}/day-of/${target.taskId}/reopen`, "POST")).status === 400);

    // 3 and 4. Coordination sheet history and the department PDF with its tasks.
    const frozen = await api(manager, `projects/${EVENT}/beo/freeze`, "POST", { notes: "kiểm thử lịch sử" });
    check("4 setup: sheet approved", frozen.status === 200, JSON.stringify(frozen.body));
    check("4 API: the sheet returns its version history", (frozen.body?.history?.length ?? 0) >= 1);
    {
      const { ctx, page } = await signIn(browser, "manager@test.local", errors);
      await page.goto(`${BASE}/manager/projects/${EVENT}/beo`, { waitUntil: "networkidle2" });
      check("4 UI: the history box lists the approved version", await waitText(page, `Phiếu điều phối v${frozen.body?.version}`)
        && !(await page.evaluate(() => document.body.innerText.includes("Chưa chốt phiên bản nào"))));
      await ctx.close();
    }
    const pdf = await api(manager, `projects/${EVENT}/beo/department-sheets/Kitchen/pdf`, "GET", undefined, true);
    const text = pdf.bytes.toString("latin1");
    check("3 PDF: department sheet lists its tasks", pdf.status === 200 && text.includes("NHIEM VU CAN HOAN THANH") && text.startsWith("%PDF"), `status ${pdf.status}`);

    // 6. Phone number must start with 0 (reported: 1185028924 was accepted).
    const halls = (await api(manager, "halls?date=2027-01-05")).body ?? [];
    const hall = halls.find((h) => h.slots?.some((s) => s.available));
    const badPhone = await api(manager, "projects", "POST", {
      coupleName: "Khách Sai Số", customerEmail: `phone${Date.now()}@example.com`, customerPhone: "1185028924", hall: hall?.name,
      slot: hall?.slots.find((s) => s.available)?.slotName, eventDate: "2027-01-05", baseValue: 100000000, depositAmount: 30000000, paymentMethod: "Cash",
    });
    check("6 API: phone without a leading 0 is refused", badPhone.status === 400 && /bắt đầu bằng số 0/.test(badPhone.body?.message ?? ""), JSON.stringify(badPhone.body));
    {
      const { ctx, page } = await signIn(browser, "manager@test.local", errors);
      await page.goto(`${BASE}/manager/projects/new`, { waitUntil: "networkidle2" });
      await page.waitForSelector("#customerPhone", { visible: true, timeout: 10000 });
      await page.type("#customerPhone", "1185028924");
      await click(page, '//button[normalize-space(.)="Xác nhận & tạo sự kiện"]');
      check("6 UI: the form shows the phone rule", await waitText(page, "Số điện thoại phải bắt đầu bằng số 0"));
      await ctx.close();
    }

    // 8. The complaint detail label (customer screen) is plain Vietnamese.
    const html = await (await fetch(`${BASE}/`)).text();
    const scripts = [...html.matchAll(/src="([^"]+\.js)"/g)].map((m) => m[1]);
    const bundle = (await Promise.all(scripts.map(async (s) => (await fetch(new URL(s, BASE))).text()))).join("\n");
    check("8 bundle: complaint label is 'Thời gian gửi'", bundle.includes("Thời gian gửi:") && !bundle.includes("Thời점"));

    check("no page errors", errors.length === 0, errors.join(" | "));
  } catch (e) {
    check("suite ran to the end", false, e.stack);
  } finally {
    await browser.close();
  }
  console.log(`${bad === 0 ? "all" : "some"} bug report checks passed (${bad} failed)`);
  process.exit(bad ? 1 : 0);
})();
