import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

// M-02..M-05 and the shared portal snapshot.
export const projectService = {
  getProjects: () => axiosClient.get(ENDPOINTS.projects).then((r) => r.data),
  getProject: (code) => axiosClient.get(ENDPOINTS.project(code)).then((r) => r.data),
  createProject: (body) => axiosClient.post(ENDPOINTS.projects, body).then((r) => r.data),
  uploadNrdReceipt: (code, file) => {
    const form = new FormData();
    form.append("receipt", file);
    return axiosClient.post(ENDPOINTS.nrdReceipt(code), form).then((r) => r.data);
  },
  downloadNrdReceipt: (code) => axiosClient.get(ENDPOINTS.nrdReceipt(code), { responseType: "blob" }).then((r) => r.data),
  resendCredentials: (code) => axiosClient.post(ENDPOINTS.resendCredentials(code)).then((r) => r.data),
  saveSpecification: (code, body) => axiosClient.put(ENDPOINTS.specification(code), body).then((r) => r.data),
  freezeBaseline: (code) => axiosClient.post(ENDPOINTS.freeze(code)).then((r) => r.data),
  getHalls: (date) => axiosClient.get(ENDPOINTS.halls, { params: date ? { date } : {} }).then((r) => r.data),
  getSnapshot: (projectCode) => axiosClient.get(ENDPOINTS.bootstrap, { params: projectCode ? { projectCode } : {} }).then((r) => r.data),
  getAddenda: (projectCode) => axiosClient.get(ENDPOINTS.addenda, { params: projectCode ? { projectCode } : {} }).then((r) => r.data),
  getRoadmap: (code, owner) => axiosClient.get(ENDPOINTS.manager.roadmap(code), { params: owner ? { owner } : {} }).then((r) => r.data),
  completeRoadmapTask: (code, taskId, body) => axiosClient.post(ENDPOINTS.manager.completeTask(code, taskId), body).then((r) => r.data),
};
