// A-01, A-09..A-18: seeds Admin data on WPSM_E2E_Test, then drives the screens with real clicks.
const fs = require("fs");
const os = require("os");
const path = require("path");
const puppeteer = require("puppeteer-core");
const { execSync } = require("child_process");
const BASE = "http://localhost:5301";
const DB = "WPSM_E2E_Test";
const sql = q => execSync(`sqlcmd -S "(localdb)\\MSSQLLocalDB" -d ${DB} -I -b -h -1 -W -Q "set nocount on; ${q}"`).toString().trim();
const sqlFile = text => {
  const file = path.join(os.tmpdir(), `wpsm-admin-seed-${process.pid}.sql`);
  fs.writeFileSync(file, text, "utf8");
  return execSync(`sqlcmd -S "(localdb)\\MSSQLLocalDB" -d ${DB} -I -b -h -1 -W -f 65001 -i "${file}"`).toString().trim();
};
let bad = 0;
const check = (name, ok, detail = "") => { if (!ok) bad++; console.log(`${ok ? "PASS" : "FAIL"} ${name} ${detail}`); };

const seed = () => {
  const photoDir = process.env.FILE_ROOT || os.tmpdir();
  fs.mkdirSync(photoDir, { recursive: true });
  const photo = path.join(photoDir, "admin-proof.png");
  fs.writeFileSync(photo, Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==", "base64"));
  const tag = Date.now().toString().slice(-6);
  const out = sqlFile(`SET NOCOUNT ON;
DECLARE @p INT, @m INT, @hall INT, @task INT, @late INT, @beo INT, @cr INT, @res INT, @alloc INT;
SELECT TOP 1 @p = ProjectId, @m = ManagerId, @hall = HallId FROM dbo.WeddingProjects ORDER BY ProjectId;
INSERT dbo.DayOfTasks (ProjectId, TaskName, DeadlineTime, Status, PhotoRequired) VALUES (@p, N'Admin E2E sound check', DATEADD(MINUTE, -30, SYSUTCDATETIME()), 'Overdue', 1);
INSERT dbo.DayOfTasks (ProjectId, TaskName, DeadlineTime, CompletedAt, Status, PhotoRequired) VALUES (@p, N'Admin E2E stage decor', DATEADD(MINUTE, -20, SYSUTCDATETIME()), SYSUTCDATETIME(), 'Done', 1);
SET @late = SCOPE_IDENTITY();
INSERT dbo.ExecutionPhotos (DayOfTaskId, UploaderId, ImageFilePath) VALUES (@late, @m, N'${photo.replace(/'/g, "''")}');
INSERT dbo.BEOs (ProjectId, Version, Status, FrozenAt, ApprovedBy) VALUES (@p, 90 + ABS(CHECKSUM(NEWID())) % 9, 'Frozen', SYSUTCDATETIME(), @m);
SET @beo = SCOPE_IDENTITY();
INSERT dbo.Complaints (ComplaintCode, ProjectId, BEOId, Description, Status, EscalationNote, EscalatedAt) VALUES (N'CPL-E2E-${tag}', @p, @beo, N'Màu hoa khác baseline', 'Escalated', N'Khách đổi màu qua CR', SYSUTCDATETIME());
INSERT dbo.ChangeRequests (CRCode, ProjectId, CreatedBy, Type, Description, CostImpact, PenaltyRate, PenaltyFee, Status) VALUES (N'CR-E2E-${tag}', @p, @m, 'Decor change', N'Đổi màu hoa', 4500000, 20, 900000, 'Negotiating');
SET @cr = SCOPE_IDENTITY();
INSERT dbo.Resources (Name, Type, TotalQuantity) VALUES (N'Audio E2E ${tag}', 'Audio', 1);
SET @res = SCOPE_IDENTITY();
INSERT dbo.ResourceAllocations (ResourceId, HallId, ProjectId, StartTime, EndTime, AllocatedQuantity) VALUES (@res, @hall, @p, DATEADD(DAY, 3, SYSUTCDATETIME()), DATEADD(HOUR, 76, SYSUTCDATETIME()), 1);
SET @alloc = SCOPE_IDENTITY();
INSERT dbo.ExceptionRequests (RequestCode, SourceType, ProjectId, CRId, RequestedBy, RequestNote) VALUES (N'EXC-W${tag}', 'CrPenaltyWaiver', @p, @cr, @m, N'Khách quen, xin miễn phí phạt');
INSERT dbo.ExceptionRequests (RequestCode, SourceType, ProjectId, AllocationId, RequestedBy, RequestNote) VALUES (N'EXC-R${tag}', 'ResourceConflict', @p, @alloc, @m, N'Thiếu dàn âm thanh');
SELECT CONCAT(@cr, ',', @alloc);`);
  const [cr, alloc] = out.split(/\r?\n/).pop().split(",");
  return { tag, cr, alloc };
};

const login = async (browser, email, password) => {
  const ctx = await browser.createBrowserContext();
  const page = await ctx.newPage();
  const errors = [];
  page.on("pageerror", e => errors.push(e.message));
  await page.goto(`${BASE}/G01`, { waitUntil: "networkidle2" });
  await page.type("#email", email);
  await page.type("#password", password);
  await page.click('#loginForm button[type="submit"]');
  await page.waitForNavigation({ waitUntil: "networkidle2" }).catch(() => {});
  const api = (p, method = "GET", body) => page.evaluate(async (p, method, body) => {
    const r = await fetch(`/api/wpsm/${p}`, { method, headers: body ? { "Content-Type": "application/json" } : {}, body: body ? JSON.stringify(body) : undefined });
    return { status: r.status, body: await r.json().catch(() => null) };
  }, p, method, body);
  return { page, api, errors };
};
const open = async (page, route) => { await page.goto(BASE + route, { waitUntil: "networkidle0" }); await new Promise(r => setTimeout(r, 400)); };
// The list re-renders when its fetch returns; clicking before that loses the opened detail.
const waitForRow = (page, table, code) =>
  page.waitForFunction((table, code) => [...document.querySelectorAll(`${table} tr`)].some(r => r.textContent.includes(code)), { timeout: 8000 }, table, code).catch(() => {});
const text = (page, selector) => page.$eval(selector, e => e.textContent.trim()).catch(() => "");

(async () => {
  const { tag, cr, alloc } = seed();
  const browser = await puppeteer.launch({ headless: "new", executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
  const admin = await login(browser, "admin@test.local", process.env.TEST_PW);
  const { page } = admin;
  check("admin form login lands on A-01", new URL(page.url()).pathname === "/A01", page.url());

  await page.waitForFunction(() => document.querySelector('[data-field="overdueTasks"]').textContent !== "—", { timeout: 8000 }).catch(() => {});
  check("A-01 overdue KPI counts seeded task", Number(await text(page, '[data-field="overdueTasks"]')) >= 1);
  check("A-01 escalated KPI", Number(await text(page, '[data-field="escalatedComplaints"]')) >= 1);
  check("A-01 alert strip lists task", (await text(page, "#a01Alerts")).includes("Admin E2E sound check"));
  await page.waitForFunction(() => [...document.querySelectorAll("#a01Photos img")].some(i => i.complete && i.naturalWidth > 0), { timeout: 8000 }).catch(() => {});
  check("A-01 latest photo image loads", await page.$$eval("#a01Photos img", imgs => imgs.some(i => i.naturalWidth > 0)));

  await open(page, "/A09");
  check("A-09 utilization shown", (await text(page, '[data-field="utilization"]')).includes("%"));
  await page.click('main a[href^="/A12"]');
  await page.waitForFunction(() => location.pathname === "/A12" && document.querySelector("#reportTable tr"), { timeout: 8000 }).catch(() => {});
  check("A-12 opened from tab keeps range", new URL(page.url()).searchParams.has("from"));
  check("A-12 lists seeded CR type", (await text(page, "#reportTable")).includes("Decor change"));
  check("A-12 chart has 4 status bars", (await page.$$("#reportChart .rounded-t")).length === 4);
  await open(page, "/A10");
  check("A-10 table and chart", (await page.$$("#reportTable tr")).length >= 1 && (await page.$$("#reportChart .rounded-t")).length >= 1);
  await open(page, "/A11");
  check("A-11 has month rows", (await page.$$("#reportTable tr")).length >= 1);
  await page.$eval("#reportFrom", e => { e.value = "2026-12-31"; });
  await page.$eval("#reportTo", e => { e.value = "2026-01-01"; });
  await page.click('[data-action="apply-report"]');
  check("A-11 rejects from > to", (await text(page, "#reportError")).length > 0);
  check("API rejects from > to", (await admin.api("admin/reports?from=2026-12-31&to=2026-01-01")).status === 400);

  await open(page, "/A13");
  await page.waitForFunction(() => document.querySelector("#a13Photos a"), { timeout: 8000 }).catch(() => {});
  check("A-13 feed shows today's photo", (await text(page, "#a13Photos")).includes("Admin E2E stage decor"));
  await page.select("#feedStatus", "Pending");
  check("A-13 status filter hides Done photo", !(await text(page, "#a13Photos")).includes("Admin E2E stage decor"));
  await page.select("#feedStatus", "");
  const card = await page.$('#a13Photos a[href^="/A14"]');
  await card.click();
  await page.waitForFunction(() => location.pathname === "/A14" && document.querySelector('[data-field="task"]').textContent !== "—", { timeout: 8000 }).catch(() => {});
  check("A-14 shows task", (await text(page, '[data-field="task"]')).includes("Admin E2E"));
  check("A-14 marks late upload", (await text(page, '[data-field="uploadedAt"]')).includes("trễ hạn"));
  await page.waitForFunction(() => document.getElementById("a14Image").naturalWidth > 0, { timeout: 8000 }).catch(() => {});
  check("A-14 image loads", await page.$eval("#a14Image", i => i.naturalWidth > 0));

  await open(page, "/A15");
  check("A-15 lists overdue task", (await text(page, "#a15Table")).includes("Admin E2E sound check"));

  await open(page, "/A16");
  await waitForRow(page, "#a16Table", `CPL-E2E-${tag}`);
  const complaintRow = await page.$$eval("#a16Table tr", (rows, code) => rows.findIndex(r => r.textContent.includes(code)), `CPL-E2E-${tag}`);
  check("A-16 lists escalated complaint", complaintRow >= 0);
  await page.click(`#a16Table tr:nth-child(${complaintRow + 1}) [data-action="open-complaint"]`);
  check("A-16 detail shows manager note", (await text(page, '#a16Detail [data-field="managerNote"]')).includes("CR"));
  await page.click('[data-action="open-resolve"]');
  check("A-16 empty decision blocked", (await text(page, "#a16Error")).length > 0 && await page.$eval("#modal-a17", m => m.classList.contains("hidden")));
  await page.type("#a16Decision", "CR đã duyệt đổi màu; không bồi thường.");
  await page.click('[data-action="open-resolve"]');
  await page.click('[data-action="confirm-resolve"]');
  await page.waitForFunction(() => document.getElementById("modal-a17").classList.contains("hidden"), { timeout: 8000 }).catch(() => {});
  check("A-17 resolves complaint in DB", sql(`select Status + '|' + cast(case when ResolvedBy is null then 0 else 1 end as varchar) from Complaints where ComplaintCode='CPL-E2E-${tag}'`) === "Resolved|1");
  check("A-17 notifies Manager and Customer", sql(`select count(*) from Notifications where Message like N'%CPL-E2E-${tag}%'`) === "2");
  check("A-16 list no longer shows it", !(await text(page, "#a16Table")).includes(`CPL-E2E-${tag}`));

  await open(page, "/A18");
  const openRequest = async code => {
    await waitForRow(page, "#a18Table", code);
    const index = await page.$$eval("#a18Table tr", (rows, code) => rows.findIndex(r => r.textContent.includes(code)), code);
    await page.click(`#a18Table tr:nth-child(${index + 1}) [data-action="open-exception"]`);
    return index;
  };
  check("A-18 lists waiver request", (await openRequest(`EXC-W${tag}`)) >= 0);
  check("A-18 context shows penalty", (await text(page, '#a18Detail [data-field="context"]')).includes("900.000"));
  await page.click('[data-action="approve-exception"]');
  check("A-18 empty note blocked", (await text(page, "#a18Error")).length > 0 && sql(`select Status from ExceptionRequests where RequestCode='EXC-W${tag}'`) === "Pending");
  await page.type("#a18Note", "Khách quen, đồng ý miễn phí phạt.");
  await page.click('[data-action="approve-exception"]');
  await page.waitForFunction(() => document.getElementById("a18Detail").classList.contains("hidden"), { timeout: 8000 }).catch(() => {});
  check("A-18 approve waives CR penalty", sql(`select cast(PenaltyFee as int) from ChangeRequests where CRId=${cr}`) === "0");
  check("A-18 approve logged in CR history", sql(`select count(*) from CRHistories where CRId=${cr} and Action='ExceptionApproved'`) === "1");
  await openRequest(`EXC-R${tag}`);
  await page.type("#a18Note", "Không đủ thiết bị, đổi sang dàn khác.");
  await page.click('[data-action="reject-exception"]');
  await page.waitForFunction(() => document.getElementById("a18Detail").classList.contains("hidden"), { timeout: 8000 }).catch(() => {});
  check("A-18 reject keeps allocation", sql(`select r.Status + '|' + a.Status from ExceptionRequests r join ResourceAllocations a on a.AllocationId=r.AllocationId where r.RequestCode='EXC-R${tag}'`) === "Rejected|Reserved", `alloc ${alloc}`);
  const id = sql(`select ExceptionRequestId from ExceptionRequests where RequestCode='EXC-R${tag}'`);
  check("decided request cannot be decided again", (await admin.api(`admin/exception-requests/${id}/decision`, "POST", { approve: true, note: "x" })).status === 400);
  check("admin pages have no JS errors", admin.errors.length === 0, admin.errors.join(" | "));

  const manager = await login(browser, "manager@test.local", process.env.TEST_PW);
  check("manager API blocked", (await manager.api("admin/dashboard")).status === 403);
  const managerPage = await manager.page.goto(`${BASE}/A01`, { waitUntil: "domcontentloaded" });
  check("manager page /A01 blocked", managerPage.status() === 403);

  await browser.close();
  console.log(`admin passed: ${bad === 0}`);
  process.exit(bad ? 1 : 0);
})().catch(e => { console.error(e); process.exit(2); });
