// API paths added with the Report 4 use cases (G-04, M-01..M-23, C-05..C-16, A-02..A-08 extensions).
const seg = encodeURIComponent;
const project = (code) => `/projects/${seg(code)}`;
const cr = (code, crCode) => `${project(code)}/change-requests/${seg(crCode)}`;
const complaint = (code, complaintCode) => `${project(code)}/complaints/${seg(complaintCode)}`;

export const OPS = {
  notifications: "/notifications",
  notificationRead: (id) => `/notifications/${id}/read`,
  notificationsReadAll: "/notifications/read-all",

  managerWork: "/manager/work",
  managerOverdue: "/manager/overdue-alerts",
  roadmapTasks: (code) => `${project(code)}/roadmap/tasks`,
  completeRoadmapTask: (code, taskId) => `${project(code)}/roadmap/tasks/${taskId}/complete`,

  changeRequests: (code) => `${project(code)}/change-requests`,
  crSpecItems: (code) => `${project(code)}/change-requests/spec-items`,
  crPreview: (code) => `${project(code)}/change-requests/preview`,
  cr,
  crSubmit: (code, crCode) => `${cr(code, crCode)}/submit`,
  crItems: (code, crCode) => `${cr(code, crCode)}/items`,
  crApprove: (code, crCode) => `${cr(code, crCode)}/approve`,
  crReject: (code, crCode) => `${cr(code, crCode)}/reject`,
  crComment: (code, crCode) => `${cr(code, crCode)}/comments`,
  crException: (code, crCode) => `${cr(code, crCode)}/exception-requests`,
  crAddendum: (code, crCode) => `${cr(code, crCode)}/addendum`,
  crSignedCopy: (code, crCode) => `${cr(code, crCode)}/addendum/signed-copy`,

  beo: (code) => `${project(code)}/beo`,
  beoNotes: (code) => `${project(code)}/beo/notes`,
  beoFreeze: (code) => `${project(code)}/beo/freeze`,
  beoReapprove: (code) => `${project(code)}/beo/reapprove`,

  dayOf: (code) => `${project(code)}/day-of`,
  dayOfLoad: (code) => `${project(code)}/day-of/load`,
  dayOfComplete: (code, taskId) => `${project(code)}/day-of/${taskId}/complete`,

  payments: (code) => `${project(code)}/payments`,
  settlementReceipt: (code) => `${project(code)}/payments/settlement-receipt`,

  proofPhotos: (code) => `${project(code)}/proof-photos`,
  postEvent: (code) => `${project(code)}/post-event`,
  ratings: (code) => `${project(code)}/ratings`,
  complaints: (code) => `${project(code)}/complaints`,
  complaint,
  complaintResolve: (code, complaintCode) => `${complaint(code, complaintCode)}/resolve`,
  complaintEscalate: (code, complaintCode) => `${complaint(code, complaintCode)}/escalate`,

  resourceCalendar: "/resources/calendar",
  allocations: "/resource-allocations",
  allocation: (id) => `/resource-allocations/${id}`,
  allocationConflict: (id) => `/resource-allocations/${id}/conflict`,
  allocationException: "/resource-allocations/exception-requests",

  catalogItem: (id) => `/catalog/${id}`,
  catalogStatus: (id) => `/catalog/${id}/status`,
  priceFrameHistory: (itemId) => `/price-frames/history/${itemId}`,
  penaltyRules: "/policies/penalty-rules",
};
