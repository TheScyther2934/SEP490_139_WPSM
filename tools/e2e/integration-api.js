// Integration test (Report 5, level IT): API + PostgreSQL through the use cases, no browser.
// Covers resource allocation and exceptions (UC-27, UC-28, UC-39, GBR-15) and ratings / complaints (UC-10, UC-11, UC-30, UC-38,
// GBR-16, GBR-17). Runs against an API with the development seed on a throwaway PostgreSQL database:
//   REACT_BASE (default http://localhost:5300), TEST_PW (password of admin@test.local and manager@test.local),
//   CUSTOMER_PW (password of the seeded customers, default Wedding2025!). The database needs one Audio resource of 10 (see run notes).
// Prints one line per case: "PASS|FAIL <case id> <title> ..." so the result can be copied into Report5_Test Report.xlsx.
const BASE = (process.env.REACT_BASE || "http://localhost:5300") + "/api/wpsm";
const PW = process.env.TEST_PW;
const CUSTOMER_PW = process.env.CUSTOMER_PW || "Wedding2025!";
let bad = 0;
const results = [];
const check = (id, name, ok, detail = "") => {
  if (!ok) bad++;
  results.push({ id, name, ok, detail });
  console.log(`${ok ? "PASS" : "FAIL"} ${id} ${name} ${detail}`);
};
async function call(method, path, token, body) {
  const r = await fetch(BASE + path, {
    method, headers: { "Content-Type": "application/json", ...(token ? { Authorization: "Bearer " + token } : {}) },
    body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(30000)
  });
  const text = await r.text(); let data; try { data = JSON.parse(text); } catch { data = text; }
  return { status: r.status, data, message: data?.message ?? "" };
}
const login = async (email, password = PW) => {
  const r = await call("POST", "/auth/token", null, { email, password });
  if (r.status !== 200) throw new Error(`login ${email}: ${r.status}`);
  return r.data.accessToken;
};

