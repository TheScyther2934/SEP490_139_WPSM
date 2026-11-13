// React client (src/Client) served by the API on 5300: routes, role guards, session restore and the main flows,
// driven through the real forms. run.ps1 raises the sign-in rate limit because every suite shares one IP.
const puppeteer = require("puppeteer-core");
const BASE = process.env.REACT_BASE || "http://localhost:5300";
const PW = process.env.TEST_PW;
const EVENT_DATE = process.env.EVENT_DATE;
let bad = 0;
const check = (name, ok, detail = "") => { if (!ok) bad++; console.log(`${ok ? "PASS" : "FAIL"} ${name} ${detail}`); };
const path = (page) => new URL(page.url()).pathname;
const waitPath = (page, wanted, timeout = 8000) =>
  page.waitForFunction((p) => location.pathname === p, { timeout }, wanted).then(() => true).catch(() => false);
const waitText = (page, text, timeout = 8000) =>
  page.waitForFunction((t) => document.body.innerText.includes(t), { timeout }, text).then(() => true).catch(() => false);
const clickButton = async (page, label) => {
  const handle = await page.waitForSelector(`::-p-xpath(//button[normalize-space(.)="${label}"])`, { timeout: 8000 });
  await handle.click();
};
const typeInto = async (page, selector, text) => {
  await page.$eval(selector, (e) => { e.focus(); e.select(); });
  await page.keyboard.press("Backspace");
  await page.type(selector, text);
};
// MUI select: open, pick, then wait until the menu and its backdrop are gone, or the next click hits the backdrop.
// After a full load the session restore runs first, so wait for the select instead of clicking at once.
const chooseOption = async (page, selectSelector, optionSelector) => {
  await (await page.waitForSelector(selectSelector, { visible: true, timeout: 8000 })).click();
  await (await page.waitForSelector(optionSelector, { visible: true, timeout: 8000 })).click();
  await page.waitForFunction(() => !document.querySelector('[role="listbox"]'), { timeout: 8000 });
};
// Screenshots for a visual check, only when SHOTS_DIR is set.
const shot = async (page, name) => { if (process.env.SHOTS_DIR) await page.screenshot({ path: `${process.env.SHOTS_DIR}/${name}.png`, fullPage: true }); };
const login = async (page, email, password) => {
  await page.goto(`${BASE}/login`, { waitUntil: "networkidle2" });
  await page.type("#email", email);
  await page.type("#password", password);
  await clickButton(page, "Đăng nhập");
};

