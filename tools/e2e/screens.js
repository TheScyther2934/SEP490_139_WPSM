const puppeteer = require("puppeteer-core");
const { execSync } = require("child_process");
const BASE = "http://localhost:5301";
const sql = q => execSync(`sqlcmd -S "(localdb)\\MSSQLLocalDB" -d WPSM_E2E_Test -I -b -h -1 -W -Q "set nocount on; ${q}"`).toString().trim();
let bad = 0;
(async () => {
  const browser = await puppeteer.launch({ headless: "new", executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
  const customerEmail = sql("select top 1 Email from Users where Role='Customer' and MustChangePassword=0 order by UserId");
  const roles = [
    ["admin@test.local", process.env.TEST_PW, ["/A02", "/A04", "/A06", "/G02", "/G03"]],
    ["manager@test.local", process.env.TEST_PW, ["/M02", "/M03", "/M04", "/M05", "/G02", "/G03"]],
    [customerEmail, "Cust0mer-Pass-2026!", ["/C01", "/C02", "/C03", "/C09", "/G02", "/G03"]]
  ];
  for (const [email, password, routes] of roles) {
    const ctx = await browser.createBrowserContext();
    const page = await ctx.newPage();
    const issues = [];
    page.on("pageerror", e => issues.push("JS: " + e.message));
    page.on("response", r => { if (r.url().startsWith(BASE) && r.status() >= 400) issues.push(`HTTP ${r.status()} ${r.url().replace(BASE, "")}`); });
    await page.goto(`${BASE}/G01`, { waitUntil: "domcontentloaded" });
    const login = await page.evaluate(async (email, password) => (await fetch("/api/wpsm/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, password }) })).status, email, password);
    console.log(`${email}: login ${login}`);
    for (const route of routes) {
      issues.length = 0;
      const res = await page.goto(BASE + route, { waitUntil: "networkidle2", timeout: 30000 }).catch(e => ({ status: () => e.message }));
      await new Promise(r => setTimeout(r, 300));
      const status = await page.$eval("#wpsm-api-status", e => e.textContent.trim()).catch(() => "no status");
      const failed = res.status() !== 200 || issues.length || /Không kết nối/.test(status);
      if (failed) bad++;
      console.log(`${failed ? "FAIL" : "PASS"} ${route} ${res.status()} [${status}] ${issues.join(" | ")}`);
    }
    await ctx.close();
  }
  await browser.close();
  console.log(`sweep issues: ${bad}`);
  process.exit(bad ? 1 : 0);
})().catch(e => { console.error(e); process.exit(2); });
