// Report 4 use cases added on 2026-11-05 (docs/explain/sds-gaps.md), on WPSM_E2E_Test through the React build served by the API on 5300.
// Data is prepared through the API; the main flows are clicked in Chrome. Event dates are moved with SQL to reach T-7, the event day and after it.
const fs = require("fs");
const os = require("os");
const path = require("path");
const { execSync } = require("child_process");
const puppeteer = require("puppeteer-core");
const BASE = process.env.REACT_BASE || "http://localhost:5300";
const PW = process.env.TEST_PW;
const DB = "WPSM_E2E_Test";
let bad = 0;
const check = (name, ok, detail = "") => { if (!ok) bad++; console.log(`${ok ? "PASS" : "FAIL"} ${name} ${ok ? "" : detail}`); };
const sql = (q) => execSync(`sqlcmd -S "(localdb)\\MSSQLLocalDB" -d ${DB} -I -b -h -1 -W -Q "set nocount on; ${q}"`).toString().trim();
const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
const addDays = (n) => { const d = new Date(); d.setDate(d.getDate() + n); return iso(d); };
const PNG = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==", "base64");

async function api(token, p, method = "GET", body) {
  const isForm = body instanceof FormData;
  const r = await fetch(`${BASE}/api/wpsm/${p}`, {
    method,
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body && !isForm ? { "Content-Type": "application/json" } : {}) },
    body: body ? (isForm ? body : JSON.stringify(body)) : undefined,
  });
  const text = await r.text();
  let json = null;
  try { json = JSON.parse(text); } catch { /* empty body */ }
  return { status: r.status, body: json };
}
const token = async (email, password) => (await api(null, "auth/token", "POST", { email, password })).body?.accessToken;
const pngForm = (field, name = "proof.png", count = 1) => {
  const form = new FormData();
  for (let i = 0; i < count; i++) form.append(field, new Blob([PNG], { type: "image/png" }), name);
  return form;
};

// UI helpers (same conventions as react.js: real clicks, wait for MUI menus to close).
const waitText = (page, text, timeout = 10000) =>
  page.waitForFunction((t) => document.body.innerText.includes(t), { timeout }, text).then(() => true).catch(() => false);
const clickButton = async (page, label, scope = "") => {
  const handle = await page.waitForSelector(`::-p-xpath(${scope}//button[normalize-space(.)="${label}"])`, { visible: true, timeout: 10000 });
  await handle.click();
};
const typeInto = async (page, selector, text) => {
  await page.waitForSelector(selector, { visible: true, timeout: 10000 });
  await page.$eval(selector, (e) => { e.focus(); e.select?.(); });
  await page.keyboard.down("Control"); await page.keyboard.press("a"); await page.keyboard.up("Control");
  await page.keyboard.press("Backspace");
  await page.type(selector, text);
};
const chooseById = async (page, id, option) => {
  await (await page.waitForSelector(`#${id}`, { visible: true, timeout: 10000 })).click();
  await (await page.waitForSelector(`::-p-xpath(//li[@role="option"][contains(normalize-space(.), "${option}")])`, { visible: true, timeout: 10000 })).click();
  await page.waitForFunction(() => !document.querySelector('[role="listbox"]'), { timeout: 8000 });
};
const confirmDialog = async (page, label) => clickButton(page, label, '//div[@role="dialog"]');
const waitDialogClosed = (page) => page.waitForFunction(() => !document.querySelector('[role="dialog"]'), { timeout: 10000 }).then(() => true).catch(() => false);

async function signIn(browser, email, password, errors) {
  const ctx = await browser.createBrowserContext();
  const page = await ctx.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto(`${BASE}/login`, { waitUntil: "networkidle2" });
  await page.type("#email", email);
  await page.type("#password", password);
  await clickButton(page, "Đăng nhập");
  await page.waitForFunction(() => location.pathname !== "/login", { timeout: 10000 }).catch(() => {});
  return { ctx, page };
}

// Moves the event date. The hall/slot/date unique index (GBR-09) must stay free: keep the slot when possible,
// otherwise take any free slot (other halls too). T-7 accepts 1..7 days ahead; "after the event" accepts 1..4 days back.
function moveEvent(projectCode, offset) {
  const candidates = offset > 0 ? [offset, offset - 1, offset + 1, offset - 2, offset - 3].filter((d) => d >= 1 && d <= 7)
    : offset < 0 ? [offset, offset - 1, offset - 2, offset - 3] : [offset];
  for (const o of candidates) {
    const date = addDays(o);
    // One line: sqlcmd -Q does not accept line breaks inside the query.
    const free = sql(`select top 1 concat(s.HallId, ',', s.SlotId) from WeddingProjects w cross join Slots s where w.ProjectCode='${projectCode}' `
      + `and not exists (select 1 from WeddingProjects x where x.SlotId=s.SlotId and x.EventDate='${date}' and x.ProjectId<>w.ProjectId) `
      + "order by case when s.SlotId=w.SlotId then 0 when s.HallId=w.HallId then 1 else 2 end");
    if (free) {
      const [hallId, slotId] = free.split(",");
      sql(`update WeddingProjects set EventDate='${date}', HallId=${hallId}, SlotId=${slotId} where ProjectCode='${projectCode}'`);
      return date;
    }
  }
  throw new Error(`no free hall slot near T${offset}`);
}

