import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

// 404 here means the account has no event yet; pages show that as an empty state, not an error.
const orNullOn404 = (request) => request.then((r) => r.data).catch((error) => {
  if (error.response?.status === 404) return null;
  throw error;
});

// The API resolves the customer's own events.
export const customerService = {
  getDashboard: () => orNullOn404(axiosClient.get(ENDPOINTS.customer.dashboard)),
  // The booked event (read-only once locked), addon contracts to confirm, who to contact.
  getEvents: () => axiosClient.get(ENDPOINTS.customer.events).then((r) => r.data),
  getEvent: (code) => orNullOn404(axiosClient.get(ENDPOINTS.customer.event, { params: code ? { code } : {} })),
  confirmAddon: (addonCode) => axiosClient.post(ENDPOINTS.customer.confirmAddon(addonCode)).then((r) => r.data),


  // C-12, C-13: Post-event & Rate Services
  getPostEvent: (projectCode) =>
    orNullOn404(axiosClient.get(ENDPOINTS.customer.postEvent, { params: projectCode ? { projectCode } : {} })),
  submitRatings: (projectCode, ratings) =>
    axiosClient.post(ENDPOINTS.customer.ratings, { projectCode, ratings }).then((r) => r.data),

  // C-14, C-15, C-16: Customer Complaints
  getComplaints: (projectCode) =>
    axiosClient.get(ENDPOINTS.customer.complaints, { params: projectCode ? { projectCode } : {} }).then((r) => r.data),
  getBeoOptions: (projectCode) =>
    axiosClient.get(ENDPOINTS.customer.complaintBeoOptions, { params: projectCode ? { projectCode } : {} }).then((r) => r.data),
  createComplaint: (body) => axiosClient.post(ENDPOINTS.customer.complaints, body).then((r) => r.data),
  getComplaint: (complaintCode) => orNullOn404(axiosClient.get(ENDPOINTS.customer.complaint(complaintCode))),
};
