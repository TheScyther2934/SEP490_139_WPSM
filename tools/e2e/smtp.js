(async () => {
  const base = "http://localhost:5302/api/wpsm";
  const login = await fetch(base + "/auth/token", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: "manager@test.local", password: process.env.TEST_PW }) });
  const tokens = await login.json();
  if (!login.ok) throw new Error(`Fixture login failed (${login.status})`);
  const cookie = login.headers.get("set-cookie").split(";")[0];
  const res = await fetch(base + "/projects", { method: "POST", headers: { "Content-Type": "application/json", Cookie: cookie, Authorization: `Bearer ${tokens.accessToken}` }, body: JSON.stringify({ coupleName: "SMTP", customerEmail: "smtp" + Date.now() + "@example.com", customerPhone: "0900000003", hall: "Sảnh Kim Cương", slot: "Lunch", eventDate: "2027-06-01", baseValue: 100000000, nrdAmount: 30000000, paymentMethod: "Cash" }) });
  const body = await res.json();
  const ok = res.status === 201 && body.temporaryPassword === null;
  console.log((ok ? "PASS" : "FAIL") + " password hidden when SMTP configured", res.status);
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