(async () => {
  if (!PW) throw new Error("TEST_PW is required");
  const m = await login("manager@test.local");
  const a = await login("admin@test.local");

  // ── Resources (UC-27, UC-28, UC-39) ────────────────────────────────────────────────────────────────────────
  const project = await call("GET", "/projects/WPSM-DEMO-02", m);
  const day = project.data.eventDate;           // a locked sample event; the window is on its day (Vietnam time 10:00–13:00)
  const at = (h) => new Date(`${day}T${String(h).padStart(2, "0")}:00:00+07:00`).toISOString();
  const body = (code, quantity, extra = {}) => ({ resourceId: 1, projectCode: code, startTime: at(10), endTime: at(13), quantity, ...extra });

  const a1 = await call("POST", "/resource-allocations", m, body("WPSM-DEMO-02", 8));
  check("IT-UC27-01", "Allocate 8 of 10 on a locked event", a1.status === 200, `${a1.status} ${a1.message}`);
  const over = await call("POST", "/resource-allocations", m, body("WPSM-DEMO-01", 5));
  check("IT-UC27-02", "Allocation over capacity returns the clash (MSG08, GBR-15)", over.status === 409 && /MSG08/.test(over.message), `${over.status} ${over.message}`);
  const noNote = await call("POST", "/resource-allocations/exception-requests", m, body("WPSM-DEMO-01", 5, { note: " " }));
  check("IT-UC28-01", "Exception request without a note is refused", noNote.status === 400, `${noNote.status} ${noNote.message}`);
  const ex = await call("POST", "/resource-allocations/exception-requests", m, body("WPSM-DEMO-01", 5, { note: "Need extra speakers" }));
  check("IT-UC28-02", "Exception request with a note is saved and waits for the Admin", ex.status === 200 && ex.data?.allocation?.exceptionStatus === "Pending", `${ex.status} ${ex.message}`);
  const fits = await call("POST", "/resource-allocations", m, body("WPSM-DEMO-01", 2));
  check("IT-UC27-03", "An allocation waiting for the Admin does not count against capacity", fits.status === 200, `${fits.status} ${fits.message}`);
  const editPending = await call("PUT", `/resource-allocations/${ex.data?.allocation?.allocationId}`, m, body("WPSM-DEMO-01", 1));
  check("IT-UC27-04", "An allocation waiting for the Admin cannot be rescheduled", editPending.status === 400, `${editPending.status} ${editPending.message}`);
  const tooMany = await call("PUT", `/resource-allocations/${a1.data?.allocation?.allocationId}`, m, body("WPSM-DEMO-02", 9));
  check("IT-UC27-05", "Rescheduling over capacity is refused with the clash", tooMany.status === 409, `${tooMany.status}`);
  const moved = await call("PUT", `/resource-allocations/${a1.data?.allocation?.allocationId}`, m, body("WPSM-DEMO-02", 7, { startTime: at(11) }));
  check("IT-UC27-06", "Rescheduling within capacity is saved", moved.status === 200, `${moved.status} ${moved.message}`);
  const settled = await call("POST", "/resource-allocations", m, body("WPSM-2025-011", 1));
  check("IT-UC27-07", "A settled event takes no allocation", settled.status === 400, `${settled.status} ${settled.message}`);
  const queue = await call("GET", "/admin/exception-requests", a);
  const req = (queue.data?.items || []).find((i) => i.status === "Pending");
  check("IT-UC39-01", "The Admin sees the pending request with its shortage", queue.status === 200 && !!req && req.resource?.shortage > 0, `${queue.status} shortage ${req?.resource?.shortage}`);
  const noDecision = await call("POST", `/admin/exception-requests/${req?.id}/decision`, a, { approve: true, note: "" });
  check("IT-UC39-02", "A decision without a note is refused", noDecision.status === 400, `${noDecision.status} ${noDecision.message}`);
  const approve = await call("POST", `/admin/exception-requests/${req?.id}/decision`, a, { approve: true, note: "Approved for the ceremony" });
  check("IT-UC39-03", "The Admin approves the exception", approve.status === 204, `${approve.status}`);
  const twice = await call("POST", `/admin/exception-requests/${req?.id}/decision`, a, { approve: false, note: "Changed mind" });
  check("IT-UC39-04", "A request is decided only once", twice.status === 400, `${twice.status} ${twice.message}`);
  const release = await call("DELETE", `/resource-allocations/${fits.data?.allocation?.allocationId}`, m);
  check("IT-UC27-08", "The operator releases an allocation", release.status === 204, `${release.status}`);
  const otherScope = await call("GET", `/resource-allocations/${a1.data?.allocation?.allocationId}/conflict`, a);
  check("IT-SEC-01", "Resource endpoints are refused to the Admin role (Manager only)", otherScope.status === 403, `${otherScope.status}`);

  // ── Ratings and complaints (UC-10, UC-11, UC-30, UC-38) ────────────────────────────────────────────────────
  const four = [{ module: "Hall", stars: 5, comment: "Lovely" }, { module: "Menu", stars: 4 }, { module: "Decor", stars: 5 }, { module: "MC", stars: 4 }];
  const held = await login("khoaanh.wedding@gmail.com", CUSTOMER_PW);          // WPSM-DEMO-03: held, not settled
  const summary = await call("GET", "/customer/post-event?projectCode=WPSM-DEMO-03", held);
  check("IT-UC10-01", "Before the settlement the post-event summary says rating is not open", summary.status === 200 && summary.data.canRate === false, `canRate=${summary.data?.canRate}`);
  const early = await call("POST", "/customer/ratings", held, { projectCode: "WPSM-DEMO-03", ratings: four });
  check("IT-UC10-02", "Rating before the settlement is refused (GBR-16)", early.status === 400, `${early.status} ${early.message}`);
  const empty = await call("POST", "/customer/complaints", held, { projectCode: "WPSM-DEMO-03", description: " " });
  check("IT-UC11-01", "A complaint without a description is refused (MSG25)", empty.status === 400 && /MSG25/.test(empty.message), `${empty.status} ${empty.message}`);
  const filed = await call("POST", "/customer/complaints", held, { projectCode: "WPSM-DEMO-03", description: "The sound cut out during the ceremony" });
  check("IT-UC11-02", "A complaint after the event is filed and numbered", filed.status === 201 && /^CMP-/.test(filed.data?.complaintCode || ""), `${filed.status} ${filed.data?.complaintCode}`);
  const wrongSheet = await call("POST", "/customer/complaints", held, { projectCode: "WPSM-DEMO-03", beoId: 999999, description: "Wrong sheet" });
  check("IT-UC11-03", "A complaint on a sheet of another event is refused", wrongSheet.status === 400, `${wrongSheet.status} ${wrongSheet.message}`);
  const locked = await login("thinhtran.cuoi@gmail.com", CUSTOMER_PW);          // WPSM-DEMO-02: locked, not held
  const before = await call("POST", "/customer/complaints", locked, { projectCode: "WPSM-DEMO-02", description: "Not held yet" });
  check("IT-UC11-04", "A complaint before the event is refused (GBR-17)", before.status === 400, `${before.status} ${before.message}`);
  const paid = await login("baolinh2025@outlook.com", CUSTOMER_PW);             // WPSM-2025-011: settled
  const rate = await call("POST", "/customer/ratings", paid, { projectCode: "WPSM-2025-011", ratings: four });
  check("IT-UC10-03", "Rating after the settlement is saved", rate.status === 200 && rate.data?.isLocked === true, `${rate.status} ${rate.message}`);
  const rateTwice = await call("POST", "/customer/ratings", paid, { projectCode: "WPSM-2025-011", ratings: four });
  check("IT-UC10-04", "Ratings cannot be submitted twice (MSG26)", rateTwice.status === 400 && /MSG26/.test(rateTwice.message), `${rateTwice.status} ${rateTwice.message}`);
  const code = filed.data?.complaintCode;
  const badAction = await call("POST", `/manager/complaints/${code}/action`, m, { action: "Close", note: "x" });
  check("IT-UC30-01", "An unknown complaint action is refused", badAction.status === 400, `${badAction.status} ${badAction.message}`);
  const esc = await call("POST", `/manager/complaints/${code}/action`, m, { action: "Escalate", note: "Customer asks for a refund" });
  check("IT-UC30-02", "The operator escalates the complaint to the Admin", esc.status === 200 && esc.data?.status === "Escalated", `${esc.status} ${esc.data?.status}`);
  const selfResolve = await call("POST", `/manager/complaints/${code}/action`, m, { action: "Resolve", note: "Handled" });
  check("IT-UC30-03", "An escalated complaint is no longer the operator's to resolve", selfResolve.status === 400, `${selfResolve.status} ${selfResolve.message}`);
  const noText = await call("POST", `/admin/complaints/${filed.data?.complaintId}/resolve`, a, { decision: "" });
  check("IT-UC38-01", "An Admin decision without text is refused", noText.status === 400, `${noText.status} ${noText.message}`);
  const resolve = await call("POST", `/admin/complaints/${filed.data?.complaintId}/resolve`, a, { decision: "Refund 10% of the sound service" });
  check("IT-UC38-02", "The Admin resolves the escalated complaint", resolve.status === 204, `${resolve.status}`);
  const after = await call("GET", `/customer/complaints/${code}`, held);
  check("IT-UC12-01", "The customer sees the complaint resolved by the Admin", after.status === 200 && after.data?.status === "Resolved" && after.data?.resolvedByName === "Admin", `${after.data?.status} ${after.data?.resolvedByName}`);
  const foreign = await call("GET", `/customer/complaints/${code}`, paid);
  check("IT-SEC-02", "A customer cannot read another customer's complaint (GBR-12)", foreign.status === 404, `${foreign.status}`);

  console.log(`${results.length - bad}/${results.length} passed`);
  process.exit(bad ? 1 : 0);
})().catch((e) => { console.error("FAIL run", e.message); process.exit(1); });
