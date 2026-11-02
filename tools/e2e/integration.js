const puppeteer = require("puppeteer-core");
const crypto = require("crypto");
const fs = require("fs");
const { execSync } = require("child_process");
const BASE = "http://localhost:5301";
const sql = q => execSync(`sqlcmd -S "(localdb)\\MSSQLLocalDB" -d WPSM_E2E_Test -I -b -h -1 -W -Q "set nocount on; ${q}"`).toString().trim();
const results = [];
const check = (name, ok, extra = "") => { results.push(!!ok); console.log(`${ok ? "PASS" : "FAIL"} ${name} ${extra}`); };
const sleep = ms => new Promise(r => setTimeout(r, ms));
const chunk = (type, data) => { const n = Buffer.alloc(4); n.writeUInt32BE(data.length); return Buffer.concat([n, Buffer.from(type), data, Buffer.alloc(4)]); };
const PNG = [...Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), chunk("IHDR", Buffer.alloc(13)), chunk("tEXt", Buffer.from("GPS-SECRET")), chunk("IDAT", Buffer.alloc(4)), chunk("IEND", Buffer.alloc(0))])];
const app = (m, data) => { const n = Buffer.alloc(2); n.writeUInt16BE(data.length + 2); return Buffer.concat([Buffer.from([0xff, m]), n, data]); };
const JPG = [...Buffer.concat([Buffer.from([0xff, 0xd8]), app(0xe0, Buffer.from("JFIF\0")), app(0xe1, Buffer.from("Exif\0\0GPS-SECRET")), Buffer.from([0xff, 0xda, 0, 2, 1, 2, 0xff, 0xd9])])];
const PDF = [0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34];

const client = async (browser) => {
  const ctx = await browser.createBrowserContext();
  const page = await ctx.newPage();
  await page.setViewport({ width: 1440, height: 1000 });
  await page.goto(`${BASE}/G01`, { waitUntil: "domcontentloaded" });
  const api = (path, method = "GET", body) => page.evaluate(async (path, method, body) => {
    const r = await fetch(`/api/wpsm/${path}`, { method, headers: body ? { "Content-Type": "application/json" } : {}, body: body ? JSON.stringify(body) : undefined });
    return { status: r.status, body: await r.json().catch(() => null) };
  }, path, method, body);
  const upload = (path, field, bytes, name, extra = {}) => page.evaluate(async (path, field, bytes, name, extra) => {
    const f = new FormData();
    f.append(field, new Blob([new Uint8Array(bytes)]), name);
    for (const [k, v] of Object.entries(extra)) f.append(k, v);
    const r = await fetch(`/api/wpsm/${path}`, { method: "POST", body: f });
    return { status: r.status, body: await r.json().catch(() => null) };
  }, path, field, bytes, name, extra);
  const download = url => page.evaluate(async url => {
    const r = await fetch(url);
    return { status: r.status, size: (await r.arrayBuffer()).byteLength, disposition: r.headers.get("content-disposition"), type: r.headers.get("content-type"), nosniff: r.headers.get("x-content-type-options") };
  }, url);
  return { page, api, upload, download };
};