(async () => {
  const browser = await puppeteer.launch({ headless: "new", executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
  const errors = [];

  // ── Guards without a session ──────────────────────────────────────────────
  {
    const ctx = await browser.createBrowserContext();
    const page = await ctx.newPage();
    page.on("pageerror", (e) => errors.push(e.message));
    await page.goto(`${BASE}/admin/dashboard`, { waitUntil: "networkidle2" });
    check("anonymous /admin/dashboard redirects to /login", await waitPath(page, "/login"));
    await page.goto(`${BASE}/khong-ton-tai`, { waitUntil: "networkidle2" });
    check("unknown URL shows 404 page", await waitText(page, "Không tìm thấy trang"));
    await page.goto(`${BASE}/login`, { waitUntil: "networkidle2" });
    await clickButton(page, "Đăng nhập");
    check("empty login shows MSG02", await waitText(page, "Trường Email là bắt buộc."));
    await ctx.close();
  }

  // ── Admin ─────────────────────────────────────────────────────────────────
  {
    const ctx = await browser.createBrowserContext();
    const page = await ctx.newPage();
    page.on("pageerror", (e) => errors.push(e.message));
    await login(page, "admin@test.local", "wrong-password-1");
    check("wrong password shows generic error (GBR-11)", await waitText(page, "Email hoặc mật khẩu không đúng"));
    check("wrong password stays on /login", path(page) === "/login");

    await page.goto(`${BASE}/login`, { waitUntil: "networkidle2" });
    await login(page, "admin@test.local", PW);
    check("admin lands on /admin/dashboard", await waitPath(page, "/admin/dashboard"), path(page));
    check("A-01 shows KPI", await waitText(page, "Công suất sảnh tháng này"));

    // ADMIN-UI: one horizontal bar instead of the sidebar on desktop; the drawer only opens from the menu button.
    await page.setViewport({ width: 1440, height: 900 });
    const nav = await page.$$eval('header nav[aria-label="Điều hướng chính"] a', (links) => links.map((a) => ({ text: a.innerText.trim(), active: a.classList.contains("active") })));
    check("admin top bar lists the 9 admin screens", nav.length === 9, JSON.stringify(nav.map((n) => n.text)));
    check("top bar marks the current screen", nav.find((n) => n.active)?.text === "Tổng quan", JSON.stringify(nav.filter((n) => n.active)));
    check("admin has no permanent sidebar", !(await page.$(".MuiDrawer-docked")));
    check("A-01 has six KPI cards and the overdue strip", await page.$$eval("#a01Kpis > *", (c) => c.length) === 6 && !!(await page.$("#a01Overdue")));
    await shot(page, "a01-dashboard");
    await page.click('header nav a[href="/admin/catalog"]');
    check("top bar link opens A-02", await waitPath(page, "/admin/catalog"));

    const range = "from=2026-01-01&to=2026-12-31";
    await page.goto(`${BASE}/admin/reports/hall-utilization?${range}`, { waitUntil: "networkidle2" });
    check("F5 on a deep link keeps the session (refresh cookie)", await waitText(page, "Công suất theo sảnh"), path(page));
    check("report filter read from the query string", await page.$eval("#reportFrom", (e) => e.value) === "2026-01-01");
    await page.click(`a[href="/admin/reports/net-revenue?${range}"]`);
    check("report tab keeps the date range", await waitPath(page, "/admin/reports/net-revenue") && new URL(page.url()).search === `?${range}`, page.url());

    await page.goto(`${BASE}/admin/catalog`, { waitUntil: "networkidle2" });
    await page.waitForSelector("#catalogTable");
    check("A-02 table has the code column", (await page.$eval("#catalogTable thead", (h) => h.innerText)).includes("Mã"));
    check("A-02 shows the row range", await waitText(page, "trên"));
    await shot(page, "a02-catalog");
    await page.type('input[aria-label="Tìm dịch vụ"]', "khong-co-dich-vu-nay");
    check("A-02 search goes to the query string", await page.waitForFunction(() => new URL(location.href).searchParams.get("q") === "khong-co-dich-vu-nay", { timeout: 8000 }).then(() => true).catch(() => false), page.url());
    check("A-02 search with no match shows the empty row", await waitText(page, "Không có dịch vụ phù hợp."));
    await page.reload({ waitUntil: "networkidle2" });
    check("F5 keeps the A-02 search", await page.$eval('input[aria-label="Tìm dịch vụ"]', (e) => e.value) === "khong-co-dich-vu-nay");
    await page.goto(`${BASE}/admin/catalog`, { waitUntil: "networkidle2" });
    await chooseOption(page, "#catalogStatusFilter", 'li[data-value="active"]');
    check("A-02 status filter goes to the query string", new URL(page.url()).searchParams.get("status") === "active", page.url());
    await clickButton(page, "Thêm dịch vụ");
    await clickButton(page, "Lưu");
    check("A-03 validates required fields", await waitText(page, "Trường Tên dịch vụ là bắt buộc."));
    check("A-03 status is a radio pair", (await page.$$('[role="dialog"] input[type="radio"]')).length === 2);
    await shot(page, "a03-dialog");
    await clickButton(page, "Hủy");

    await page.goto(`${BASE}/admin/policies`, { waitUntil: "networkidle2" });
    check("A-06 shows policies from the API", await waitText(page, "Tỷ lệ các đợt thanh toán"));
    check("A-06 penalty rules are a table", !!(await page.waitForSelector("#penaltyCard table", { timeout: 8000 }).catch(() => null)));
    await shot(page, "a06-policies");
    await page.goto(`${BASE}/admin/price-frames`, { waitUntil: "networkidle2" });
    await shot(page, "a04-price-frames");
    await page.goto(`${BASE}/admin/reports/hall-utilization?${range}`, { waitUntil: "networkidle2" });
    await shot(page, "a10-hall-utilization");

    // Narrow screens: the bar hides its links behind the menu button.
    await page.setViewport({ width: 800, height: 900 });
    check("narrow admin screen hides the top links", await page.$eval('header nav[aria-label="Điều hướng chính"]', (n) => getComputedStyle(n).display === "none"));
    await page.click('button[aria-label="Mở menu"]');
    await (await page.waitForSelector('.MuiDrawer-root a[href="/admin/policies"]', { visible: true, timeout: 8000 })).click();
    check("drawer link opens A-06", await waitPath(page, "/admin/policies"));
    await shot(page, "a06-policies-narrow");

    await page.goto(`${BASE}/manager/dashboard`, { waitUntil: "networkidle2" });
    check("admin on a manager URL gets /403", await waitPath(page, "/403"), path(page));
    await ctx.close();
  }

  // ── Manager: create a project through M-03 ─────────────────────────────────
  let projectCode = null, customerEmail = `react${Date.now()}@example.com`, tempPassword = null;
  {
    const ctx = await browser.createBrowserContext();
    const page = await ctx.newPage();
    page.on("pageerror", (e) => errors.push(e.message));
    await login(page, "manager@test.local", PW);
    check("manager lands on /manager/dashboard (M-01)", await waitPath(page, "/manager/dashboard"), path(page));

    await page.goto(`${BASE}/manager/projects/new`, { waitUntil: "networkidle2" });
    await clickButton(page, "Xác nhận & tạo dự án");
    check("M-03 blocks an empty form", await waitText(page, "Trường Họ và tên là bắt buộc."));

    await page.type("#coupleName", "Khách React");
    await page.type("#customerEmail", customerEmail);
    await page.type("#customerPhone", "0900000009");
    await page.$eval("#eventDate", (el, value) => {
      // React tracks the value through the native setter; typing into a date input depends on the OS locale.
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value").set.call(el, value);
      el.dispatchEvent(new Event("input", { bubbles: true }));
    }, EVENT_DATE);
    await page.waitForFunction(() => !document.querySelector("#hallSelect")?.getAttribute("aria-disabled"), { timeout: 8000 }).catch(() => {});
    await chooseOption(page, "#hallSelect", 'li[role="option"]');
    const slot = await page.waitForSelector('input[type="radio"]:not([disabled])', { timeout: 8000 }).catch(() => null);
    check("M-03 shows a free slot for the chosen hall and date", !!slot);
    if (slot) await slot.click();
    await page.type("#baseValue", "100000000");
    await page.type("#nrdAmount", "1000");
    await clickButton(page, "Xác nhận & tạo dự án");
    const fieldErrors = () => page.$$eval(".Mui-error, [role=alert]", (els) => [...new Set(els.map((e) => e.innerText.trim()).filter(Boolean))].join(" | "));
    check("M-03 rejects NRD below the policy minimum (MSG14)", await waitText(page, "NRD cần tối thiểu"), await fieldErrors());
    await typeInto(page, "#nrdAmount", "50000000");
    await chooseOption(page, "#nrdPaymentMethod", 'li[data-value="Cash"]');
    await clickButton(page, "Xác nhận & tạo dự án");
    const confirm = await page.waitForSelector("#createProjectConfirmSubmit", { timeout: 8000 }).catch(() => null);
    check("M-03 opens the confirmation once the form is valid", !!confirm, confirm ? "" : await fieldErrors());
    if (confirm) await confirm.click();
    const created = await page.waitForSelector("#createProjectSuccess", { timeout: 15000 }).catch(() => null);
    check("M-03 creates the project", !!created);
    if (created) {
      const text = await page.$eval("#createProjectSuccess", (e) => e.innerText);
      projectCode = (text.match(/Đã tạo (\S+)/) || [])[1];
      tempPassword = await page.$eval("#createProjectSuccess code", (e) => e.textContent.trim()).catch(() => null);
      check("temporary password shown once (dev without SMTP)", !!tempPassword);
    }

    if (projectCode) {
      await page.goto(`${BASE}/manager/projects`, { waitUntil: "networkidle2" });
      await typeInto(page, "#search-input", projectCode);
      const found = await waitText(page, projectCode);
      const q = await page.waitForFunction(() => new URLSearchParams(location.search).get("q"), { timeout: 5000 }).then((h) => h.jsonValue()).catch(() => null);
      check("M-02 search finds the new project and keeps it on the URL", found && q === projectCode, `q=${q} url=${page.url()}`);

      await page.goto(`${BASE}/manager/projects/${projectCode}`, { waitUntil: "networkidle2" });
      check("M-04 opens by :projectCode", await waitText(page, "1. Hợp đồng gốc"));
      await page.click(`a[href="/manager/projects/${projectCode}/specification"][role="tab"]`);
      check("project tab opens M-05 by URL", await waitPath(page, `/manager/projects/${projectCode}/specification`));
      check("M-05 shows default specification rows (L-11)", !!(await page.waitForSelector("tr[data-spec-item-id]", { timeout: 8000 }).catch(() => null)));
      await clickButton(page, "Lưu nháp");
      check("M-05 saves the draft", await waitText(page, "Đã lưu đặc tả."));
      await page.goto(`${BASE}/manager/projects/${projectCode}/roadmap`, { waitUntil: "networkidle2" });
      check("M-06 before freeze explains the roadmap", await waitText(page, "Lộ trình được tạo sau khi baseline được đóng băng."));
    }
    await page.goto(`${BASE}/manager/projects/KHONG-CO`, { waitUntil: "networkidle2" });
    check("unknown project code shows an error, not a crash", await waitText(page, "Không tìm thấy dự án"));
    await page.goto(`${BASE}/customer/overview`, { waitUntil: "networkidle2" });
    check("manager on a customer URL gets /403", await waitPath(page, "/403"), path(page));

    // Sign out ends the session; protected URLs go back to /login.
    await page.goto(`${BASE}/manager/dashboard`, { waitUntil: "networkidle2" });
    await page.click('button[aria-label="Tài khoản"]');
    await (await page.waitForSelector('::-p-xpath(//li[normalize-space(.)="Đăng xuất"])')).click();
    check("sign out returns to /login", await waitPath(page, "/login"));
    await page.goto(`${BASE}/manager/projects`, { waitUntil: "networkidle2" });
    check("after sign out, F5 cannot restore the session", await waitPath(page, "/login"), path(page));
    await ctx.close();
  }

  // ── Customer: forced password change, then own pages ───────────────────────
  if (tempPassword) {
    const ctx = await browser.createBrowserContext();
    const page = await ctx.newPage();
    page.on("pageerror", (e) => errors.push(e.message));
    await login(page, customerEmail, tempPassword);
    check("temporary password forces /change-password", await waitPath(page, "/change-password"), path(page));
    await page.goto(`${BASE}/customer/overview`, { waitUntil: "networkidle2" });
    check("other routes redirect to /change-password", await waitPath(page, "/change-password"), path(page));

    await page.type("#currentPassword", tempPassword);
    await page.type("#newPassword", "khongcoso");
    await page.type("#confirmPassword", "khongcoso");
    await clickButton(page, "Đổi mật khẩu");
    check("G-03 requires a letter and a digit", await waitText(page, "ít nhất một chữ cái và một chữ số"));
    await typeInto(page, "#newPassword", "Cust0mer-React-2026");
    await typeInto(page, "#confirmPassword", "Cust0mer-React-2026");
    await clickButton(page, "Đổi mật khẩu");
    check("after the change the customer lands on /customer/overview", await waitPath(page, "/customer/overview", 10000), path(page));
    check("C-01 shows the countdown", !!(await page.waitForSelector("#countdown-label", { timeout: 8000 }).catch(() => null)));

    await page.goto(`${BASE}/customer/contract`, { waitUntil: "networkidle2" });
    check("C-02 shows the customer's own project", await waitText(page, `Hợp đồng ${projectCode}`));
    await page.goto(`${BASE}/customer/contract/addenda`, { waitUntil: "networkidle2" });
    check("C-09 without addenda shows the empty state", await waitText(page, "Chưa có phụ lục hợp đồng."));
    await page.goto(`${BASE}/customer/roadmap`, { waitUntil: "networkidle2" });
    check("C-03 before freeze explains the roadmap", await waitText(page, "Lộ trình sẽ được tạo sau khi quản lý chốt baseline."));
    await page.goto(`${BASE}/customer/change-requests/CR-1`, { waitUntil: "networkidle2" });
    check("C-07 with an unknown CR code shows not found", await waitText(page, "Không tìm thấy"));
    await page.goto(`${BASE}/admin/dashboard`, { waitUntil: "networkidle2" });
    check("customer on an admin URL gets /403", await waitPath(page, "/403"), path(page));
    await ctx.close();
  }

  check("no uncaught page errors", errors.length === 0, errors.slice(0, 3).join(" | "));
  await browser.close();
  console.log(`react client passed: ${bad === 0}`);
  process.exit(bad ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(2); });
