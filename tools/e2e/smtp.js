// API on 5302 with Smtp:Host set (to an unreachable port): real-SMTP mode without sending anything.
// Checks that credentials travel only by email through the outbox (EMAIL-SMTP).
const { execSync } = require("child_process");
const sql = (q) => execSync(`sqlcmd -S "(localdb)\\MSSQLLocalDB" -d WPSM_E2E_Test -I -b -h -1 -W -f 65001 -Q "set nocount on; ${q}"`).toString().trim();
let bad = 0;
const check = (name, ok, detail = "") => { if (!ok) bad++; console.log(`${ok ? "PASS" : "FAIL"} ${name} ${detail}`); };

(async () => {
  const base = "http://localhost:5302/api/wpsm";
  const login = await fetch(base + "/auth/token", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: "manager@test.local", password: process.env.TEST_PW }) });
  const tokens = await login.json();
  if (!login.ok) throw new Error(`Fixture login failed (${login.status})`);
  const cookie = login.headers.get("set-cookie").split(";")[0];
  const email = "smtp" + Date.now() + "@example.com";
  const res = await fetch(base + "/projects", { method: "POST", headers: { "Content-Type": "application/json", Cookie: cookie, Authorization: `Bearer ${tokens.accessToken}` }, body: JSON.stringify({ coupleName: "SMTP", customerEmail: email, customerPhone: "0900000003", hall: "Sảnh Kim Cương", slot: "Lunch", eventDate: "2027-06-01", baseValue: 100000000, nrdAmount: 30000000, paymentMethod: "Cash" }) });
  const body = await res.json();
  check("password hidden when SMTP configured", res.status === 201 && body.temporaryPassword === null, res.status);
  check("credential email reported as pending", body.emailStatus === "Pending", body.emailStatus);

  const outbox = sql(`SELECT CONCAT(COUNT(*), '|', MAX(CASE WHEN BodyHtml LIKE '%/login%' AND BodyHtml NOT LIKE '%/G01%' THEN 1 ELSE 0 END)) FROM dbo.EmailOutbox WHERE ToEmail = '${email}' AND Kind = 'Credentials'`);
  check("one credential email queued in the outbox, linking to the React /login", outbox === "1|1", outbox);
  // The legacy EmailDeliveries queue was dropped: the outbox is the only path (no duplicate email).
  const legacy = sql("SELECT CASE WHEN OBJECT_ID(N'dbo.EmailDeliveries', N'U') IS NULL THEN 'dropped' ELSE 'exists' END");
  check("the old EmailDeliveries table is gone (outbox only)", legacy === "dropped", legacy);

  console.log(`smtp passed: ${bad === 0}`);
  process.exit(bad ? 1 : 0);
})().catch(e => { console.error(e); process.exit(2); });
