import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

export const managerService = {
  getDashboard: () => axiosClient.get(ENDPOINTS.manager.dashboard).then((r) => r.data),
  getOverdueAlerts: () => axiosClient.get(ENDPOINTS.manager.overdueAlerts).then((r) => r.data),
  getDayOfTasks: (code) => axiosClient.get(ENDPOINTS.manager.dayOfTasks(code)).then((r) => r.data),
  completeDayOfTask: (code, taskId, formData) =>
    axiosClient.post(ENDPOINTS.manager.completeDayOfTask(code, taskId), formData, {
      headers: { "Content-Type": "multipart/form-data" },
    }).then((r) => r.data),
  completeDayOfTaskDirect: (taskId, formData) =>
    axiosClient.post(ENDPOINTS.manager.completeDayOfTaskDirect(taskId), formData, {
      headers: { "Content-Type": "multipart/form-data" },
    }).then((r) => r.data),
};
