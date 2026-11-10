import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

// 404 here means the account has no wedding project yet; pages show that as an empty state, not an error.
const orNullOn404 = (request) => request.then((r) => r.data).catch((error) => {
  if (error.response?.status === 404) return null;
  throw error;
});

// C-01, C-03, C-11: the API resolves the customer's own project (GBR-12).
export const customerService = {
  getDashboard: () => orNullOn404(axiosClient.get(ENDPOINTS.customer.dashboard)),
  getRoadmap: () => orNullOn404(axiosClient.get(ENDPOINTS.customer.roadmap)),
  completeTask: (projectCode, taskId) => axiosClient.post(ENDPOINTS.customer.completeTask(projectCode, taskId)).then((r) => r.data),

  // C-06, C-07, C-08: Customer Change Request List, Create CR, Cost Impact & Decision
  getChangeRequests: (projectCode) =>
    orNullOn404(axiosClient.get(ENDPOINTS.customer.changeRequests, { params: projectCode ? { projectCode } : {} })),
  getChangeRequestContext: (projectCode) =>
    orNullOn404(axiosClient.get(ENDPOINTS.customer.changeRequestContext, { params: projectCode ? { projectCode } : {} })),
  createChangeRequest: (body) =>
    axiosClient.post(ENDPOINTS.customer.changeRequests, body).then((r) => r.data),
  getChangeRequest: (crCode, projectCode) =>
    orNullOn404(axiosClient.get(ENDPOINTS.customer.changeRequest(crCode), { params: projectCode ? { projectCode } : {} })),
  decideChangeRequest: (crCode, action, note) =>
    axiosClient.post(ENDPOINTS.customer.decideChangeRequest(crCode), { action, note }).then((r) => r.data),

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
