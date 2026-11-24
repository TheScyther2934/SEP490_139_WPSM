import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

export const managerService = {
  getDashboard: () => axiosClient.get(ENDPOINTS.manager.dashboard).then((r) => r.data),
  getOverdueAlerts: () => axiosClient.get(ENDPOINTS.manager.overdueAlerts).then((r) => r.data),
};
