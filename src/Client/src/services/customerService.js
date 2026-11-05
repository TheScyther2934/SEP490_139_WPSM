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
};