(async () => {
  const browser = await puppeteer.launch({ headless: "new", executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
  const pw = process.env.TEST_PW;
  const manager = await client(browser);
  check("manager login", (await manager.api("auth/login", "POST", { email: "manager@test.local", password: pw })).status === 200);

  const mail = `c${Date.now()}@example.com`;
  const base = { customerPhone: "0900000002", hall: "Sảnh Pha Lê", slot: "Lunch", baseValue: 100000000, nrdAmount: 30000000 };
  const created = await manager.api("projects", "POST", { ...base, coupleName: "Khách C02", customerEmail: mail, eventDate: process.env.EVENT_DATE, paymentMethod: "Cash" });
  check("project created", created.status === 201, JSON.stringify(created.body?.message || ""));
  const id = created.body.project.id, tempPw = created.body.temporaryPassword;
  check("credentials queued with project", sql(`select count(*) from EmailOutbox where Kind='Credentials' and ToEmail='${mail}'`) === "1");

  const customer = await client(browser);
  await customer.api("auth/login", "POST", { email: mail, password: tempPw });
  const newPw = "Cust0mer-Pass-2026!";
  const changed = await customer.api("auth/change-password", "POST", { currentPassword: tempPw, newPassword: newPw, confirmPassword: newPw });
  check("customer password changed", changed.status < 300, String(changed.status));
  await customer.api("auth/login", "POST", { email: mail, password: newPw });

  const openC02 = async () => { await customer.page.goto(`${BASE}/C02`, { waitUntil: "domcontentloaded" }); await customer.page.waitForFunction(() => window.wpsm?.project); };
  const field = name => customer.page.$eval(`[data-field="${name}"]`, e => e.textContent.trim());
  const hidden = sel => customer.page.$eval(sel, e => e.classList.contains("hidden"));
  await openC02();
  check("C-02 pending message before freeze", !(await hidden("#c02PendingNotice")) && (await hidden("#c02SpecSection")));
  check("C-02 hall real", (await field("c02Hall")) === "Sảnh Pha Lê", await field("c02Hall"));
  check("C-02 slot with time", (await field("c02Slot")).includes("11:00 - 14:00"), await field("c02Slot"));
  check("C-02 base value", (await field("c02BaseValue")).startsWith("100.000.000"));
  check("C-02 NRD paid", (await field("c02Nrd")).startsWith("30.000.000") && (await field("c02NrdPercent")) === "30%" && (await field("c02NrdStatus")).startsWith("Đã xác nhận"));

  const items = [[1, 25000000, 1], [2, 3500000, 10], [3, 12000000, 1], [4, 4000000, 1], [5, 8000000, 1]].map(([catalogItemId, unitPrice, quantity]) => ({ catalogItemId, unitPrice, quantity, description: `Mô tả <${catalogItemId}>` }));
  const savedSpec = await manager.api(`projects/${id}/specification`, "PUT", { summary: "", guestCount: 0, items });
  check("spec saved", savedSpec.status === 200, `${savedSpec.status} ${JSON.stringify(savedSpec.body?.message)}`);
  const frozenRes = await manager.api(`projects/${id}/freeze`, "POST", {});
  check("baseline frozen", frozenRes.status === 200, `${frozenRes.status} ${JSON.stringify(frozenRes.body?.message)}`);
  check("freeze email queued", sql(`select count(*) from EmailOutbox where Kind='Reminder' and ToEmail='${mail}'`) === "1");

  await openC02();
  check("C-02 spec after freeze", (await hidden("#c02PendingNotice")) && !(await hidden("#c02SpecSection")));
  check("C-02 5 groups + 5 lines", (await field("c02GroupCount")) === "5" && (await customer.page.$$eval("[data-c02-line]", l => l.length)) === 5);
  check("C-02 version marker", (await customer.page.$$eval("[data-c02-line]", l => l.every(x => x.textContent.includes("Baseline v1")))) && (await field("c02Version")).includes("Baseline v1"));
  check("C-02 read-only", (await customer.page.$$eval('[data-wpsm-screen="C02"] main input, [data-wpsm-screen="C02"] main select, [data-wpsm-screen="C02"] main textarea', i => i.length)) === 0);
  check("C-02 description escaped", (await customer.page.$eval("[data-c02-line]", e => e.textContent)).includes("Mô tả <"));
  check("C-02 no mock left", !(await customer.page.$eval("main", m => /Sảnh Hoa Hồng|310\.000\.000|93\.000\.000|217\.000\.000/.test(m.textContent))));
  check("C-02 outstanding", (await field("c02Outstanding")).startsWith("70.000.000"), await field("c02Outstanding"));
  check("C-02 toggle group", await customer.page.evaluate(() => { toggleCategory(1); const a = document.getElementById("content-1").classList.contains("hidden"); toggleAllGroups(); toggleAllGroups(); return a; }));
  const denied = await customer.api(`projects/${id}/specification`, "PUT", { summary: "", guestCount: 0, items: [] });
  check("customer cannot edit spec", denied.status === 403, String(denied.status));
  await customer.page.screenshot({ path: process.env.SHOT });

  // API-01
  const form = { purpose: "ProofPhoto", projectCode: id };
  const up = await manager.upload("files", "file", PNG, "../../anh.png", form);
  check("API-01 upload png", up.status === 201, JSON.stringify(up.body));
  check("API-01 path stripped from name", up.body?.originalName === "anh.png", up.body?.originalName);
  check("API-01 rejects fake png", (await manager.upload("files", "file", [1, 2, 3, 4, 5, 6, 7, 8], "x.png", form)).status === 400);
  check("API-01 rejects bad purpose", (await manager.upload("files", "file", PNG, "a.png", { ...form, purpose: "Other" })).status === 400);
  const big = new Array(10 * 1024 * 1024 + 1).fill(0); PNG.forEach((b, i) => big[i] = b);
  check("API-01 rejects > 10 MB", (await manager.upload("files", "file", big, "b.png", form)).status === 400);
  const fileCount = sql("select count(*) from StoredFiles");
  check("API-01 rejects truncated image", (await manager.upload("files", "file", PNG.slice(0, 12), "t.png", form)).status === 400);
  check("API-01 only valid file stored", fileCount === "1", fileCount);
  const storageKey = sql("select StorageKey from StoredFiles");
  check("API-01 file on private disk", fs.existsSync(`${process.env.FILE_ROOT}/${storageKey}`), storageKey);
  const storedPng = fs.readFileSync(`${process.env.FILE_ROOT}/${storageKey}`);
  check("PNG metadata removed", !storedPng.includes("GPS-SECRET") && storedPng.includes("IDAT") && storedPng.includes("IEND"));
  const jpg = await manager.upload("files", "file", JPG, "b.jpg", form);
  const storedJpg = fs.readFileSync(`${process.env.FILE_ROOT}/${sql(`select StorageKey from StoredFiles where FileId='${jpg.body.id}'`)}`);
  check("JPEG metadata removed", jpg.status === 201 && !storedJpg.includes("GPS-SECRET") && storedJpg.includes("JFIF") && jpg.body.sizeBytes === storedJpg.length);
  check("API-01 customer cannot upload", (await customer.upload("files", "file", PNG, "a.png", form)).status === 403);

  // API-02
  const fid = up.body.id;
  const signed = await manager.api(`files/${fid}`);
  check("API-02 signed url", signed.status === 200 && signed.body.url.includes("sig="), signed.body?.url);
  const ttl = (new Date(signed.body.expiresAt) - Date.now()) / 60000;
  check("API-02 lifetime 5 min", ttl > 4.5 && ttl <= 5.01, ttl.toFixed(2));
  const dl = await manager.download(signed.body.url);
  check("API-02 download", dl.status === 200 && dl.size === storedPng.length && dl.type === "image/png" && dl.disposition?.includes("anh.png") && dl.nosniff === "nosniff", JSON.stringify(dl));
  check("API-02 customer owner allowed", (await customer.api(`files/${fid}`)).status === 200);
  const tampered = signed.body.url.replace(/sig=(.)/, (m, c) => `sig=${c === "A" ? "B" : "A"}`);
  check("API-02 tampered sig 403", (await manager.download(tampered)).status === 403);
  check("API-02 extended expiry 403", (await manager.download(signed.body.url.replace(/expires=\d+/, m => `expires=${Number(m.slice(8)) + 9999}`))).status === 403);
  const anon = await client(browser);
  check("API-02 anonymous 401", (await anon.api(`files/${fid}`)).status === 401);
  const admin = await client(browser);
  await admin.api("auth/login", "POST", { email: "admin@test.local", password: pw });
  check("API-02 admin denied", (await admin.api(`files/${fid}`)).status === 404);
  sql(`update WeddingProjects set ManagerId=1 where ProjectCode='${id}'`);
  check("API-02 other manager denied", (await manager.api(`files/${fid}`)).status === 404);
  check("API-01 other manager cannot upload", (await manager.upload("files", "file", PNG, "a.png", form)).status === 404);
  sql(`update WeddingProjects set ManagerId=2 where ProjectCode='${id}'`);

  // NRD receipt through the file service
  const bankDate = new Date(new Date(process.env.EVENT_DATE).getTime() + 86400000).toISOString().slice(0, 10);
  const bank = await manager.api("projects", "POST", { ...base, coupleName: "Khách Bank", customerEmail: `b${Date.now()}@example.com`, eventDate: bankDate, paymentMethod: "Bank" });
  const bankId = bank.body.project.id;
  check("NRD rejects fake receipt", (await manager.upload(`projects/${bankId}/nrd-receipt`, "receipt", [9, 9, 9, 9], "x.pdf")).status === 400);
  const receipt = await manager.upload(`projects/${bankId}/nrd-receipt`, "receipt", PDF, "bien-nhan.pdf");
  check("NRD receipt stored", receipt.status === 200 && receipt.body.fileId, JSON.stringify(receipt.body));
  check("NRD links file id", sql(`select count(*) from Payments p join StoredFiles f on p.ReceiptFilePath=convert(nvarchar(36),f.FileId) where f.Purpose='NrdReceipt' and p.Status='Paid'`) === "1");

  // GBR-01 trigger
  let triggerError = "";
  try { sql(`update s set Quantity=99 from SpecItems s join WeddingProjects w on w.ProjectId=s.ProjectId where w.ProjectCode='${id}'`); } catch (e) { triggerError = String(e.stdout || e.message); }
  check("DB trigger blocks baseline edit", triggerError.includes("frozen"), triggerError.slice(0, 80));
  check("baseline row unchanged", sql(`select count(*) from SpecItems s join WeddingProjects w on w.ProjectId=s.ProjectId where w.ProjectCode='${id}' and s.Quantity=99`) === "0");
  check("DB trigger blocks baseline delete", (() => { try { sql(`delete s from SpecItems s join WeddingProjects w on w.ProjectId=s.ProjectId where w.ProjectCode='${id}'`); return false; } catch { return true; } })());

  // MSG27 resend
  const bankEmail = sql(`select u.Email from Users u join WeddingProjects w on w.CustomerId=u.UserId where w.ProjectCode='${bankId}'`);
  sql(`update EmailOutbox set Status='Failed' where RelatedProjectId=(select ProjectId from WeddingProjects where ProjectCode='${bankId}') and Kind='Credentials'`);
  await manager.page.goto(`${BASE}/M04?id=${bankId}`, { waitUntil: "domcontentloaded" });
  await manager.page.waitForFunction(() => window.wpsm?.project);
  check("MSG27 banner on hub", !!(await manager.page.$("#credentialAlert")));
  await manager.page.$eval('[data-action="resend-credentials"]', b => b.click());
  await sleep(800);
  check("MSG27 resend queues new email", !(await manager.page.$("#credentialAlert")) && sql(`select count(*) from EmailOutbox where ToEmail='${bankEmail}' and Status='Queued'`) === "1");
  check("resend refused after activation", (await manager.api(`projects/${id}/resend-credentials`, "POST", {})).status === 400);
  sql(`update WeddingProjects set ManagerId=1 where ProjectCode='${bankId}'`);
  check("resend blocked for other manager", (await manager.api(`projects/${bankId}/resend-credentials`, "POST", {})).status === 404);
  sql(`update WeddingProjects set ManagerId=2 where ProjectCode='${bankId}'`);

  // JOB-07
  sql("insert EmailOutbox(ToEmail,Subject,BodyHtml,Kind,NextAttemptAt) values ('khong-hop-le','x','secret-body','Credentials',sysutcdatetime())");
  let sent = "0";
  for (let i = 0; i < 16 && Number(sent) < 3; i++) { await sleep(5000); sent = sql("select count(*) from EmailOutbox where Status='Sent'"); }
  check("JOB-07 sent all valid mail", Number(sent) >= 3, sent);
  const eml = fs.existsSync(process.env.MAIL_DIR) ? fs.readdirSync(process.env.MAIL_DIR).filter(f => f.endsWith(".eml")) : [];
  check("API-03 message written", eml.length === Number(sent), String(eml.length));
  check("credentials body redacted after send", !sql("select BodyHtml from EmailOutbox where Kind='Credentials' and Status='Sent' and ToEmail='" + mail + "'").includes(tempPw));
  const retry = sql("select concat(Status,'/',RetryCount,'/',datediff(second,sysutcdatetime(),NextAttemptAt)) from EmailOutbox where ToEmail='khong-hop-le'").split("/");
  check("JOB-07 first failure schedules +5 min", retry[0] === "Queued" && retry[1] === "1" && Number(retry[2]) > 200 && Number(retry[2]) <= 300, retry.join("/"));
  sql("update EmailOutbox set RetryCount=3, NextAttemptAt=sysutcdatetime() where ToEmail='khong-hop-le'");
  let failed = "";
  for (let i = 0; i < 16 && !failed.startsWith("Failed"); i++) { await sleep(5000); failed = sql("select concat(Status,'/',RetryCount,'/',BodyHtml) from EmailOutbox where ToEmail='khong-hop-le'"); }
  check("JOB-07 gives up after 3 retries", failed.startsWith("Failed/4/") && !failed.includes("secret-body"), failed);

  // API-04
  const messageId = sql(`select top 1 ProviderMessageId from EmailOutbox where Kind='Reminder'`);
  const hook = async (obj, secret = process.env.HOOK_SECRET) => {
    const body = JSON.stringify(obj);
    const sig = crypto.createHmac("sha256", secret).update(body).digest("hex");
    return (await fetch(`${BASE}/api/wpsm/notifications/email-status`, { method: "POST", headers: { "Content-Type": "application/json", "X-Wpsm-Signature": sig }, body })).status;
  };
  check("API-04 bad signature 401", (await hook({ messageId, status: "Delivered" }, "wrong")) === 401);
  check("API-04 not updated by bad signature", sql(`select count(*) from EmailOutbox where DeliveredAt is not null`) === "0");
  check("API-04 delivered", (await hook({ messageId, status: "Delivered" })) === 204);
  check("API-04 delivered saved", sql(`select count(*) from EmailOutbox where DeliveredAt is not null and ProviderMessageId='${messageId}'`) === "1");
  check("API-04 unknown id 404", (await hook({ messageId: "nope", status: "Delivered" })) === 404);
  check("API-04 failed", (await hook({ messageId, status: "Failed", error: "mailbox full" })) === 204 && sql(`select concat(Status,'/',LastError) from EmailOutbox where ProviderMessageId='${messageId}'`) === "Failed/mailbox full");
  check("API-04 bad status 400", (await hook({ messageId, status: "Weird" })) === 400);

  await browser.close();
  console.log(`${results.filter(Boolean).length}/${results.length} passed`);
  process.exit(results.every(Boolean) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
