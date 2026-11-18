import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

const unwrap = (request) => request.then((r) => r.data);

// FE-07: M-21 calendar and allocation dialog, M-22 conflict detail.
// A save that breaks GBR-09 rejects with HTTP 409 and `error.response.data.conflict`.
export const resourceCalendarService = {
  getCalendar: ({ from, to, hallId }) => unwrap(axiosClient.get(ENDPOINTS.resourceCalendar, {
    params: { from, to, hallId: hallId || undefined },
  })),
  getResources: () => unwrap(axiosClient.get(ENDPOINTS.resources)),
  getAllocation: (allocationId) => unwrap(axiosClient.get(ENDPOINTS.resourceAllocation(allocationId))),
  create: (body) => unwrap(axiosClient.post(ENDPOINTS.resourceAllocations, body)),
  update: (allocationId, body) => unwrap(axiosClient.put(ENDPOINTS.resourceAllocation(allocationId), body)),
  release: (allocationId) => unwrap(axiosClient.delete(ENDPOINTS.resourceAllocation(allocationId))),
  requestException: (allocationId, note) => unwrap(axiosClient.post(ENDPOINTS.allocationException(allocationId), { note })),
};

/** The 409 body of a GBR-09 conflict, or null for any other error. */
export const conflictOf = (error) => error?.response?.status === 409 ? error.response.data?.conflict ?? null : null;
