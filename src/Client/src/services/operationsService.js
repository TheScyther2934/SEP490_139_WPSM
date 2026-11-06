import axiosClient from "../api/axiosClient";
import { OPS } from "../api/operationsEndpoints";

const data = (r) => r.data;
const form = (fields) => {
  const body = new FormData();
  Object.entries(fields).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") return;
    if (Array.isArray(value)) value.forEach((v) => body.append(key, v));
    else body.append(key, value);
  });
  return body;
};

// G-04 (UC-03)
export const notificationService = {
  list: (unread) => axiosClient.get(OPS.notifications, { params: unread ? { unread: true } : {} }).then(data),
  markRead: (id) => axiosClient.post(OPS.notificationRead(id)),
  markAllRead: () => axiosClient.post(OPS.notificationsReadAll),
};

// M-01, M-06, M-23 (UC-21, UC-32)
export const managerService = {
  getWork: () => axiosClient.get(OPS.managerWork).then(data),
  getOverdue: () => axiosClient.get(OPS.managerOverdue).then(data),
  getRoadmap: (code) => axiosClient.get(OPS.roadmapTasks(code)).then(data),
  completeTask: (code, taskId) => axiosClient.post(OPS.completeRoadmapTask(code, taskId)),
};

// C-05..C-08, M-07..M-09 (UC-07..UC-11, UC-22..UC-24)
export const changeRequestService = {
  list: (code, status) => axiosClient.get(OPS.changeRequests(code), { params: status ? { status } : {} }).then(data),
  specItems: (code) => axiosClient.get(OPS.crSpecItems(code)).then(data),
  preview: (code, items) => axiosClient.post(OPS.crPreview(code), { items }).then(data),
  create: (code, body) => axiosClient.post(OPS.changeRequests(code), body).then(data),
  get: (code, crCode) => axiosClient.get(OPS.cr(code, crCode)).then(data),
  submit: (code, crCode) => axiosClient.post(OPS.crSubmit(code, crCode)).then(data),
  counterPropose: (code, crCode, items, note) => axiosClient.put(OPS.crItems(code, crCode), { items, note }).then(data),
  approve: (code, crCode) => axiosClient.post(OPS.crApprove(code, crCode)).then(data),
  reject: (code, crCode, reason) => axiosClient.post(OPS.crReject(code, crCode), { reason }).then(data),
  comment: (code, crCode, note) => axiosClient.post(OPS.crComment(code, crCode), { note }).then(data),
  requestException: (code, crCode, note) => axiosClient.post(OPS.crException(code, crCode), { note }).then(data),
  addendum: (code, crCode) => axiosClient.get(OPS.crAddendum(code, crCode)).then(data),
  uploadSignedCopy: (code, crCode, file) => axiosClient.post(OPS.crSignedCopy(code, crCode), form({ file })).then(data),
};

// M-10..M-12 (UC-27..UC-29)
export const beoService = {
  get: (code) => axiosClient.get(OPS.beo(code)).then(data),
  saveNotes: (code, note) => axiosClient.put(OPS.beoNotes(code), { note }).then(data),
  freeze: (code) => axiosClient.post(OPS.beoFreeze(code)).then(data),
  reapprove: (code) => axiosClient.post(OPS.beoReapprove(code)).then(data),
};

// M-16, M-17 (UC-30, UC-31)
export const dayOfService = {
  get: (code) => axiosClient.get(OPS.dayOf(code)).then(data),
  load: (code) => axiosClient.post(OPS.dayOfLoad(code)).then(data),
  photos: (code) => axiosClient.get(OPS.proofPhotos(code)).then(data),
  complete: (code, taskId, photos) => axiosClient.post(OPS.dayOfComplete(code, taskId), form({ photos })).then(data),
};

// C-10, M-13..M-15 (UC-12, UC-33, UC-34)
export const paymentService = {
  get: (code) => axiosClient.get(OPS.payments(code)).then(data),
  record: (code, payment) => axiosClient.post(OPS.payments(code), form(payment)).then(data),
  issueSettlementReceipt: (code) => axiosClient.post(OPS.settlementReceipt(code)).then(data),
};

// C-12..C-16, M-18..M-20 (UC-14..UC-16, UC-35, UC-36)
export const feedbackService = {
  postEvent: (code) => axiosClient.get(OPS.postEvent(code)).then(data),
  rate: (code, ratings) => axiosClient.post(OPS.ratings(code), { ratings }).then(data),
  complaints: (code) => axiosClient.get(OPS.complaints(code)).then(data),
  complaint: (code, complaintCode) => axiosClient.get(OPS.complaint(code, complaintCode)).then(data),
  fileComplaint: (code, body) => axiosClient.post(OPS.complaints(code), body).then(data),
  resolve: (code, complaintCode, response) => axiosClient.post(OPS.complaintResolve(code, complaintCode), { response }).then(data),
  escalate: (code, complaintCode, note) => axiosClient.post(OPS.complaintEscalate(code, complaintCode), { note }).then(data),
};

// M-21, M-22 (UC-25, UC-26). A conflict answers 409 with the clashing allocations in the body.
const allocationResult = (promise) => promise.then(data).catch((e) => {
  if (e.response?.status === 409) return e.response.data;
  throw e;
});
export const resourceService = {
  calendar: (params) => axiosClient.get(OPS.resourceCalendar, { params }).then(data),
  allocate: (body) => allocationResult(axiosClient.post(OPS.allocations, body)),
  update: (id, body) => allocationResult(axiosClient.put(OPS.allocation(id), body)),
  release: (id) => axiosClient.delete(OPS.allocation(id)),
  conflict: (id) => axiosClient.get(OPS.allocationConflict(id)).then(data),
  requestException: (body) => axiosClient.post(OPS.allocationException, body).then(data),
};

// A-02, A-04, A-08 (UC-37, UC-38, UC-40)
export const configurationAdminService = {
  updateCatalogItem: (id, item) => axiosClient.put(OPS.catalogItem(id), item).then(data),
  setCatalogStatus: (id, active) => axiosClient.post(OPS.catalogStatus(id), { active }).then(data),
  priceFrameHistory: (itemId) => axiosClient.get(OPS.priceFrameHistory(itemId)).then(data),
  penaltyRules: () => axiosClient.get(OPS.penaltyRules).then(data),
  updatePenaltyRules: (rules, effectiveFrom) => axiosClient.put(OPS.penaltyRules, { rules, effectiveFrom }).then(data),
};
