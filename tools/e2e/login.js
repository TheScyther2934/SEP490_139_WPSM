// G-01: logs in through the real form, because the other suites call the login API directly.
const puppeteer = require("puppeteer-core");
const BASE = "http://localhost:5301";
let bad = 0;
const check = (name, ok, detail = "") => { if (!ok) bad++; console.log(`${ok ? "PASS" : "FAIL"} ${name} ${detail}`); };
const submit = async (page, email, password) => {
  await page.goto(`${BASE}/G01`, { waitUntil: "networkidle2" });
  await page.type("#email", email);
  await page.type("#password", password);
  await page.click('#loginForm button[type="submit"]');
};
(async () => {
  const browser = await puppeteer.launch({ headless: "new", executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
  const roles = [["admin@test.local", "/A01"], ["manager@test.local", "/M02"]];
  for (const [email, home] of roles) {
    const ctx = await browser.createBrowserContext();
    const page = await ctx.newPage();
    await submit(page, email, "wrong-password");
    await page.waitForFunction(() => !document.getElementById("errorNotificationArea").classList.contains("hidden"), { timeout: 5000 }).catch(() => {});
    check(`${email} wrong password stays on G01`, new URL(page.url()).pathname === "/G01");

    await submit(page, email, process.env.TEST_PW);
    await page.waitForFunction(path => location.pathname === path, { timeout: 5000 }, home).catch(() => {});
    const path = new URL(page.url()).pathname;
    const error = await page.$eval("#loginErrorMessage", e => e.textContent.trim()).catch(() => "");
    check(`${email} form login opens ${home}`, path === home, path === home ? "" : `at ${path}: ${error}`);
    await ctx.close();
  }
  await browser.close();
  console.log(`login form passed: ${bad === 0}`);
  process.exit(bad ? 1 : 0);
})().catch(e => { console.error(e); process.exit(2); });