(async () => {
  const errors = [];
  const browser = await puppeteer.launch({ headless: "new", executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
  const manager = await token("manager@test.local", PW);
  const admin = await token("admin@test.local", PW);
  check("manager and admin sign in", !!manager && !!admin);

  // ── Setup: project (M-03 API), freeze (UC-20), customer account ──────────────
  const eventDate = addDays(60);
  const halls = (await api(manager, `halls?date=${eventDate}`)).body ?? [];
  const hall = halls.find((h) => h.slots.some((s) => s.available));
  const slot = hall?.slots.find((s) => s.available);
  const customerEmail = `ops${Date.now()}@example.com`;
  const created = await api(manager, "projects", "POST", {
    coupleName: "Khách Vận Hành", customerEmail, customerPhone: "0900000011", hall: hall?.name, slot: slot?.slotName,
    eventDate, baseValue: 100000000, nrdAmount: 50000000, paymentMethod: "Cash",
  });
  const code = created.body?.project?.id;
  check("setup: project created", created.status === 201 && !!code, JSON.stringify(created.body));
  const frozen = await api(manager, `projects/${code}/freeze`, "POST");
  check("setup: baseline frozen", frozen.status === 200, JSON.stringify(frozen.body));
  let customerPw = created.body?.temporaryPassword;
  let customer = await token(customerEmail, customerPw);
  const changed = await api(customer, "auth/change-password", "POST", { currentPassword: customerPw, newPassword: "Ops-Cust0mer-2026", confirmPassword: "Ops-Cust0mer-2026" });
  customerPw = "Ops-Cust0mer-2026";
  customer = await token(customerEmail, customerPw);
  check("setup: customer changed the temporary password", changed.status < 300 && !!customer, JSON.stringify(changed.body));

  // ── A-08 (UC-40) through the API: overlap rejected (MSG23), valid set saved ──
  const overlap = await api(admin, "policies/penalty-rules", "PUT", { rules: [{ tDayMin: 0, tDayMax: 20, ratePercent: 10 }, { tDayMin: 15, tDayMax: 30, ratePercent: 5 }] });
  check("A-08 rejects overlapping T-day ranges (MSG23)", overlap.status === 400 && overlap.body?.message?.includes("MSG23"), JSON.stringify(overlap.body));
  const rules = await api(admin, "policies/penalty-rules", "PUT", { rules: [{ tDayMin: 0, tDayMax: 14, ratePercent: 10 }, { tDayMin: 15, tDayMax: 70, ratePercent: 2 }] });
  check("A-08 saves a T-day rule set", rules.status === 200 && rules.body?.length === 2, JSON.stringify(rules.body));
  check("manager cannot change penalty rules", (await api(manager, "policies/penalty-rules", "PUT", { rules: [] })).status === 403);

  // ── Change Request: C-06 create in the UI, M-08 + C-07 approve in the UI ────
  const specs = (await api(customer, `projects/${code}/change-requests/spec-items`)).body ?? [];
  const catering = specs.find((s) => s.category === "Ẩm thực") ?? specs[0];
  const outOfFrame = await api(customer, `projects/${code}/change-requests/preview`, "POST", { items: [{ specItemId: catering.specItemId, newQuantity: 2, newUnitPrice: 1 }] });
  check("CR preview rejects a price outside the PriceFrame (MSG06)", outOfFrame.status === 400 && outOfFrame.body?.message?.includes("MSG06"), JSON.stringify(outOfFrame.body));
  const preview = await api(customer, `projects/${code}/change-requests/preview`, "POST", { items: [{ specItemId: catering.specItemId, newQuantity: 20, newUnitPrice: catering.unitPrice }] });
  check("CR preview: delta and T-day penalty from the rule set (GBR-03)", preview.body?.priceDelta === 19 * catering.unitPrice && preview.body?.penaltyRate === 2,
    JSON.stringify(preview.body));

  let crCode = null;
  {
    const { ctx, page } = await signIn(browser, customerEmail, customerPw, errors);
    await page.goto(`${BASE}/customer/change-requests/new`, { waitUntil: "networkidle2" });
    await clickButton(page, "Gửi yêu cầu");
    check("C-06 requires type, item and reason", await waitText(page, "Trường Loại thay đổi là bắt buộc.") && await waitText(page, "Trường Lý do là bắt buộc."));
    await chooseById(page, "crType", "Đổi số bàn");
    await chooseById(page, "crSpecItem-0", catering.name);
    await typeInto(page, 'input[data-field="newQuantity"]', "20");
    await page.type('textarea[data-field="crReason"]', "Tăng số bàn do thêm khách");
    check("C-06 shows the live cost preview", await waitText(page, "Ước tính chi phí"));
    await clickButton(page, "Gửi yêu cầu");
    const opened = await page.waitForFunction(() => /\/customer\/change-requests\/CR-/.test(location.pathname), { timeout: 10000 }).then(() => true).catch(() => false);
    crCode = opened ? decodeURIComponent(page.url().split("/").pop()) : null;
    check("C-06 submits and opens C-07", opened && !!crCode, page.url());
    check("C-07 shows the negotiation history", await waitText(page, "Gửi thương lượng"));
    await ctx.close();
  }
  check("CR is Negotiating with a history entry (UC-07)", sql(`select Status from ChangeRequests where CRCode='${crCode}'`) === "Negotiating"
    && Number(sql(`select count(*) from CRHistories h join ChangeRequests c on c.CRId=h.CRId where c.CRCode='${crCode}'`)) >= 2);
  check("manager notified of the new CR", Number(sql(`select count(*) from Notifications n join WeddingProjects w on w.ManagerId=n.RecipientId where w.ProjectCode='${code}' and n.Type='CR update' and n.Message like N'%${crCode}%'`)) >= 1);

  {
    const { ctx, page } = await signIn(browser, "manager@test.local", PW, errors);
    await page.goto(`${BASE}/manager/projects/${code}/change-requests`, { waitUntil: "networkidle2" });
    check("M-07 lists the CR", await waitText(page, crCode));
    await page.goto(`${BASE}/manager/projects/${code}/change-requests/${crCode}`, { waitUntil: "networkidle2" });
    await clickButton(page, "Từ chối");
    await confirmDialog(page, "Từ chối");
    check("M-08 reject requires a reason", await waitText(page, "Trường Lý do từ chối là bắt buộc."));
    await clickButton(page, "Hủy");
    await waitDialogClosed(page);
    await clickButton(page, "Duyệt");
    await confirmDialog(page, "Duyệt");
    check("M-08 records the Manager approval (MSG20)", await waitText(page, "Đã ghi nhận quyết định"));
    await ctx.close();
  }
  check("CR stays Negotiating with one approval (GBR-04)", sql(`select Status from ChangeRequests where CRCode='${crCode}'`) === "Negotiating");

  {
    const { ctx, page } = await signIn(browser, customerEmail, customerPw, errors);
    await page.goto(`${BASE}/customer/change-requests/${crCode}`, { waitUntil: "networkidle2" });
    await clickButton(page, "Duyệt");
    await confirmDialog(page, "Duyệt");
    check("C-08 approval closes the CR", await waitText(page, "Đã duyệt"));
    check("C-07 links the generated addendum", await waitText(page, "Xem phụ lục ADD-"));
    await ctx.close();
  }
  const approved = sql(`select concat(c.Status,'|',cast(c.CostImpact as bigint),'|',cast(c.PenaltyFee as bigint),'|',cast(w.CurrentTotal as bigint)) from ChangeRequests c join WeddingProjects w on w.ProjectId=c.ProjectId where c.CRCode='${crCode}'`).split("|");
  const delta = 19 * catering.unitPrice, fee = Math.round(delta * 0.02);
  check("approved CR stores delta and penalty, project total updated (GBR-03, GBR-08)",
    approved[0] === "Approved" && Number(approved[1]) === delta && Number(approved[2]) === fee && Number(approved[3]) === 100000000 + delta + fee, approved.join("|"));
  check("approved items become a revised Spec Item version (GBR-01)",
    sql(`select count(*) from SpecItems s join WeddingProjects w on w.ProjectId=s.ProjectId where w.ProjectCode='${code}' and s.Version=2 and s.Quantity=20`) === "1");
  const addendum = (await api(manager, `projects/${code}/change-requests/${crCode}/addendum`)).body;
  check("addendum PDF generated and stored (UC-24)", !!addendum?.pdfFileId, JSON.stringify(addendum));
  const signed = await api(manager, `projects/${code}/change-requests/${crCode}/addendum/signed-copy`, "POST", pngForm("file", "scan.png"));
  check("signed addendum upload sets Signed", signed.status === 200 && signed.body?.status === "Signed" && !!signed.body?.signedFileId, JSON.stringify(signed.body));
  const signedUrl = await api(customer, `files/${addendum?.pdfFileId}`);
  check("customer gets a signed URL for the addendum PDF (API-02)", signedUrl.status === 200 && !!signedUrl.body?.url);
  const pdf = signedUrl.body?.url ? await fetch(`${BASE}${signedUrl.body.url}`) : null;
  check("addendum PDF downloads as a PDF", pdf?.status === 200 && (await pdf.arrayBuffer().then((b) => Buffer.from(b).subarray(0, 4).toString())) === "%PDF");

  // Penalty waiver (UC-47): pending exception blocks approval; Admin approval waives the fee.
  const cr2 = await api(manager, `projects/${code}/change-requests`, "POST", { type: "Decor change", reason: "Đổi hoa", submit: true,
    items: [{ specItemId: specs.find((s) => s.category === "Trang trí")?.specItemId ?? specs[2].specItemId, newQuantity: 2, newUnitPrice: (specs.find((s) => s.category === "Trang trí") ?? specs[2]).unitPrice }] });
  const cr2Code = cr2.body?.code;
  check("manager creates a CR on the customer's behalf", cr2.status === 201 && cr2.body?.createdByRole === "Manager", JSON.stringify(cr2.body));
  const exc = await api(manager, `projects/${code}/change-requests/${cr2Code}/exception-requests`, "POST", { note: "Khách thân thiết" });
  check("manager requests a penalty waiver", exc.status === 200 && exc.body?.exceptions?.[0]?.status === "Pending");
  const blocked = await api(customer, `projects/${code}/change-requests/${cr2Code}/approve`, "POST");
  check("approval blocked while the exception is pending", blocked.status === 400);
  const excId = sql(`select ExceptionRequestId from ExceptionRequests e join ChangeRequests c on c.CRId=e.CRId where c.CRCode='${cr2Code}'`);
  check("admin approves the waiver (A-18)", (await api(admin, `admin/exception-requests/${excId}/decision`, "POST", { approve: true, note: "Đồng ý miễn phí" })).status === 204);
  await api(customer, `projects/${code}/change-requests/${cr2Code}/approve`, "POST");
  const done2 = await api(manager, `projects/${code}/change-requests/${cr2Code}/approve`, "POST");
  check("waived CR approved without penalty", done2.body?.status === "Approved" && done2.body?.cost?.penaltyFee === 0 && done2.body?.cost?.penaltyWaived, JSON.stringify(done2.body?.cost));

  // GBR-12: another customer cannot see this project's CRs.
  const otherCustomerProject = sql(`select top 1 ProjectCode from WeddingProjects where ProjectCode<>'${code}'`);
  check("customer gets 404 on another project's CRs (GBR-12)", (await api(customer, `projects/${otherCustomerProject}/change-requests`)).status === 404);
  check("customer cannot open the BEO API (Manager only)", (await api(customer, `projects/${code}/beo`)).status === 403);

  // ── G-04 notifications ───────────────────────────────────────────────────────
  {
    const { ctx, page } = await signIn(browser, customerEmail, customerPw, errors);
    await page.goto(`${BASE}/notifications?tab=unread`, { waitUntil: "networkidle2" });
    check("G-04 lists unread CR updates", await waitText(page, crCode));
    await page.click("[data-notification]");
    check("opening a notification navigates to its screen", await page.waitForFunction(() => location.pathname.startsWith("/customer/"), { timeout: 8000 }).then(() => true).catch(() => false), page.url());
    await page.goto(`${BASE}/notifications`, { waitUntil: "networkidle2" });
    await clickButton(page, "Đánh dấu tất cả đã đọc");
    check("mark all read clears unread", await waitText(page, "Đã đánh dấu tất cả là đã đọc."));
    await ctx.close();
  }
  check("no unread notifications left for the customer", (await api(customer, "notifications?unread=true")).body?.length === 0);

  // ── Resource calendar (UC-25, UC-26, UC-47) ──────────────────────────────────
  const tag = Date.now().toString().slice(-6);
  const resourceId = sql(`insert Resources (Name, Type, TotalQuantity) values (N'Follow light E2E ${tag}', 'Follow Light', 2); select scope_identity()`).split(/\r?\n/).pop();
  const start = `${eventDate}T09:00:00Z`, end = `${eventDate}T15:00:00Z`;
  const alloc = await api(manager, "resource-allocations", "POST", { resourceId: Number(resourceId), projectCode: code, startTime: start, endTime: end, quantity: 2 });
  check("M-21 allocates within capacity", alloc.status === 200 && !!alloc.body?.allocation, JSON.stringify(alloc.body));
  const clash = await api(manager, "resource-allocations", "POST", { resourceId: Number(resourceId), projectCode: code, startTime: `${eventDate}T14:00:00Z`, endTime: `${eventDate}T18:00:00Z`, quantity: 1 });
  check("over-capacity overlap answers 409 with the clash (MSG08)", clash.status === 409 && clash.body?.conflicts?.length === 1 && clash.body?.message?.includes("MSG08"), JSON.stringify(clash.body));
  const later = await api(manager, "resource-allocations", "POST", { resourceId: Number(resourceId), projectCode: code, startTime: `${eventDate}T15:00:00Z`, endTime: `${eventDate}T18:00:00Z`, quantity: 1 });
  check("back-to-back allocation is not a conflict", later.status === 200);
  const excAlloc = await api(manager, "resource-allocations/exception-requests", "POST", { resourceId: Number(resourceId), projectCode: code, startTime: `${eventDate}T10:00:00Z`, endTime: `${eventDate}T11:00:00Z`, quantity: 1, note: "Thiếu đèn" });
  check("conflict escalated as an exception request", excAlloc.status === 200 && excAlloc.body?.allocation?.exceptionStatus === "Pending", JSON.stringify(excAlloc.body));
  // With the 2-unit allocation released, 10:00–11:00 has the pending 1-unit request only; counting it would make 3 > 2.
  check("manager releases an allocation", (await api(manager, `resource-allocations/${alloc.body?.allocation?.allocationId}`, "DELETE")).status === 204);
  const pendingIgnored = await api(manager, "resource-allocations", "POST", { resourceId: Number(resourceId), projectCode: code, startTime: `${eventDate}T10:00:00Z`, endTime: `${eventDate}T11:00:00Z`, quantity: 2 });
  check("pending-exception rows are ignored by the conflict check", pendingIgnored.status === 200, JSON.stringify(pendingIgnored.body));
  const excAllocId = sql(`select ExceptionRequestId from ExceptionRequests where AllocationId=${excAlloc.body?.allocation?.allocationId}`);
  await api(admin, `admin/exception-requests/${excAllocId}/decision`, "POST", { approve: false, note: "Không đủ thiết bị" });
  check("rejected exception releases the allocation", sql(`select Status from ResourceAllocations where AllocationId=${excAlloc.body?.allocation?.allocationId}`) === "Released");
  {
    const { ctx, page } = await signIn(browser, "manager@test.local", PW, errors);
    await page.goto(`${BASE}/manager/resource-calendar?from=${eventDate}&to=${eventDate}`, { waitUntil: "networkidle2" });
    check("M-21 shows the allocation lanes", await waitText(page, `Follow light E2E ${tag}`));
    await clickButton(page, "Phân bổ tài nguyên");
    await clickButton(page, "Lưu", '//div[@role="dialog"]');
    check("M-21 dialog validates required fields", await waitText(page, "Trường Tài nguyên là bắt buộc."));
    await clickButton(page, "Hủy");
    await page.goto(`${BASE}/manager/resource-calendar/conflicts/${excAlloc.body?.allocation?.allocationId}`, { waitUntil: "networkidle2" });
    check("M-22 shows the exception outcome", await waitText(page, "Admin đã từ chối ngoại lệ"));
    await ctx.close();
  }

  // ── BEO (UC-27..UC-29) ───────────────────────────────────────────────────────
  const early = await api(manager, `projects/${code}/beo/freeze`, "POST");
  check("BEO freeze blocked before T-7", early.status === 400 && early.body?.message?.includes("T-7"), JSON.stringify(early.body));
  const t5 = moveEvent(code, 5);
  {
    const { ctx, page } = await signIn(browser, "manager@test.local", PW, errors);
    await page.goto(`${BASE}/manager/projects/${code}/beo`, { waitUntil: "networkidle2" });
    check("M-10 shows the compiled timeline", await waitText(page, "Kiểm tra âm thanh"));
    await clickButton(page, "Duyệt và đóng băng");
    await confirmDialog(page, "Đóng băng");
    check("M-10 freezes the BEO", await waitText(page, "Đã đóng băng BEO"));
    await page.goto(`${BASE}/manager/projects/${code}/beo/department-sheets`, { waitUntil: "networkidle2" });
    // Wait for the rows: right after a full load the page is still restoring the session.
    await page.waitForSelector("#sheetTable tbody tr", { timeout: 8000 }).catch(() => {});
    check("M-11 lists four department PDFs", (await page.$$eval("#sheetTable tbody tr", (r) => r.length).catch(() => 0)) === 4);
    await ctx.close();
  }
  check("BEO Frozen with 4 sheets in the database (UC-28)", sql(`select concat(b.Status,'|',count(s.SheetId)) from BEOs b join WeddingProjects w on w.ProjectId=b.ProjectId left join BEODepartmentSheets s on s.BEOId=b.BEOId where w.ProjectCode='${code}' group by b.Status`) === "Frozen|4", t5);

  const cr3 = await api(customer, `projects/${code}/change-requests`, "POST", { type: "Menu change", reason: "Thêm món", submit: true,
    items: [{ specItemId: (await api(customer, `projects/${code}/change-requests/spec-items`)).body.find((s) => s.name === catering.name).specItemId, newQuantity: 22, newUnitPrice: catering.unitPrice }] });
  check("freeze blocked while a CR is pending (GBR-05)", (await api(manager, `projects/${code}/beo`)).body?.pendingCrCodes?.includes(cr3.body?.code));
  await api(customer, `projects/${code}/change-requests/${cr3.body?.code}/approve`, "POST");
  const cr3Done = await api(manager, `projects/${code}/change-requests/${cr3.body?.code}/approve`, "POST");
  check("late CR makes the frozen BEO Stale (GBR-05)", cr3Done.body?.beoEffect?.includes("Stale") && (await api(manager, `projects/${code}/beo`)).body?.status === "Stale", JSON.stringify(cr3Done.body?.beoEffect));
  {
    const { ctx, page } = await signIn(browser, "manager@test.local", PW, errors);
    await page.goto(`${BASE}/manager/projects/${code}/beo`, { waitUntil: "networkidle2" });
    check("M-10 shows MSG22 banner", await waitText(page, "MSG22"));
    await clickButton(page, "Duyệt lại");
    check("M-12 lists the changes since the freeze", await waitText(page, cr3.body?.code));
    await page.click('[role="dialog"] input[type="checkbox"]');
    await confirmDialog(page, "Duyệt lại");
    check("M-12 creates a new frozen version", await waitText(page, "Đã tạo phiên bản BEO mới"));
    await ctx.close();
  }
  check("BEO v2 Frozen, v1 kept for audit (UC-29)",
    sql(`select string_agg(concat(b.Version,':',b.Status), ',') within group (order by b.Version) from BEOs b join WeddingProjects w on w.ProjectId=b.ProjectId where w.ProjectCode='${code}'`) === "1:Stale,2:Frozen");

  // ── Payments: T-30 (M-14 API rules) ──────────────────────────────────────────
  const bankNoReceipt = await api(manager, `projects/${code}/payments`, "POST", (() => { const f = new FormData(); f.append("type", "T30"); f.append("amount", "1000"); f.append("method", "Bank"); f.append("paidDate", addDays(0)); return f; })());
  check("Bank payment needs a receipt", bankNoReceipt.status === 400, JSON.stringify(bankNoReceipt.body));
  const tooMuch = await api(manager, `projects/${code}/payments`, "POST", (() => { const f = new FormData(); f.append("type", "T30"); f.append("amount", "999999999999"); f.append("method", "Cash"); f.append("paidDate", addDays(0)); return f; })());
  check("payment above the balance rejected (MSG11)", tooMuch.status === 400 && tooMuch.body?.message?.includes("MSG11"), JSON.stringify(tooMuch.body));
  const t30 = await api(manager, `projects/${code}/payments`, "POST", (() => { const f = pngForm("receipt", "t30.png"); f.append("type", "T30"); f.append("amount", "30000000"); f.append("method", "Bank"); f.append("paidDate", addDays(0)); return f; })());
  check("T-30 payment recorded with receipt", t30.status === 200 && t30.body?.totalPaid === 80000000 && !!t30.body?.payments.find((p) => p.type === "T30")?.receiptFileId, JSON.stringify(t30.body));
  const earlyFinal = await api(manager, `projects/${code}/payments`, "POST", (() => { const f = new FormData(); f.append("type", "FINAL"); f.append("amount", "1000"); f.append("method", "Cash"); f.append("paidDate", addDays(0)); return f; })());
  check("final payment blocked before the event", earlyFinal.status === 400);

  // ── Day-of (UC-30..UC-32, JOB-05) ────────────────────────────────────────────
  moveEvent(code, 0);
  const loaded = await api(manager, `projects/${code}/day-of/load`, "POST");
  check("day-of timeline loaded from the template", loaded.status === 200 && loaded.body?.tasks?.length === 5, JSON.stringify(loaded.body));
  const required = loaded.body?.tasks?.find((t) => t.photoRequired);
  const noPhoto = await api(manager, `projects/${code}/day-of/${required?.taskId}/complete`, "POST", new FormData());
  check("proof task cannot complete without photos (GBR-06, MSG10)", noPhoto.status === 400 && noPhoto.body?.message?.includes("MSG10"));
  const four = await api(manager, `projects/${code}/day-of/${required?.taskId}/complete`, "POST", pngForm("photos", "p.png", 4));
  check("more than 3 photos rejected", four.status === 400);
  {
    const { ctx, page } = await signIn(browser, "manager@test.local", PW, errors);
    await page.goto(`${BASE}/manager/projects/${code}/day-of`, { waitUntil: "networkidle2" });
    await page.waitForSelector(`#task-${required?.taskId}`, { timeout: 10000 });
    await clickButton(page, "Hoàn thành", `//*[@id="task-${required?.taskId}"]`);
    const file = path.join(os.tmpdir(), `wpsm-proof-${process.pid}.png`);
    fs.writeFileSync(file, PNG);
    await (await page.waitForSelector('input[data-field="proofPhotos"]')).uploadFile(file);
    await confirmDialog(page, "Hoàn thành");
    check("M-17 uploads the photo and completes the task", await waitText(page, "Đã hoàn thành công việc."));
    await ctx.close();
  }
  check("task Done with one photo", sql(`select concat(t.Status,'|',count(p.PhotoId)) from DayOfTasks t left join ExecutionPhotos p on p.DayOfTaskId=t.DayOfTaskId where t.DayOfTaskId=${required?.taskId} group by t.Status`) === "Done|1");
  const photos = await api(customer, `projects/${code}/proof-photos`);
  check("C-11 API returns the photo with a signed URL", photos.body?.length === 1 && photos.body[0].imageUrl.includes("sig="), JSON.stringify(photos.body));
  const image = await fetch(`${BASE}${photos.body?.[0]?.imageUrl}`);
  check("signed photo URL serves the image", image.status === 200);
  const late = loaded.body?.tasks?.find((t) => t.taskId !== required?.taskId && !t.photoRequired);
  sql(`update DayOfTasks set DeadlineTime=dateadd(minute,-5,sysutcdatetime()) where DayOfTaskId=${late?.taskId}`);
  let overdue = "";
  for (let i = 0; i < 25 && overdue !== "Overdue"; i++) {
    await new Promise((r) => setTimeout(r, 4000));
    overdue = sql(`select Status from DayOfTasks where DayOfTaskId=${late?.taskId}`);
  }
  check("JOB-05 flags the missed deadline as Overdue (GBR-07)", overdue === "Overdue", overdue);
  check("M-23 lists the overdue task", (await api(manager, "manager/overdue-alerts")).body?.some((a) => a.taskId === late?.taskId));
  check("manager notified of the overdue task (MSG21)", Number(sql(`select count(*) from Notifications where Type='Overdue alert' and Message like N'%MSG21%'`)) >= 1);
  const lateDone = await api(manager, `projects/${code}/day-of/${late?.taskId}/complete`, "POST", new FormData());
  check("late completion clears Overdue and keeps the late marker", lateDone.body?.tasks?.find((t) => t.taskId === late?.taskId)?.isLate === true);

  // ── Final settlement (UC-33, UC-34) in the UI ────────────────────────────────
  moveEvent(code, -1);
  {
    const { ctx, page } = await signIn(browser, "manager@test.local", PW, errors);
    await page.goto(`${BASE}/manager/projects/${code}/payments`, { waitUntil: "networkidle2" });
    await clickButton(page, "Ghi nhận thanh toán");
    await chooseById(page, "paymentType", "Quyết toán");
    await chooseById(page, "paymentMethod", "Tiền mặt");
    await confirmDialog(page, "Ghi nhận");
    check("M-14 records the final payment", await waitText(page, "Đã ghi nhận thanh toán."));
    await page.goto(`${BASE}/manager/projects/${code}/payments/settlement-receipt`, { waitUntil: "networkidle2" });
    check("M-15 offers the settlement receipt", await waitText(page, "Tải / in biên nhận"));
    await ctx.close();
  }
  check("project Settled with a settlement receipt (UC-34)", sql(`select concat(w.Status,'|',count(f.FileId)) from WeddingProjects w left join StoredFiles f on f.ProjectId=w.ProjectId and f.Purpose='SettlementReceipt' where w.ProjectCode='${code}' group by w.Status`) === "Settled|1");

  // ── Ratings and complaints (UC-14..UC-16, UC-35) ─────────────────────────────
  {
    const { ctx, page } = await signIn(browser, customerEmail, customerPw, errors);
    await page.goto(`${BASE}/customer/payments`, { waitUntil: "networkidle2" });
    check("C-10 shows the settlement receipt link", await waitText(page, "Biên nhận quyết toán"));
    await page.goto(`${BASE}/customer/post-event/ratings`, { waitUntil: "networkidle2" });
    // MUI Rating: each star is a visually hidden radio with a <label for>; click the label of the 5th star.
    const starId = await page.$eval('[data-module="Hall"] input[type="radio"][value="5"]', (e) => e.id);
    await page.click(`label[for="${starId}"]`);
    await clickButton(page, "Gửi đánh giá");
    check("C-13 saves the rating (MSG26)", await waitText(page, "MSG26"));
    await page.goto(`${BASE}/customer/complaints/new`, { waitUntil: "networkidle2" });
    await chooseById(page, "complaintBeo", "BEO v2");
    await page.type('textarea[data-field="complaintDescription"]', "Hoa trên bàn khác BEO");
    await clickButton(page, "Gửi khiếu nại");
    check("C-15 files the complaint (MSG25)", await waitText(page, "MSG25"));
    await ctx.close();
  }
  check("rating stored once", sql(`select count(*) from Ratings r join WeddingProjects w on w.ProjectId=r.ProjectId where w.ProjectCode='${code}' and r.Module='Hall' and r.Stars=5`) === "1");
  check("rating cannot be changed (GBR-10)", (await api(customer, `projects/${code}/ratings`, "POST", { ratings: [{ module: "Hall", stars: 1 }] })).status === 400);
  const complaintCode = sql(`select top 1 ComplaintCode from Complaints c join WeddingProjects w on w.ProjectId=c.ProjectId where w.ProjectCode='${code}'`);
  {
    const { ctx, page } = await signIn(browser, "manager@test.local", PW, errors);
    await page.goto(`${BASE}/manager/projects/${code}/complaints/${complaintCode}`, { waitUntil: "networkidle2" });
    check("M-19 shows the BEO sheets and photos", await waitText(page, "Ảnh minh chứng ngày cưới"));
    await clickButton(page, "Phản hồi và đóng");
    await confirmDialog(page, "Đã giải quyết");
    check("M-19 resolve requires a response", await waitText(page, "là bắt buộc"));
    await page.type('textarea[data-field="dialogText"]', "Nhà hàng xin lỗi và hoàn 1 bàn hoa");
    await confirmDialog(page, "Đã giải quyết");
    check("M-19 resolves the complaint", await waitText(page, "Đã giải quyết khiếu nại"));
    await ctx.close();
  }
  {
    const { ctx, page } = await signIn(browser, customerEmail, customerPw, errors);
    await page.goto(`${BASE}/customer/complaints/${complaintCode}`, { waitUntil: "networkidle2" });
    check("C-16 shows the resolution", await waitText(page, "Nhà hàng xin lỗi"));
    await ctx.close();
  }

  // ── M-01, M-06 and Admin A-02 / A-04 / A-08 screens ──────────────────────────
  {
    const { ctx, page } = await signIn(browser, "manager@test.local", PW, errors);
    await page.goto(`${BASE}/manager/dashboard`, { waitUntil: "networkidle2" });
    check("M-01 shows My tasks", await waitText(page, "Việc của tôi"));
    await page.goto(`${BASE}/manager/projects/${code}/roadmap?owner=Manager`, { waitUntil: "networkidle2" });
    await clickButton(page, "Đánh dấu xong");
    check("M-06 marks a Manager task done", await waitText(page, "Đã đánh dấu hoàn thành."));
    await ctx.close();
  }
  check("customer cannot complete Manager tasks via M-06 API", (await api(customer, `projects/${code}/roadmap/tasks/1/complete`, "POST")).status === 403);
  {
    const { ctx, page } = await signIn(browser, "admin@test.local", PW, errors);
    await page.goto(`${BASE}/admin/catalog`, { waitUntil: "networkidle2" });
    await page.waitForSelector("[data-item]");
    const itemId = await page.$eval("[data-item]", (r) => r.getAttribute("data-item"));
    await clickButton(page, "Sửa", `//*[@data-item="${itemId}"]`);
    check("A-03 opens in edit mode", await waitText(page, `Sửa dịch vụ #${itemId}`));
    await clickButton(page, "Hủy");
    await waitDialogClosed(page);
    const before = sql(`select Status from ServiceCatalogItems where ItemId=${itemId}`);
    await page.click(`[data-item="${itemId}"] button:last-child`);
    await waitText(page, before === "Active" ? "Đã ngừng dùng" : "Đã dùng lại");
    const after = sql(`select Status from ServiceCatalogItems where ItemId=${itemId}`);
    check("A-02 toggles the item status", before !== after, `${before} -> ${after}`);
    // The button is disabled while the first call runs; click again only once it shows the opposite action.
    const toggleLabel = (id, label) => page.waitForFunction((i, l) => {
      const b = document.querySelector(`[data-item="${i}"] button:last-child`);
      return b && !b.disabled && b.innerText.includes(l);
    }, { timeout: 8000 }, id, label).catch(() => {});
    await toggleLabel(itemId, before === "Active" ? "Dùng lại" : "Ngừng dùng");
    // L-14: the fixed toast sits over the last column; close it so the real click reaches the button.
    const close = await page.$('.MuiSnackbar-root button[aria-label="Close"]');
    if (close) { await close.click(); await page.waitForFunction(() => !document.querySelector(".MuiSnackbar-root"), { timeout: 5000 }).catch(() => {}); }
    await page.click(`[data-item="${itemId}"] button:last-child`);
    await page.waitForFunction((id, s) => document.querySelector(`[data-item="${id}"] button:last-child`)?.innerText.includes(s), { timeout: 8000 },
      itemId, before === "Active" ? "Ngừng dùng" : "Dùng lại").catch(() => {});
    const back = sql(`select Status from ServiceCatalogItems where ItemId=${itemId}`);
    check("A-02 toggles the item back", back === before,
      `${before} -> ${back}; buttons: ${await page.$eval(`[data-item="${itemId}"]`, (r) => r.innerText).catch(() => "?")}`);
    await page.goto(`${BASE}/admin/price-frames`, { waitUntil: "networkidle2" });
    await clickButton(page, "Lịch sử");
    check("A-04 shows the price history", !!(await page.waitForSelector("#priceHistory tbody tr", { timeout: 8000 }).catch(() => null)));
    await page.goto(`${BASE}/admin/policies`, { waitUntil: "networkidle2" });
    check("A-06 shows the T-day rule set", await waitText(page, "Từ T-70 đến T-15"));
    await (await page.waitForSelector('::-p-xpath((//button[normalize-space(.)="Chỉnh sửa"])[2])', { visible: true })).click();
    await clickButton(page, "Thêm khoảng");
    const inputs = await page.$$('[data-rule="2"] input');
    await inputs[0].type("10"); await inputs[1].type("20"); await inputs[2].type("3");
    await confirmDialog(page, "Lưu");
    check("A-08 dialog highlights overlapping ranges (MSG23)", await waitText(page, "MSG23"));
    await ctx.close();
  }

  check("no uncaught page errors", errors.length === 0, errors.slice(0, 3).join(" | "));
  await browser.close();
  console.log(`operations passed: ${bad === 0}`);
  process.exit(bad ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(2); });
