// Services still used by pages: day-of photos, resources, admin configuration.
// Notifications, Manager dashboard, BEO, payments and feedback have their own services.
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

// M-16, M-17 (UC-30, UC-31)
export const dayOfService = {
  get: (code) => axiosClient.get(OPS.dayOf(code)).then(data),
  load: (code) => axiosClient.post(OPS.dayOfLoad(code)).then(data),
  photos: (code) => axiosClient.get(OPS.proofPhotos(code)).then(data),
  complete: (code, taskId, photos) => axiosClient.post(OPS.dayOfComplete(code, taskId), form({ photos })).then(data),
  addPhotos: (code, taskId, photos) => axiosClient.post(OPS.dayOfPhotos(code, taskId), form({ photos })).then(data),
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

// A-02, A-04 (UC-37, UC-38)
export const configurationAdminService = {
  updateCatalogItem: (id, item) => axiosClient.put(OPS.catalogItem(id), item).then(data),
  setCatalogStatus: (id, active) => axiosClient.post(OPS.catalogStatus(id), { active }).then(data),
  priceFrameHistory: (itemId) => axiosClient.get(OPS.priceFrameHistory(itemId)).then(data),
};
